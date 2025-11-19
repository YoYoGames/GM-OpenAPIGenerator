using CodeGenCore.Ir;
using System.Collections.Immutable;

namespace GMSwaggerCodeGen.Ir
{
    public sealed record IrHttpEndpoint(
        string Name,          // e.g. get_character
        string Verb,          // GET, POST …
        string PathTemplate,  // "/characters/{id}"
        ImmutableArray<IrParam> Parameters,
        IrRequestBody? Body,          // optional body spec
        IrType? ResponseSchema,
        ImmutableArray<IrAuthRequirement> Auth,
        string? Summary = null,   // operation.summary
        string? Description = null);  // operation.description
}
