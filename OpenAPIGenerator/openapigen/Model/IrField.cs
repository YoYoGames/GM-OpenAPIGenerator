using codegencore.Model;

namespace openapigen.Model
{
    public sealed record IrField(
        string Name,
        IrValueSchema Schema,
        bool Required,
        bool ReadOnly,
        bool WriteOnly,
        string? DefaultLiteral,
        string? Description);

}
