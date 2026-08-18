using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;
using openapigen.Utils;

namespace openapigen.Emitters.Gml
{
    /// <summary>Endpoint wrapper functions — one per OpenAPI operation.</summary>
    public sealed class EndpointsEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Endpoint Definitions (auto-generated, DO NOT EDIT)").Line();
                foreach (var ep in ir.Endpoints)
                    EndpointEmitter.Emit(ep, ir, w, naming);
            });
        }
    }

    /// <summary>Schema constructors and their validators.</summary>
    public sealed class SchemasEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Schema Definitions (auto-generated, DO NOT EDIT)").Line();
                SchemaEmitter.EmitAll(ir, w, naming);
            });
        }
    }

    /// <summary>Internal runtime: request struct, auth, cookies, body converters.</summary>
    public sealed class HelpersEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Internal Definitions (auto-generated, DO NOT EDIT)").Line();
                HttpHelperEmitter.Emit(ir, w, naming);
            });
        }
    }
}
