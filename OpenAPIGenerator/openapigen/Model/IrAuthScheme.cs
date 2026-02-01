using System.Collections.Immutable;

namespace openapigen.Model
{
    public abstract record IrAuthScheme(string Name)
    {
        public sealed record Basic(string Name) : IrAuthScheme(Name);
        public sealed record Bearer(string Name) : IrAuthScheme(Name);
        public sealed record ApiKey(string Name, string ParamName, IrLocation In) : IrAuthScheme(Name);
        public sealed record OAuth2(string Name, ImmutableArray<string> AllScopes) : IrAuthScheme(Name);
        public sealed record OpenIdConnect(string Name, string Url) : IrAuthScheme(Name);
    }

}
