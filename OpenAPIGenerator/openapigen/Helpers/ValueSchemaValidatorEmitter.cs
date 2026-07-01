using codegencore.Model;
using codegencore.Writers;
using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;
using System.Collections.Immutable;

namespace openapigen.Helpers
{
    internal static class ValueSchemaValidatorEmitter
    {
        public static void Emit(
            GmlWriter w,
            string expr,
            IrValueSchema schema,
            bool required,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar = "_where",
            string? displayName = null)
        {
            var name = displayName is null ? expr : $"'{displayName}'";

            if (!required)
            {
                w.If($"!is_undefined({expr})", body =>
                {
                    EmitRequired(body, expr, schema, resolver, n, whereVar, name);
                });
                return;
            }

            EmitRequired(w, expr, schema, resolver, n, whereVar, name);
        }

        private static void EmitRequired(
            GmlWriter w,
            string expr,
            IrValueSchema schema,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar,
            string nameForError)
        {
            switch (schema)
            {
                case IrValueSchema.Simple s:
                    EmitTypeOrNamedSchema(w, expr, s.Type, resolver, n, whereVar, nameForError);
                    return;

                case IrValueSchema.AllOf all:
                    foreach (var part in all.Parts)
                        EmitRequired(w, expr, part, resolver, n, whereVar, nameForError);
                    return;

                case IrValueSchema.AnyOf any:
                    EmitTryMany(w, expr, any.Options, resolver, n, whereVar, nameForError, exactOne: false);
                    return;

                case IrValueSchema.OneOf one:
                    EmitTryMany(w, expr, one.Options, resolver, n, whereVar, nameForError, exactOne: true);
                    return;

                default:
                    return;
            }
        }

        private static void EmitTypeOrNamedSchema(
            GmlWriter w,
            string expr,
            IrType type,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar,
            string nameForError)
        {
            // Nullable: let caller decide undefined semantics; validate underlying if present
            if (type is IrType.Nullable nn)
            {
                EmitTypeOrNamedSchema(w, expr, nn.Underlying, resolver, n, whereVar, nameForError);
                return;
            }

            // Arrays
            if (type is IrType.Array arr)
            {
                var pred = arr.FixedLength is null
                    ? $"!is_array({expr})"
                    : $"(!is_array({expr}) || array_length({expr}) != {arr.FixedLength.Value})";

                w.Line($"if ({pred}) throw $\"{{{whereVar}}} :: {San(nameForError)} expected {DisplayType(type, n)}\";");

                // Optional: validate elements (expensive; leave off unless you want it)
                // for (var i=0; i<array_length(expr); i++) validate elem...
                return;
            }

            // Named schema
            if (type is IrType.Named named)
            {
                EmitNamedSchemaValidation(w, expr, named.Name, resolver, n, whereVar, nameForError);
                return;
            }

            // Builtin
            var pred2 = PredicateForBuiltin(expr, type);
            if (!string.IsNullOrEmpty(pred2))
                w.Line($"if ({pred2}) throw $\"{{{whereVar}}} :: {San(nameForError)} expected {DisplayType(type, n)}\";");
        }

