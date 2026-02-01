using GMSwaggerCodeGen.Helpers;
using openapigen.Emitters.Gml;
using openapigen.Model;

namespace GMSwaggerCodeGen.Emitters.Gml
{

    public sealed class HttpGmlEmitter(GmlNaming naming) : IIrEmitter
    {
        private readonly GmlNaming _n = naming;


        public void Emit(IrWebCompilation ir, string dir)
        {
            Directory.CreateDirectory(dir);

            // schemas
            var w = CodeWriter.From(new IndentedStringBuilder());
            w.Section("Schema Definitions (auto-generated, DO NOT EDIT)").Line();
            foreach (var s in ir.Structs) SchemaEmitter.Emit(s, w, _n);
            foreach (var s in ir.Structs) SchemaEmitter.EmitValidation(s, w, _n);
            File.WriteAllText(Path.Combine(dir, "generated_schemas.gml"), w.ToString());

            // endpoints
            w = CodeWriter.From(new IndentedStringBuilder());
            w.Section("Endpoint Definitions (auto-generated, DO NOT EDIT)").Line();
            foreach (var ep in ir.Endpoints) EndpointEmitter.Emit(ep, w, _n);
            File.WriteAllText(Path.Combine(dir, "generated_http.gml"), w.ToString());

            // http helper
            w = CodeWriter.From(new IndentedStringBuilder());
            w.Section("Internal Definitions (auto-generated, DO NOT EDIT)").Line();
            HttpHelperEmitter.Emit(ir, w, _n);
            File.WriteAllText(Path.Combine(dir, "generated_helpers.gml"), w.ToString());

            // http controller (gamemaker object that should be created)
            HttpControllerEmitter.Emit(ir, dir, _n);
        }
    }

}
