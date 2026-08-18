using openapigen.Model;

namespace openapigen.Emitters
{
    /// <summary>
    /// Emits one generated artifact. <paramref name="root"/> is the config's resolved output root;
    /// each emitter resolves its own destination file against it.
    /// </summary>
    public interface IIrEmitter { void Emit(IrWebCompilation comp, string root); }
}
