using codegencore.Model;
using System.Collections.Immutable;

namespace openapigen.Model
{
    public sealed record IrHttpEndpoint(
        string Name,
        string Verb,
        string PathTemplate,
        ImmutableArray<IrParam> Parameters,
        IrRequestBody? Body,
        IrValueSchema? ResponseSchema,
        IrAuthPolicy Auth,
        string? Summary,
        string? Description,
        ImmutableArray<string> Tags);

}
