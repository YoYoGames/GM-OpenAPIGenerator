namespace CodeGenCore.Ir
{
    public sealed record IrField(
        string Name,
        IrType Type,
        string? DefaultLiteral,
        bool Required,
        string? Description = null);

}
