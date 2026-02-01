using System.Collections.Immutable;

namespace openapigen.Model
{
    public abstract record IrAuthRequirement
    {
        public sealed record None : IrAuthRequirement; // explicitly no auth
        public sealed record Scheme(string SchemeName, ImmutableArray<string> Scopes) : IrAuthRequirement;
    }

}
