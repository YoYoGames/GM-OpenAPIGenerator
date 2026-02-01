using System.Collections.Immutable;

namespace openapigen.Model
{
    public sealed record IrWebCompilation(
    ImmutableArray<IrHttpEndpoint> Endpoints,
    ImmutableArray<IrSchema> Schemas,
    ImmutableArray<IrAuthScheme> AuthSchemes);

}
