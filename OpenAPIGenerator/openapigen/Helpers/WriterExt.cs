using codegencore.Writers.Lang;

namespace openapigen.Helpers;

internal static class WriterExt
{
    public static GmlWriter FieldAssign(this GmlWriter w, string lhs, string rhs)
        => w.Line($"{lhs} = {rhs};");
}
