using CodeGenCore.Writers;

namespace GMSwaggerCodeGen.Helpers;

internal static class WriterExt
{
    public static ICodeWriter FieldAssign(this ICodeWriter io, string lhs, string rhs)
        => io.AppendLine($"{lhs} = {rhs};");
}
