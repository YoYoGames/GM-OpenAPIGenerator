using System.Collections.Immutable;

namespace CodeGenCore.Ir
{
    public sealed record IrFunction(string Name, IrType ReturnType, ImmutableArray<IrParameter> Parameters);

}
