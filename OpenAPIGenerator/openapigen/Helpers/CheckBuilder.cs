using codegencore.Model;
using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;
using System.Collections.Immutable;

namespace openapigen.Helpers
{
    internal static class CheckBuilder
    {
        /// <summary>
        /// Emit validation for an OpenAPI schema value at runtime.
        /// IMPORTANT: oneOf/anyOf requires validators to be non-fatal (throw or return bool).
        /// If your validators use show_error(..., true), oneOf/anyOf cannot be implemented correctly.
        /// </summary>
        public static void EmitSchema(
            GmlWriter w,
            string id,
            IrValueSchema schema,
            bool required,
            SchemaResolver resolver,
            GmlNaming n,
            string where = "_where",
            string? name = null)
        {
            var displayName = string.IsNullOrEmpty(name) ? id : $"'{name}'";

            if (required)
            {
                EmitRequired(w, id, schema, resolver, n, where, displayName);
            }
            else
            {
                w.If($"!is_undefined({id})", body =>
                {
                    EmitRequired(body, id, schema, resolver, n, where, displayName);
                });
            }
        }

        private static void EmitRequired(
            GmlWriter w,
            string id,
            IrValueSchema schema,
            SchemaResolver resolver,
            GmlNaming n,
            string where,
            string displayName)
        {
            // If this schema ultimately is a named schema, prefer calling its validator.
            // That keeps CheckBuilder small and pushes complexity into schema validators.
            // But we also support inline composition here.
            schema = resolver.Unalias(schema);

            switch (schema)
            {
                case IrValueSchema.Simple s:
                    EmitSimple(w, id, s.Type, resolver, n, where, displayName);
                    return;

                case IrValueSchema.AllOf all:
                    EmitAllOf(w, id, all, resolver, n, where, displayName);
                    return;

                case IrValueSchema.AnyOf any:
                    EmitAnyOf(w, id, any, resolver, n, where, displayName);
                    return;

                case IrValueSchema.OneOf one:
                    EmitOneOf(w, id, one, resolver, n, where, displayName);
                    return;

                default:
                    // Unknown schema -> accept anything
                    return;
            }
        }

        private static void EmitSimple(
            GmlWriter w,
            string id,
            IrType t,
            SchemaResolver resolver,
            GmlNaming n,
            string where,
            string displayName)
        {
            // 1) Collections / arrays
            if (t is IrType.Array c)
            {
                var collectionPred = c.FixedLength is null
                    ? $"!is_array({id})"
                    : $"(!is_array({id}) || array_length({id}) != {c.FixedLength.Value})";

                w.Line($"if ({collectionPred}) throw $\"{{{where}}} :: {SanErr(displayName)} expected {DisplayType(t, n)}\";");

                // Optional: validate elements if you want (usually expensive)
                // var elemSchema = new IrValueSchema.Simple(c.Element);
                // w.For(...) ...
                return;
            }

            // 2) Nullable wrapper: allow undefined/null-ish values?
            // Your model uses IrType.Nullable; decide semantics.
            // Here: if value is "undefined" it is allowed only by caller (required=false handles that).
            // If you support explicit null, map it here. GameMaker doesn't have JSON null distinctly.
            if (t is IrType.Nullable nn)
            {
                // Validate underlying when present
                EmitSimple(w, id, nn.Underlying, resolver, n, where, displayName);
                return;
            }

            // 3) Named schemas: call validator by schema kind
            if (t is IrType.Named named)
            {
                EmitNamed(w, id, named.Name, resolver, n, where, displayName);
                return;
            }

            // 4) Builtins (scalar/native/dynamic)
            var pred = PredicateForType(id, t);
            if (string.IsNullOrEmpty(pred)) return;

            w.Line($"if ({pred}) throw $\"{{{where}}} :: {SanErr(displayName)} expected {DisplayType(t, n)}\";");
        }

        private static void EmitNamed(
            GmlWriter w,
            string id,
            string schemaName,
            SchemaResolver resolver,
            GmlNaming n,
            string where,
            string displayName)
        {
            // Follow aliases at the declaration level too
            var decl = resolver.Get(schemaName);
            decl = resolver.UnaliasDecl(decl);

            // Struct / Enum / OneOf / AnyOf / AllOf all get a validator function
            // Convention: <StructPrefix><SchemaName>_validate(value, where)
            var fnName = $"{n.StructPrefix}{schemaName}_validate";

            // For non-struct schemas, you still generate a validator stub later.
            // For now, always call it if the schema exists.
            w.Line($"{fnName}({id}, {where});");
        }

        private static void EmitAllOf(
            GmlWriter w,
            string id,
            IrValueSchema.AllOf all,
            SchemaResolver resolver,
            GmlNaming n,
            string where,
            string displayName)
        {
            // allOf: all parts must validate
            foreach (var part in all.Parts)
                EmitRequired(w, id, part, resolver, n, where, displayName);
        }

        private static void EmitAnyOf(
            GmlWriter w,
            string id,
            IrValueSchema.AnyOf any,
            SchemaResolver resolver,
            GmlNaming n,
            string where,
            string displayName)
        {
            // anyOf: at least one must validate
            EmitAtLeastOne(w, id, any.Options, resolver, n, where, displayName, exactOne: false);
        }

        private static void EmitOneOf(
            GmlWriter w,
            string id,
            IrValueSchema.OneOf one,
            SchemaResolver resolver,
            GmlNaming n,
            string where,
            string displayName)
        {
            // oneOf: exactly one must validate
            EmitAtLeastOne(w, id, one.Options, resolver, n, where, displayName, exactOne: true);
        }

