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

            /* JSDoc */
            w.JsDoc(b =>
            {
                b.Line($"@func {structName}()");
                if (!string.IsNullOrEmpty(s.Description)) b.Description(s.Description);
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
            })
            .Line();
        }

        public static void EmitValidation(IrStruct s, ICodeWriter w, GmlNaming n)
        {
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
                    CheckBuilder.Emit(fn, $"_inst[$ \"{f.Name}\"]", f.Type, f.Required, n, "_where", f.Name);
                }
            }).Line();
        }
    
    }

}
