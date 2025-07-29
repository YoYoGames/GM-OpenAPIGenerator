namespace GMSwaggerCodeGen.Ir
{
    public abstract record IrAuthRequirement(string Name);

    public sealed record IrNoAuth() : IrAuthRequirement("");

    public sealed record IrBasicAuth(string Name) : IrAuthRequirement(Name);
    public sealed record IrBearerAuth(string Name) : IrAuthRequirement(Name);

    public sealed record IrApiKeyAuth(string Name, IrLocation In) : IrAuthRequirement(Name);

    public sealed record IrOAuth2Auth(string Name, IReadOnlyList<string> Scopes) : IrAuthRequirement(Name);
}