        private static void EmitAtLeastOne(
            GmlWriter w,
            string id,
            ImmutableArray<IrValueSchema> options,
            SchemaResolver resolver,
            GmlNaming n,
            string where,
            string displayName,
            bool exactOne)
        {
            // This requires validators to be non-fatal (throw or return bool).
            // We implement via try/catch and counting successes.
            w.Line("var __ok = 0;");

            for (int i = 0; i < options.Length; i++)
            {
                w.Line($"try").Block(body => {
                    EmitRequired(body, id, options[i], resolver, n, where, displayName);
                    body.Line("__ok += 1;");
                }).Line(" catch(__e) { }");
            }

            if (exactOne)
            {
                w.Line($"if (__ok != 1) throw $\"{{{where}}} :: {SanErr(displayName)} expected oneOf\";");
            }
            else
            {
                w.Line($"if (__ok < 1) throw $\"{{{where}}} :: {SanErr(displayName)} expected anyOf\";");
            }
        }

        private static string PredicateForType(string id, IrType t)
        {
            // Note: Nullable is handled by the caller (required/undefined logic).
            // If you have explicit "null" in your GML representation, add it here.
            return t switch
            {
                IrType.Nullable n => PredicateForType(id, n.Underlying),

                IrType.Array a => a.FixedLength is null
                    ? $"!is_array({id})"
                    : $"(!is_array({id}) || array_length({id}) != {a.FixedLength.Value})",

                IrType.Named => $"!is_struct({id})", // schema-specific validator will do deeper checks

                IrType.Builtin b => b.Kind switch
                {
                    BuiltinKind.String => $"!is_string({id})",

                    // GameMaker uses "Real" for numbers; you can’t distinguish ints vs floats at runtime
                    BuiltinKind.Bool => $"!is_bool({id})",
                    BuiltinKind.Int8 or BuiltinKind.UInt8 or
                    BuiltinKind.Int16 or BuiltinKind.UInt16 or
                    BuiltinKind.Int32 or BuiltinKind.UInt32 or
                    BuiltinKind.Int64 or BuiltinKind.UInt64 or
                    BuiltinKind.Float32 or BuiltinKind.Float64 => $"!is_real({id})",

                    // Wrapper-generator pragmatic mapping:
                    // - Buffer/Function: treat them as runtime kinds if you expect them
                    BuiltinKind.Function => $"!is_callable({id})",
                    BuiltinKind.Buffer => $"!is_array({id}) && !is_struct({id})", // placeholder: depends on how you represent bytes in GML

                    // "Any" means accept anything, so no predicate
                    BuiltinKind.Any or BuiltinKind.AnyArray or BuiltinKind.AnyMap => string.Empty,

                    BuiltinKind.Void => string.Empty,

                    _ => string.Empty
                },

                _ => string.Empty
            };
        }

        private static string DisplayType(IrType t, GmlNaming n)
        {
            return t switch
            {
                IrType.Nullable nn => DisplayType(nn.Underlying, n),

                IrType.Array a => a.FixedLength is null
                    ? $"Array<{DisplayType(a.Element, n)}>"
                    : $"Array[{a.FixedLength.Value}]<{DisplayType(a.Element, n)}>",

                IrType.Named named =>
                    // Do NOT assume StructPrefix is correct for enums; your naming may differ.
                    // For now: treat named schemas as their schema name with prefix.
                    $"{n.StructPrefix}{named.Name}",

                IrType.Builtin b => b.Kind switch
                {
                    BuiltinKind.Bool => "Bool",

                    BuiltinKind.String => "String",

                    BuiltinKind.Int8 or BuiltinKind.UInt8 or
                    BuiltinKind.Int16 or BuiltinKind.UInt16 or
                    BuiltinKind.Int32 or BuiltinKind.UInt32 or
                    BuiltinKind.Int64 or BuiltinKind.UInt64 => "Real",

                    BuiltinKind.Float32 or BuiltinKind.Float64 => "Real",

                    BuiltinKind.Buffer => "Buffer",
                    BuiltinKind.Function => "Function",

                    BuiltinKind.Any => "Any",
                    BuiltinKind.AnyArray => "Array",
                    BuiltinKind.AnyMap => "Struct",

                    BuiltinKind.Void => "Void",

                    _ => "Any"
                },

                _ => "Any"
            };
        }


        private static string SanErr(string s) => s.Replace("\"", "'");
    }

    /// <summary>
    /// Minimal resolver used by CheckBuilder. Keep it in your emitter project.
    /// </summary>
    internal sealed class SchemaResolver
    {
        private readonly IReadOnlyDictionary<string, IrSchema> _map;

        public SchemaResolver(IrWebCompilation compilation)
        {
            _map = compilation.Schemas.ToDictionary(s => s.Name, s => s);
        }

        public bool TryGet(string name, out IrSchema schema) => _map.TryGetValue(name, out schema!);

        public IrSchema Get(string name) => _map[name];

        public IrValueSchema Unalias(IrValueSchema schema)
        {
            // resolves Simple(Named(alias)) -> alias.Schema, repeatedly
            var current = schema;
            for (int i = 0; i < 32; i++)
            {
                if (current is not IrValueSchema.Simple simple) return current;
                if (simple.Type is not IrType.Named named) return current;

                if (!_map.TryGetValue(named.Name, out var decl)) return current;
                if (decl is not IrSchema.Alias a) return current;

                current = a.Schema;
            }
            return current;
        }

        public IrSchema UnaliasDecl(IrSchema decl)
        {
            var current = decl;
            for (int i = 0; i < 32; i++)
            {
                if (current is not IrSchema.Alias a) return current;
                var target = Unalias(a.Schema);

                // If alias points to a named schema, follow it
                if (target is IrValueSchema.Simple s && s.Type is IrType.Named n && _map.TryGetValue(n.Name, out var next))
                {
                    current = next;
                    continue;
                }

                return current;
            }
            return current;
        }
    }
}
