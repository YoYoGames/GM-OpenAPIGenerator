using codegencore.Model;
using System.Collections.Immutable;

namespace openapigen.Model
{
    public sealed record IrRequestBody(
        ImmutableArray<string> MediaTypes,
        IrValueSchema Schema,
        bool Required);

}
