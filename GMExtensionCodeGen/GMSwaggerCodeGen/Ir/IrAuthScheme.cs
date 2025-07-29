using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMSwaggerCodeGen.Ir
{
    public abstract record IrAuthScheme(string Name);
    public sealed record IrBasicScheme(string Name) : IrAuthScheme(Name);
    public sealed record IrBearerScheme(string Name) : IrAuthScheme(Name);
    public sealed record IrApiKeyScheme(string Name, string KeyName, IrLocation In) : IrAuthScheme(Name);
    public sealed record IrOAuth2Scheme(string Name, IReadOnlyList<string> Scopes) : IrAuthScheme(Name);
    public sealed record IrOpenIdScheme(string Name, string OpenIdUrl) : IrAuthScheme(Name);

}
