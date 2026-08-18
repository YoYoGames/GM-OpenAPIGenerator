using openapigen.Emitters.Gml;
using openapigen.Helpers;
using openapigen.Model;
using openapigen.Utils;

namespace openapigen.Emitters.Controller
{
    /// <summary>Create event: converters, auth store, cookie jar, request maps.</summary>
    public sealed class ControllerCreateEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Create Event (auto-generated, DO NOT EDIT)").Line();
                HttpControllerEmitter.EmitCreateEvent(w, ir, naming);
                w.Line();
            });
        }
    }

    /// <summary>Clean Up event: destroys what the Create event allocated.</summary>
    public sealed class ControllerCleanupEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Clean Up Event (auto-generated, DO NOT EDIT)").Line();
                HttpControllerEmitter.EmitCleanUpEvent(w, ir, naming);
                w.Line();
            });
        }
    }

    /// <summary>Async HTTP event: response dispatch, cookie capture, hooks and callbacks.</summary>
    public sealed class ControllerHttpEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Http Event (auto-generated, DO NOT EDIT)").Line();
                HttpControllerEmitter.EmitHttpEvent(w, ir, naming);
                w.Line();
            });
        }
    }
}
