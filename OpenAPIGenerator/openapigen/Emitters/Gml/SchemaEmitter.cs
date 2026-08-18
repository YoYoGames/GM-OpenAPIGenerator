using codegencore.Model;
using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;

namespace openapigen.Emitters.Gml
{
    internal static class SchemaEmitter
    {
        public static void EmitAll(IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            // 1) Constructors (struct only)
            foreach (var s in c.Schemas.OrderBy(x => x.Name))
            {
                if (s is IrSchema.Struct st)
                    StructSchemaEmitter.Emit(st, c, w, n);
            }

            // 2) Validators (all schemas)
            foreach (var s in c.Schemas.OrderBy(x => x.Name))
            {
                SchemaValidatorEmitter.Emit(s, c, w, n);
            }
        }
    }

    internal static class SchemaValidatorEmitter
    {
        public static void Emit(IrSchema s, IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            switch (s)
            {
                case IrSchema.Struct st:
                    StructSchemaEmitter.EmitValidation(st, c, w, n);
                    break;

                case IrSchema.Enum en:
                    EnumSchemaEmitter.EmitValidation(en, c, w, n);
                    break;

                case IrSchema.OneOf o:
                    CompositeSchemaEmitter.EmitValidation(o, c, w, n);
                    break;

                case IrSchema.AnyOf a:
                    CompositeSchemaEmitter.EmitValidation(a, c, w, n);
                    break;

                case IrSchema.AllOf all:
                    CompositeSchemaEmitter.EmitValidation(all, c, w, n);
                    break;

                case IrSchema.Alias al:
                    AliasSchemaEmitter.EmitValidation(al, c, w, n);
                    break;

                default:
                    // no-op
                    break;
            }
        }
    }

    internal static class StructSchemaEmitter
    {
        public static void Emit(IrSchema.Struct s, IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(c);
            var fields = BuildFields(s);
            var structName = n.StructPrefix + s.Name;

            var ctorSig = fields
                .Select(f => f.Field.Required && f.Default is null
                    ? f.Arg
                    : $"{f.Arg} = {f.Default ?? "undefined"}")
                .ToList();

            w.JsDoc(b =>
            {
                b.Line($"@func {structName}({string.Join(", ", ctorSig)})");
                if (!string.IsNullOrEmpty(s.Description)) b.Description(s.Description);

                foreach (var f in fields)
                {
                    var jsType = SchemaJsDoc.ToJsDoc(f.Field.Schema, n, resolver);
                    var name = f.Field.Required ? f.Arg : $"[{f.Arg}]";
                    b.Param(new ParamDoc(name, jsType, f.Field.Description));
                }
            });

            w.Struct(structName, ctorSig, body =>
            {
                foreach (var f in fields)
                    body.FieldAssign(MemberRef(f.Field.Name), f.Arg);
            }).Line();
        }

