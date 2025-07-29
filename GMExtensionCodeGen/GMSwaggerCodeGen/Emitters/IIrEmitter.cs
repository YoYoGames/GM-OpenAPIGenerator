
using GMSwaggerCodeGen.Ir;

namespace GMSwaggerCodeGen.Emitters
{
    public interface IIrEmitter { void Emit(IrWebCompilation comp, string outputDir); }
}
