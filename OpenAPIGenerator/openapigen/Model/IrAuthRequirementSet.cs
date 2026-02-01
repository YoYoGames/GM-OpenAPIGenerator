using System.Collections.Immutable;

namespace openapigen.Model
{
    public sealed record IrAuthRequirementSet(
        ImmutableArray<IrAuthRequirement> Requirements);

}
