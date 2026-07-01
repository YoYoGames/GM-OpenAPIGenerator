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
                    StructSchemaEmitter.Emit(st, w, n);
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
        public static void Emit(IrSchema.Struct s, GmlWriter w, GmlNaming n)
        {
            var fields = s.Fields.OrderByDescending(f => f.Required).ToList();
            var ctorSig = fields.Select(f => f.Required
                ? NameUtils.ParamName(f.Name)
                : $"{NameUtils.ParamName(f.Name)} = undefined");

            var structName = n.StructPrefix + s.Name;

            w.JsDoc(b =>
            {
                b.Line($"@func {structName}()");
                if (!string.IsNullOrEmpty(s.Description)) b.Description(s.Description);

                foreach (var f in fields)
                {
                    // JSDoc type: if you don't have this yet, just use "Any"
                    // Better: create SchemaJsDoc.ToJsDoc(IrValueSchema,...)
                    var jsType = SchemaJsDoc.ToJsDoc(f.Schema, n);

                    b.Param(new ParamDoc(NameUtils.ParamName(f.Name), jsType, f.Description));
                }
            });

            w.Struct(structName, ctorSig, body =>
            {
                foreach (var f in fields)
                {
                    var arg = NameUtils.ParamName(f.Name);
                    var lhs = NameUtils.IsValidIdent(f.Name) ? f.Name : $"self[$ \"{f.Name}\"]";
                    body.FieldAssign(lhs, arg);
                }
            }).Line();
        }

        public static void EmitValidation(IrSchema.Struct s, IrWebCompilation c, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(c);
            var fields = s.Fields.OrderByDescending(f => f.Required).ToList();
            var structName = n.StructPrefix + s.Name;

            w.JsDoc(js =>
            {
                js.Line($"@func {structName}_validate()");
                js.Param(new ParamDoc("_inst", "Struct", "The struct to be validated."));
                js.Param(new ParamDoc("_where", "String", "What is the callee of this function (used for debug)."));
                js.Tag("ignore");
            });

            w.Function($"{structName}_validate", ["_inst", "_where = _GMFUNCTION_"], fn =>
            {
                fn.Assign("_where", w => w.Append($"$\"{{_where}} :: {structName}_validate\"")).Line();

                foreach (var f in fields)
                {
                    var expr = $"_inst[$ \"{f.Name}\"]";

                    // New: schema-aware checks
                    CheckBuilder.EmitSchema(
                        fn,
                        expr,
                        f.Schema,
                        required: f.Required,
                        resolver,
                        n,
                        where: "_where",
                        name: f.Name
                    );
                }
            }).Line();
        }
    }

    internal static class EnumSchemaEmitter
    {
        // TODO: emit a validator that checks the value is one of the known literals
        public static void EmitValidation(IrSchema.Enum en, IrWebCompilation c, GmlWriter w, GmlNaming n) { }
    }

    internal static class CompositeSchemaEmitter
    {
        // TODO: emit validators for oneOf / anyOf / allOf compositions
        public static void EmitValidation(IrSchema s, IrWebCompilation c, GmlWriter w, GmlNaming n) { }
    }

    internal static class AliasSchemaEmitter
    {
        // TODO: emit a validator that delegates to the aliased schema
        public static void EmitValidation(IrSchema.Alias al, IrWebCompilation c, GmlWriter w, GmlNaming n) { }
    }

    internal static class SchemaJsDoc
    {
        public static string ToJsDoc(IrValueSchema schema, GmlNaming n)
        {
            // For now: collapse composites to "Any"
            // (Emitters can get more precise later.)
            return schema switch
            {
                IrValueSchema.Simple s => TypeToJsDoc(s.Type, n),
                _ => "Any"
            };
        }

        private static string TypeToJsDoc(IrType t, GmlNaming n)
        {
            return t switch
            {
                IrType.Builtin b => b.Kind switch
                {
                    BuiltinKind.Bool => "Bool",
                    BuiltinKind.Int32 or BuiltinKind.Int64 or BuiltinKind.UInt32 or BuiltinKind.UInt64 => "Real",
                    BuiltinKind.Float32 or BuiltinKind.Float64 => "Real",
                    BuiltinKind.String => "String",
                    BuiltinKind.Any or BuiltinKind.AnyArray or BuiltinKind.AnyMap => "Any",
                    BuiltinKind.Buffer => "Id.Buffer",
                    _ => "Any"
                },

                IrType.Array => "Array",
                IrType.Nullable => "Any",
                IrType.Named named => $"{n.StructPrefix}{named.Name}",
                _ => "Any"
            };
        }
    }
}
