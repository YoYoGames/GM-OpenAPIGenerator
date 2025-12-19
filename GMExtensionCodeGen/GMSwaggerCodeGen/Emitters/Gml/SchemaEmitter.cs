using CodeGenCore.Helpers;
using CodeGenCore.Ir;
using CodeGenCore.Writers;
using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.Gml;
using GMSwaggerCodeGen.Helpers;

namespace GMSwaggerCodeGen.Emitters.Gml
{
    internal static class SchemaEmitter
    {
        public static void Emit(IrStruct s, ICodeWriter w, GmlNaming n)
        {
            var fields = s.Fields.OrderByDescending(f => f.Required).ToList();
            var ctorSig = fields.Select(f => f.Required
                                        ? NameUtils.ParamName(f.Name)
                                        : $"{NameUtils.ParamName(f.Name)} = undefined");

            var structName = n.StructPrefix + s.Name;
            var uid = StringHash.ToUInt32(structName).ToString();

            /* JSDoc */
            w.JsDoc(b =>
            {
                b.Line($"@func {structName}()");
                if (!string.IsNullOrEmpty(s.Description)) b.Summary(s.Description);
                foreach (var f in fields)
                {
                    var desc = f.Description;
                    if (f.Type.IsEnum)
                    {
                        var options = string.Join(" | ", f.Type.EnumLiterals!);
                        desc = $"{desc?.Trim()} (one of: {options})";
                    }

                    b.Param(new ParamDoc(NameUtils.ParamName(f.Name), f.Type.JsDoc(n), desc));
                }
            });

            /* struct constructor */
            w.Struct(structName, ctorSig, body =>
            {
                foreach (var f in fields)
                {
                    var arg = NameUtils.ParamName(f.Name);
                    var lhs = NameUtils.IsValidIdent(f.Name) ? f.Name : $"self[$ \"{f.Name}\"]";
                    body.FieldAssign(lhs, arg);
                }
                body.Line();
                body.Assign("__uid", uid, VariableScope.Static).Line();

                EmitValidate(body, fields, n, structName);
            })
            .Line();
        }

        private static void EmitValidate(ICodeWriter body, IEnumerable<IrField> fields,
                                         GmlNaming n, string structName)
        {
            body.JsDoc(js =>
            {
                js.Line("@func validate()");
                js.Param(new ParamDoc("_where", "String", "What is the callee of this function (used for debug)."));
                js.Tag("ignore");
            });

            body.Assign("validate", expr => expr.Method(["_where = _GMFUNCTION_"], fn =>
            {
                fn.Assign("_where", w => w.Append($"$\"{{_where}} :: {structName}.validate\"")).Line();

                foreach (var f in fields)
                {
                    var acc = NameUtils.IsValidIdent(f.Name) ? f.Name : $"self[$ \"{f.Name}\"]";
                    CheckBuilder.Emit(fn, acc, f.Type, f.Required, n, "_where");
                }
            }), VariableScope.Static);
        }


        public static void EmitDocs(IrStruct s, ICodeWriter w, GmlNaming n) 
        {
            var fields = s.Fields.OrderByDescending(f => f.Required).ToList();
            var ctorSig = fields.Select(f => f.Required
                            ? NameUtils.ParamName(f.Name)
                            : $"{NameUtils.ParamName(f.Name)} = undefined");

            var structName = n.StructPrefix + s.Name;

            w.JsDoc(b =>
            {
                b.Line($"@struct {structName}");
                if (!string.IsNullOrEmpty(s.Description)) b.Summary(s.Description);
                foreach (var f in fields)
                {
                    var desc = f.Description;
                    if (f.Type.IsEnum)
                    {
                        var options = string.Join(" | ", f.Type.EnumLiterals!);
                        desc = $"{desc?.Trim()} (one of: {options})";
                    }

                    var paramName = f.Required ? NameUtils.ParamName(f.Name) : $"[{NameUtils.ParamName(f.Name)}]";

                    var typePart = $"{{{f.Type.JsDoc(n)}}} " ?? string.Empty;
                    var descPart = f.Description ?? string.Empty;
                    b.Line($"@member {typePart}{paramName} {descPart}".TrimEnd());
                }
                b.Line($"@struct_end");
            });
            w.Line();
        }
    
    }

}
