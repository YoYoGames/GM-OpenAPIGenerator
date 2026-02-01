using CodeGenCore.Helpers;
using CodeGenCore.Ir;
using CodeGenCore.Writers;
using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.CStyle;
using CodeGenCore.Writers.Lang.Gml;
using GMSwaggerCodeGen.Helpers;
using openapigen.Model;

namespace GMSwaggerCodeGen.Emitters.Docs
{
    internal class DocsEmitter(GmlNaming naming) : IIrEmitter
    {
        private readonly GmlNaming _n = naming;

        public void Emit(IrWebCompilation ir, string dir)
        {
            Directory.CreateDirectory(dir);

            // schemas
            var w = CodeWriter.From(new IndentedStringBuilder());
            w.Section("Schema Documentation (auto-generated, DO NOT EDIT)").Line();
            foreach (var s in ir.Structs) EmitSchemaDocs(s, w, _n);
            File.WriteAllText(Path.Combine(dir, "schemas_codegen.js"), w.ToString());

            // endpoints
            w = CodeWriter.From(new IndentedStringBuilder());
            w.Section("Endpoint Documentation (auto-generated, DO NOT EDIT)").Line();
            foreach (var ep in ir.Endpoints) EmitEndpointDocs(ep, w, _n);
            File.WriteAllText(Path.Combine(dir, "function_codegen.js"), w.ToString());

        }

        private static void EmitSchemaDocs(IrStruct s, ICodeWriter w, GmlNaming n)
        {
            var fields = s.Fields.OrderByDescending(f => f.Required).ToList();
            var ctorSig = fields.Select(f => f.Required
                            ? NameUtils.ParamName(f.Name)
                            : $"{NameUtils.ParamName(f.Name)} = undefined");

            var structName = n.StructPrefix + s.Name;

            w.JsDoc(b =>
            {
                b.Line($"@struct_partial {structName}");
                //if (!string.IsNullOrEmpty(s.Description)) b.Description(s.Description);
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

        private static void EmitEndpointDocs(IrHttpEndpoint ep, ICodeWriter w, GmlNaming n)
        {
            var ordered = ep.Parameters.OrderByDescending(p => p.Required).ToList();
            var sig = ordered.Select(p => p.Required
                        ? NameUtils.ParamName(p.Name)
                        : $"{NameUtils.ParamName(p.Name)} = undefined")
                 .ToList();

            bool needsBody = ep.Body is not null;
            bool ctChoice = needsBody && ep.Body!.HasChoice;

            if (needsBody) sig.Add("_body = undefined");
            if (ctChoice) sig.Add($"_content_type = \"{ep.Body!.DefaultMediaType}\"");
            sig.Add("_callback = undefined");

            var fnName = $"{n.Pub}{ep.Name}";

            w.JsDoc(js =>
            {
                js.Line($"@func_partial {fnName}");
                if (!string.IsNullOrEmpty(ep.Description)) js.Description(ep.Description);
                foreach (var p in ordered)
                {
                    var desc = p.Description;
                    if (p.Type.IsEnum)
                    {
                        var options = string.Join(" | ", p.Type.EnumLiterals!);
                        desc = $"{desc?.Trim()}( one of: {options}).";
                    }

                    var paramName = p.Required ? NameUtils.ParamName(p.Name) : $"[{NameUtils.ParamName(p.Name)}]";
                    js.Param(new ParamDoc(paramName, p.Type.JsDoc(n), desc));
                }

                if (needsBody) js.Param(new ParamDoc("_body", ep.Body!.Schema.JsDoc(n), "The body to be included in the http request.", true));
                if (ctChoice) js.Param(new ParamDoc("_content_type", "String", "The type of the body (this will be used by the mapper to convert the body argument to the correct type).", true));
                js.Param(new ParamDoc("_callback", "Function", "The function - with signature (status, data, request) - that will be executed upon request completion.", true));
                js.Line("");
                js.Tag("event", "callback");
                js.Tag("member", $"{{Real}} _status");
                js.Tag("member", $"{{{ep.ResponseSchema?.JsDoc(n)}|Undefined}} _data");
                js.Tag("member", $"{{Struct.{n.StructPrefix}Request}} _request");
                js.Tag("event_end");
                js.Line($"@func_end");
            });
            w.Function(fnName, sig, fn => { });
            w.Line();
        }
    }
}