        /// <summary>
        /// A struct member is written bare only when the name is a legal, non-reserved GML
        /// identifier; anything else — "weird-prop.name", or a keyword like "end" — must go through
        /// the struct accessor.
        /// </summary>
        internal static string MemberRef(string fieldName) =>
            NameUtils.IsValidIdent(fieldName)
                ? fieldName
                : $"self[$ \"{fieldName.Replace("\"", "\\\"")}\"]";

        internal sealed record CtorField(IrField Field, string Arg, string? Default);

        /// <summary>
        /// Orders fields required-first and assigns each a unique constructor argument name —
        /// distinct spec fields can collapse onto the same snake_case name ("userId" / "user_id").
        /// </summary>
        internal static List<CtorField> BuildFields(IrSchema.Struct s)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<CtorField>();

            foreach (var f in s.Fields.OrderByDescending(f => f.Required))
            {
                var arg = NameUtils.ParamName(f.Name);

                if (!used.Add(arg))
                {
                    var i = 2;
                    string candidate;
                    do candidate = $"{arg}_{i++}";
                    while (!used.Add(candidate));
                    arg = candidate;
                }

                result.Add(new CtorField(f, arg, GmlLiteral.For(f.DefaultLiteral, f.Schema)));
            }

            return result;
        }

        public static void EmitValidation(IrSchema.Struct s, IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(c);
            var fields = s.Fields.OrderByDescending(f => f.Required).ToList();
            var structName = n.StructPrefix + s.Name;

            ValidatorScaffold.Emit(w, structName, n, fn =>
            {
                // Without this the first field access fails inside the engine
                // ("struct_get_from_hash argument 1 incorrect type"), and the caller never sees the
                // message this function exists to produce. Only the struct path gets the guard —
                // enum, alias and composite validators are handed non-structs by design.
                fn.Line($"if (!is_struct({ValidatorScaffold.InstVar})) " +
                        $"throw $\"{{{ValidatorScaffold.WhereVar}}} :: expected Struct.{structName}\";")
                  .Line();

                foreach (var f in fields)
                {
                    ValueSchemaValidatorEmitter.Emit(
                        fn,
                        $"{ValidatorScaffold.InstVar}[$ \"{f.Name.Replace("\"", "\\\"")}\"]",
                        f.Schema,
                        required: f.Required,
                        resolver,
                        n,
                        ValidatorScaffold.WhereVar,
                        f.Name);
                }
            });
        }
    }

    /// <summary>
    /// Shared shape of every generated <c>&lt;Type&gt;_validate</c> function, so struct, enum,
    /// alias and composite validators are callable interchangeably.
    /// </summary>
    internal static class ValidatorScaffold
    {
        public const string InstVar = "__inst__";
        public const string WhereVar = "__where__";

        public static void Emit(GmlWriter w, string typeName, GmlNaming n, Action<GmlWriter> body)
        {
            w.JsDoc(js =>
            {
                js.Line($"@func {typeName}_validate({InstVar}, {WhereVar})");
                js.Param(new ParamDoc(InstVar, "Any", "The value to be validated."));
                js.Param(new ParamDoc($"[{WhereVar}]", "String", "Caller location, used in error messages."));
                js.Tag("ignore");
            });

            w.Function($"{typeName}_validate", [InstVar, $"{WhereVar} = _GMFUNCTION_"], fn =>
            {
                fn.Assign(WhereVar, w2 => w2.Append($"$\"{{{WhereVar}}} :: {typeName}_validate\"")).Line();
                body(fn);
            }).Line();
        }
    }

    internal static class EnumSchemaEmitter
    {
        /// <summary>Validates that the value is one of the enum's declared literals.</summary>
        public static void EmitValidation(IrSchema.Enum en, IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(c);
            var typeName = n.StructPrefix + en.Name;

            ValidatorScaffold.Emit(w, typeName, n, fn =>
                ValueSchemaValidatorEmitter.EmitEnumCheck(
                    fn, ValidatorScaffold.InstVar, en, ValidatorScaffold.WhereVar, $"'{en.Name}'"));
        }
    }

    internal static class CompositeSchemaEmitter
    {
        /// <summary>Validates oneOf / anyOf / allOf compositions.</summary>
        public static void EmitValidation(IrSchema s, IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(c);
            var typeName = n.StructPrefix + s.Name;

            ValidatorScaffold.Emit(w, typeName, n, fn =>
                ValueSchemaValidatorEmitter.Emit(
                    fn, ValidatorScaffold.InstVar, s.Schema, required: true,
                    resolver, n, ValidatorScaffold.WhereVar, s.Name));
        }
    }

    internal static class AliasSchemaEmitter
    {
        /// <summary>Delegates to whatever the alias ultimately points at.</summary>
        public static void EmitValidation(IrSchema.Alias al, IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(c);
            var typeName = n.StructPrefix + al.Name;

            ValidatorScaffold.Emit(w, typeName, n, fn =>
                ValueSchemaValidatorEmitter.Emit(
                    fn, ValidatorScaffold.InstVar, al.Target, required: true,
                    resolver, n, ValidatorScaffold.WhereVar, al.Name));
        }
    }

    /// <summary>
    /// Maps IR types to GameMaker Feather JSDoc type names.
    /// </summary>
    internal static class SchemaJsDoc
    {
        public static string ToJsDoc(IrValueSchema schema, GmlNaming n) => ToJsDoc(schema, n, null);

        /// <summary>
        /// Renders a Feather type. Pass a <paramref name="resolver"/> so named schemas resolve to
        /// what they actually are at runtime — an enum or a string alias is a String, not a struct.
        /// </summary>
        public static string ToJsDoc(IrValueSchema schema, GmlNaming n, SchemaResolver? resolver)
        {
            return schema switch
            {
                IrValueSchema.Simple s => TypeToJsDoc(s.Type, n, resolver),
                IrValueSchema.OneOf o => Union(o.Options, n, resolver),
                IrValueSchema.AnyOf a => Union(a.Options, n, resolver),
                _ => "Any"
            };
        }

        private static string Union(System.Collections.Immutable.ImmutableArray<IrValueSchema> options, GmlNaming n, SchemaResolver? resolver)
        {
            var parts = options.Select(o => ToJsDoc(o, n, resolver)).Distinct().ToArray();
            return parts.Length == 0 ? "Any" : string.Join("|", parts);
        }

        private static string TypeToJsDoc(IrType t, GmlNaming n, SchemaResolver? resolver)
        {
            return t switch
            {
                IrType.Builtin b => b.Kind switch
                {
                    BuiltinKind.Bool => "Bool",
                    BuiltinKind.Int32 or BuiltinKind.Int64 or BuiltinKind.UInt32 or BuiltinKind.UInt64 => "Real",
                    BuiltinKind.Float32 or BuiltinKind.Float64 => "Real",
                    BuiltinKind.String => "String",
                    BuiltinKind.Buffer => "Id.Buffer",
                    BuiltinKind.Function => "Function",
                    // A free-form object is a plain struct; a free-form array is a plain array.
                    BuiltinKind.AnyMap => "Struct",
                    BuiltinKind.AnyArray => "Array",
                    _ => "Any"
                },

                // Feather understands Array<T> for a known element type.
                IrType.Array a => ElementOf(a, n, resolver) is { Length: > 0 } elem and not "Any"
                    ? $"Array<{elem}>"
                    : "Array",

                IrType.Nullable nn => TypeToJsDoc(nn.Underlying, n, resolver),

                IrType.Named named => NamedToJsDoc(named, n, resolver),

                _ => "Any"
            };
        }

        private static string ElementOf(IrType.Array a, GmlNaming n, SchemaResolver? resolver) =>
            TypeToJsDoc(a.Element, n, resolver);

        /// <summary>
        /// Structs render as <c>Struct.GmName</c>, which is what Feather expects. Enums and aliases
        /// are not structs at runtime, so they render as their underlying scalar type.
        /// </summary>
        private static string NamedToJsDoc(IrType.Named named, GmlNaming n, SchemaResolver? resolver)
        {
            var structName = $"Struct.{n.StructPrefix}{named.Name}";

            if (resolver is null || !resolver.TryGet(named.Name, out var decl))
                return named.Kind == NamedKind.Enum ? "String" : structName;

            return resolver.UnaliasDecl(decl) switch
            {
                IrSchema.Struct => structName,
                IrSchema.Enum en => TypeToJsDoc(en.Underlying, n, resolver),
                IrSchema.Alias al => ToJsDoc(al.Target, n, resolver),
                _ => "Any"
            };
        }
    }
}
