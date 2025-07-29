using System.Collections.Immutable;

namespace CodeGenCore.Ir
{
    public sealed record IrStruct(string Name, ImmutableArray<IrField> Fields, string? Description = null);

}
