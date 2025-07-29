using CodeGenCore.Ir;

namespace GMSwaggerCodeGen.Ir
{
    public sealed record IrParam(
        string Name,
        IrType Type,
        IrLocation Location,
        bool Required, 
        string? Default = null,
        string? Description = null);
}
