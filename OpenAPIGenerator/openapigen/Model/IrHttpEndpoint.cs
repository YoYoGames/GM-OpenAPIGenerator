using codegencore.Model;
using System.Collections.Immutable;

namespace openapigen.Model
{
    public sealed record IrHttpEndpoint(
        string Name,
        /// <summary>The spec's operationId, or null when the operation declared none.</summary>
        string? OperationId,
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
