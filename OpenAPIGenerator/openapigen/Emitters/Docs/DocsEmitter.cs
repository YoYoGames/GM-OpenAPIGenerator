using codegencore.Writers;
using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using openapigen.Emitters.Gml;
using openapigen.Helpers;
using openapigen.Model;

namespace openapigen.Emitters.Docs
{
    internal class DocsEmitter(GmlNaming naming) : IIrEmitter
    {
        private readonly GmlNaming _n = naming;

        public void Emit(IrWebCompilation ir, string dir)
        {
            Directory.CreateDirectory(dir);

            var sw = new StringWriter();
            var w = new GmlWriter(CodeWriter.From(sw));
            w.Section("Schema Documentation (auto-generated, DO NOT EDIT)").Line();
            foreach (var s in ir.Schemas.OfType<IrSchema.Struct>())
                EmitSchemaDocs(s, w, _n);
            File.WriteAllText(Path.Combine(dir, "schemas_codegen.js"), sw.ToString());

            sw = new StringWriter();
            w = new GmlWriter(CodeWriter.From(sw));
            w.Section("Endpoint Documentation (auto-generated, DO NOT EDIT)").Line();
            foreach (var ep in ir.Endpoints)
                EmitEndpointDocs(ep, w, _n);
            File.WriteAllText(Path.Combine(dir, "function_codegen.js"), sw.ToString());
        }

        private static void EmitSchemaDocs(IrSchema.Struct s, GmlWriter w, GmlNaming n)
        {
            var fields = s.Fields.OrderByDescending(f => f.Required).ToList();
            var structName = n.StructPrefix + s.Name;

            w.JsDoc(b =>
            {
                b.Line($"@struct_partial {structName}");

                foreach (var f in fields)
                {
                    var jsType = SchemaJsDoc.ToJsDoc(f.Schema, n);
                    var paramName = f.Required ? NameUtils.ParamName(f.Name) : $"[{NameUtils.ParamName(f.Name)}]";
                    var descPart = f.Description ?? string.Empty;
                    b.Line($"@member {{{jsType}}} {paramName} {descPart}".TrimEnd());
                }

                b.Line("@struct_end");
            });
            w.Line();
        }

        private static void EmitEndpointDocs(IrHttpEndpoint ep, GmlWriter w, GmlNaming n)
        {
            var ordered = ep.Parameters.OrderByDescending(p => p.Required).ToList();
            var sig = ordered
                .Select(p => p.Required
                    ? NameUtils.ParamName(p.Name)
                    : $"{NameUtils.ParamName(p.Name)} = undefined")
                .ToList();

            bool needsBody = ep.Body is not null;
            bool ctChoice  = needsBody && ep.Body!.MediaTypes.Length > 1;

            if (needsBody) sig.Add("_body = undefined");
            if (ctChoice)  sig.Add($"_content_type = \"{ep.Body!.MediaTypes[0]}\"");
            sig.Add("_callback = undefined");

            var fnName = $"{n.Pub}{ep.Name}";

            w.JsDoc(js =>
            {
                js.Line($"@func_partial {fnName}");
                if (!string.IsNullOrEmpty(ep.Description)) js.Description(ep.Description);

                foreach (var p in ordered)
                {
                    var jsType    = SchemaJsDoc.ToJsDoc(p.Schema, n);
                    var paramName = p.Required ? NameUtils.ParamName(p.Name) : $"[{NameUtils.ParamName(p.Name)}]";
                    js.Param(new ParamDoc(paramName, jsType, p.Description));
                }

                if (needsBody)
                {
                    var bodyType = SchemaJsDoc.ToJsDoc(ep.Body!.Schema, n);
                    js.Param(new ParamDoc("_body", bodyType, "The body to be included in the http request.", true));
                }

                if (ctChoice)
                    js.Param(new ParamDoc("_content_type", "String", "The content-type used by the body converter.", true));

                js.Param(new ParamDoc("_callback", "Function", "Callback with signature (status, data, request).", true));

                var responseType = ep.ResponseSchema is null
                    ? "Undefined"
                    : SchemaJsDoc.ToJsDoc(ep.ResponseSchema, n);

                js.Line("");
                js.Tag("event", "callback");
                js.Tag("member", "{Real} _status");
                js.Tag("member", $"{{{responseType}|Undefined}} _data");
                js.Tag("member", $"{{Struct.{n.StructPrefix}Request}} _request");
                js.Tag("event_end");
                js.Line("@func_end");
            });

            w.Function(fnName, sig, _ => { }).Line();
        }
    }
}
