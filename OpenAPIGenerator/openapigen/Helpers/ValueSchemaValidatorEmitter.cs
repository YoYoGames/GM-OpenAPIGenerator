using codegencore.Model;
using codegencore.Writers.Lang;
using openapigen.Model;
using System.Collections.Immutable;

namespace openapigen.Helpers
{
    /// <summary>
    /// Emits runtime type checks for a value against an IR schema.
    ///
    /// This is the single validator emitter: struct fields and endpoint arguments both come through
    /// here, so a given schema is always checked the same way.
    /// </summary>
    internal static class ValueSchemaValidatorEmitter
    {
        public static void Emit(
            GmlWriter w,
            string expr,
            IrValueSchema schema,
            bool required,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar = "__where__",
            string? displayName = null)
        {
            var name = displayName is null ? expr : $"'{displayName}'";

            // A nullable field may legitimately carry no value, and after json_parse that is
            // indistinguishable from the field being absent — so nullability relaxes the presence
            // check exactly the way optionality does. The payload check below is unchanged: when a
            // value *is* there, it still has to be the declared type.
            if (required && IsNullable(schema, resolver))
                required = false;

            if (required)
            {
                EmitRequired(w, expr, schema, resolver, n, whereVar, name, depth: 0);
                return;
            }

            w.If($"!is_undefined({expr})", body =>
                EmitRequired(body, expr, schema, resolver, n, whereVar, name, depth: 0));
        }

        private static bool IsNullable(IrValueSchema schema, SchemaResolver resolver) =>
            resolver.Unalias(schema) is IrValueSchema.Simple s && s.Type is IrType.Nullable;

        private static void EmitRequired(
            GmlWriter w,
            string expr,
            IrValueSchema schema,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar,
            string nameForError,
            int depth)
        {
            switch (resolver.Unalias(schema))
            {
                case IrValueSchema.Simple s:
                    EmitTypeOrNamedSchema(w, expr, s.Type, resolver, n, whereVar, nameForError, depth);
                    return;

                case IrValueSchema.AllOf all:
                    foreach (var part in all.Parts)
                        EmitRequired(w, expr, part, resolver, n, whereVar, nameForError, depth);
                    return;

                case IrValueSchema.AnyOf any:
                    EmitTryMany(w, expr, any.Options, resolver, n, whereVar, nameForError, exactOne: false, depth);
                    return;

                case IrValueSchema.OneOf one:
                    EmitTryMany(w, expr, one.Options, resolver, n, whereVar, nameForError, exactOne: true, depth);
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
            string nameForError,
            int depth)
        {
            // Nullable: the caller's required/optional handling decides undefined; check the payload.
            if (type is IrType.Nullable nn)
            {
                EmitTypeOrNamedSchema(w, expr, nn.Underlying, resolver, n, whereVar, nameForError, depth);
                return;
            }

            if (type is IrType.Array arr)
            {
                var pred = arr.FixedLength is null
                    ? $"!is_array({expr})"
                    : $"(!is_array({expr}) || array_length({expr}) != {arr.FixedLength.Value})";

                w.Line($"if ({pred}) throw $\"{{{whereVar}}} :: {San(nameForError)} expected {DisplayType(type, n)}\";");
                return;
            }

            if (type is IrType.Named named)
            {
                EmitNamedSchemaValidation(w, expr, named, resolver, n, whereVar, nameForError);
                return;
            }

            var pred2 = PredicateForBuiltin(expr, type);
            if (!string.IsNullOrEmpty(pred2))
                w.Line($"if ({pred2}) throw $\"{{{whereVar}}} :: {San(nameForError)} expected {DisplayType(type, n)}\";");
        }

        /// <summary>
        /// Every declared schema gets its own <c>&lt;Type&gt;_validate</c> function, so a named type
        /// is validated by calling it. That keeps the emitted code small and makes recursive schemas
        /// work — the recursion happens at runtime, not during emission.
        /// </summary>
        private static void EmitNamedSchemaValidation(
            GmlWriter w,
            string expr,
            IrType.Named named,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar,
            string nameForError)
        {
            // Unknown schema: nothing to check against, so accept.
            if (!resolver.TryGet(named.Name, out _))
                return;

            // The field name is folded into the location the nested validator reports against.
            // Several fields of one struct commonly share a type, so the type name alone does not
            // say which one was wrong.
            w.Line($"{n.StructPrefix}{named.Name}_validate({expr}, $\"{{{whereVar}}} :: {San(nameForError)}\");");
        }

        /// <summary>Emits an is-string check plus a switch over the enum's literals.</summary>
        public static void EmitEnumCheck(
            GmlWriter w,
            string expr,
            IrSchema.Enum en,
            string whereVar,
            string nameForError)
        {
            var pred = PredicateForBuiltin(expr, en.Underlying);
            if (!string.IsNullOrEmpty(pred))
                w.Line($"if ({pred}) throw $\"{{{whereVar}}} :: {San(nameForError)} expected {DisplayType(en.Underlying, new GmlNaming())}\";");

            if (en.Literals.Length == 0)
                return;

            var isString = en.Underlying is IrType.Builtin { Kind: BuiltinKind.String };

            w.Switch(expr, sw =>
            {
                foreach (var lit in en.Literals)
                {
                    var label = isString ? $"\"{(lit ?? "").Replace("\"", "\\\"")}\"" : lit ?? "0";
                    sw.Case(label, _ => { });
                }

                sw.Default(d =>
                    d.Line($"throw $\"{{{whereVar}}} :: {San(nameForError)} invalid {en.Name} '{{{expr}}}'\";"));
            });
        }

        /// <summary>
        /// oneOf / anyOf: try each option and count the successes. The counter is depth-suffixed so
        /// nested compositions do not redeclare the same local.
        /// </summary>
        private static void EmitTryMany(
            GmlWriter w,
            string expr,
            ImmutableArray<IrValueSchema> options,
            SchemaResolver resolver,
            GmlNaming n,
            string whereVar,
            string nameForError,
            bool exactOne,
            int depth)
        {
            var ok = $"__ok_{depth}__";

            w.Assign(ok, "0", VariableScope.Local);

            foreach (var option in options)
            {
                w.Line("try").Block(body =>
                {
                    EmitRequired(body, expr, option, resolver, n, whereVar, nameForError, depth + 1);
                    body.Line($"{ok} += 1;");
                }).Line($" catch (__e_{depth}__) {{ }}");
            }

            var failed = exactOne ? $"{ok} != 1" : $"{ok} < 1";
            var kind = exactOne ? "oneOf" : "anyOf";
            w.Line($"if ({failed}) throw $\"{{{whereVar}}} :: {San(nameForError)} expected {kind}\";");
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

                // A GML buffer is a handle; buffer_exists is the only meaningful liveness check, but
                // it throws on a string and reports true for any real matching a live buffer id, so
                // is_handle has to gate it.
                BuiltinKind.Buffer => $"!(is_handle({expr}) && buffer_exists({expr}))",

                BuiltinKind.Function => $"!is_callable({expr})",

                BuiltinKind.AnyArray => $"!is_array({expr})",
                BuiltinKind.AnyMap => $"!is_struct({expr})",

                BuiltinKind.Any or BuiltinKind.Void => string.Empty,

                _ => string.Empty
            };
        }

        private static string DisplayType(IrType t, GmlNaming n)
        {
            return t switch
            {
                IrType.Nullable nn => DisplayType(nn.Underlying, n),
                IrType.Array a => $"Array<{DisplayType(a.Element, n)}>",
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