        private static void EmitNamedSchemaValidation(
            GmlWriter w,
            string expr,
            string schemaName,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar,
            string nameForError)
        {
            // If we don't know the schema, accept (or throw). Pragmatic: accept.
            if (!resolver.TryGet(schemaName, out var decl))
                return;

            switch (decl)
            {
                case IrSchema.Struct:
                    // Only structs get global validators in your plan
                    w.Line($"{n.StructPrefix}{schemaName}_validate({expr}, {whereVar});");
                    return;

                case IrSchema.Enum en:
                    // Inline enum validation (strings only per your decision)
                    w.Line($"if (!is_string({expr})) throw $\"{{{whereVar}}} :: {San(nameForError)} expected String\";");
                    w.Line("switch (" + expr + ")");
                    w.Line("{");
                    w.Indent();
                    foreach (var lit in en.Literals)
                    {
                        var s = (lit ?? "").Replace("\"", "\\\"");
                        w.Line($"case \"{s}\": break;");
                    }
                    w.Line("default:");
                    w.Indent();
                    w.Line($"throw $\"{{{whereVar}}} :: {San(nameForError)} invalid {schemaName} '{{{expr}}}'\";");
                    w.Unindent();
                    w.Unindent();
                    w.Line("}");
                    return;

                case IrSchema.Alias a:
                    EmitRequired(w, expr, a.Schema, resolver, n, whereVar, nameForError);
                    return;

                case IrSchema.OneOf o:
                    EmitTryMany(w, expr, o.Schema is IrValueSchema.OneOf ov ? ov.Options : o.Schema switch
                    {
                        IrValueSchema.OneOf ov2 => ov2.Options,
                        _ => []
                    }, resolver, n, whereVar, nameForError, exactOne: true);
                    return;

                case IrSchema.AnyOf a2:
                    EmitTryMany(w, expr, a2.Schema is IrValueSchema.AnyOf av ? av.Options : a2.Schema switch
                    {
                        IrValueSchema.AnyOf av2 => av2.Options,
                        _ => []
                    }, resolver, n, whereVar, nameForError, exactOne: false);
                    return;

                case IrSchema.AllOf all:
                    if (all.Schema is IrValueSchema.AllOf parts)
                    {
                        foreach (var p in parts.Parts)
                            EmitRequired(w, expr, p, resolver, n, whereVar, nameForError);
                    }
                    return;

                default:
                    return;
            }
        }

        private static void EmitTryMany(
            GmlWriter w,
            string expr,
            ImmutableArray<IrValueSchema> options,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar,
            string nameForError,
            bool exactOne)
        {
            w.Line("var __ok = 0;");
            for (int i = 0; i < options.Length; i++)
            {
                w.Line("try").Block(b => {
                    EmitRequired(b, expr, options[i], resolver, n, whereVar, nameForError);
                    b.Line("__ok += 1;");
                }).Line(" catch (__e) { }");
            }

            if (exactOne)
                w.Line($"if (__ok != 1) throw $\"{{{whereVar}}} :: {San(nameForError)} expected oneOf\";");
            else
                w.Line($"if (__ok < 1) throw $\"{{{whereVar}}} :: {San(nameForError)} expected anyOf\";");
        }

        private static string PredicateForBuiltin(string expr, IrType t)
        {
            if (t is not IrType.Builtin b) return string.Empty;

            return b.Kind switch
            {
                BuiltinKind.String => $"!is_string({expr})",
                BuiltinKind.Bool => $"!is_bool({expr})",

                BuiltinKind.Int8 or BuiltinKind.UInt8 or
                BuiltinKind.Int16 or BuiltinKind.UInt16 or
                BuiltinKind.Int32 or BuiltinKind.UInt32 or
                BuiltinKind.Int64 or BuiltinKind.UInt64 or
                BuiltinKind.Float32 or BuiltinKind.Float64 => $"!is_real({expr})",

                // bytes => expect buffer id (real)
                BuiltinKind.Buffer => $"!is_real({expr})",

                BuiltinKind.Function => $"!is_callable({expr})",

                BuiltinKind.Any or BuiltinKind.AnyArray or BuiltinKind.AnyMap => string.Empty,
                BuiltinKind.Void => string.Empty,

                _ => string.Empty
            };
        }

        private static string DisplayType(IrType t, GmlNaming n)
        {
            return t switch
            {
                IrType.Nullable nn => DisplayType(nn.Underlying, n),
                IrType.Array a => "Array",
                IrType.Named named => $"{n.StructPrefix}{named.Name}",

                IrType.Builtin b => b.Kind switch
                {
                    BuiltinKind.String => "String",
                    BuiltinKind.Bool => "Bool",
                    BuiltinKind.Buffer => "Id.Buffer",
                    BuiltinKind.Function => "Function",
                    BuiltinKind.Any => "Any",
                    BuiltinKind.AnyArray => "Array",
                    BuiltinKind.AnyMap => "Struct",
                    BuiltinKind.Void => "Void",
                    _ => "Real"
                },

                _ => "Any"
            };
        }

        private static string San(string s) => s.Replace("\"", "'");
    }
}
