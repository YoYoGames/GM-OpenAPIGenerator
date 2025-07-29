using CodeGenCore.Ir;
using System;
using System.Collections.Immutable;

namespace GMSwaggerCodeGen.Ir
{
    public sealed record IrWebCompilation(ImmutableArray<IrHttpEndpoint> Endpoints, ImmutableArray<IrStruct> Structs, ImmutableArray<IrAuthScheme> AuthSchemes);
}
