using CodeGenCore.Helpers;
using CodeGenCore.Writers;
using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.CStyle;
using GMSwaggerCodeGen.Emitters.Gml;
using GMSwaggerCodeGen.Helpers;
using GMSwaggerCodeGen.Ir;

namespace GMSwaggerCodeGen.Emitters.Docs
{
    internal class DocEmitter(GmlNaming naming) : IIrEmitter
    {
        private readonly GmlNaming _n = naming;

        public void Emit(IrWebCompilation ir, string dir)
        {
            Directory.CreateDirectory(dir);

            // schemas
            var w = CodeWriter.From(new IndentedStringBuilder());
            w.Section("Schema Documentation (auto-generated, DO NOT EDIT)").Line();
            foreach (var s in ir.Structs) SchemaEmitter.EmitDocs(s, w, _n);
            File.WriteAllText(Path.Combine(dir, "schemas_doc.js"), w.ToString());

            // endpoints
            w = CodeWriter.From(new IndentedStringBuilder());
            w.Section("Endpoint Documentation (auto-generated, DO NOT EDIT)").Line();
            foreach (var ep in ir.Endpoints) EndpointEmitter.EmitDocs(ep, w, _n);
            File.WriteAllText(Path.Combine(dir, "function_doc.js"), w.ToString());

        }
    }
}
