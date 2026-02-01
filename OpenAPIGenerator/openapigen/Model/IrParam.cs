using codegencore.Model;

namespace openapigen.Model
{
    public sealed record IrParam(
        string Name,
        IrValueSchema Schema,
        IrLocation Location,
        bool Required,
        string? DefaultLiteral,
        string? Description);

}
