using codegencore.Writers;
using codegencore.Writers.Lang;
using openapigen.Emitters.Gml;
using openapigen.Helpers;
using openapigen.Model;

namespace openapigen.Emitters.Gml
{
    public sealed class HttpGmlEmitter(GmlNaming naming) : IIrEmitter
    {
        private readonly GmlNaming _n = naming;

        public void Emit(IrWebCompilation ir, string dir)
        {
            Directory.CreateDirectory(dir);

            var sw = new StringWriter();
            var w = new GmlWriter(CodeWriter.From(sw));
            w.Section("Schema Definitions (auto-generated, DO NOT EDIT)").Line();
            SchemaEmitter.EmitAll(ir, w, _n);
            File.WriteAllText(Path.Combine(dir, "generated_schemas.gml"), sw.ToString());

            sw = new StringWriter();
            w = new GmlWriter(CodeWriter.From(sw));
            w.Section("Endpoint Definitions (auto-generated, DO NOT EDIT)").Line();
            foreach (var ep in ir.Endpoints) EndpointEmitter.Emit(ep, ir, w, _n);
            File.WriteAllText(Path.Combine(dir, "generated_http.gml"), sw.ToString());

            sw = new StringWriter();
            w = new GmlWriter(CodeWriter.From(sw));
            w.Section("Internal Definitions (auto-generated, DO NOT EDIT)").Line();
            HttpHelperEmitter.Emit(ir, w, _n);
            File.WriteAllText(Path.Combine(dir, "generated_helpers.gml"), sw.ToString());

            HttpControllerEmitter.Emit(ir, dir, _n);
        }
    }
}
