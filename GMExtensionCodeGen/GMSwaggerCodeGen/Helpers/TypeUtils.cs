using CodeGenCore.Ir;

namespace GMSwaggerCodeGen.Helpers;

internal static class TypeUtils
{
    public static string JsDoc(this IrType t, GmlNaming n) =>
        t.IsCollection
            ? $"Array[{JsDoc(t with { IsCollection = false }, n)}]"
            : t.Kind switch
            {
                IrTypeKind.Scalar when t.IsNumericScalar => "Real",
                IrTypeKind.Scalar when t.IsStringScalar => "String",
                IrTypeKind.Scalar => "Real",
                IrTypeKind.AnyArray => "Array",
                IrTypeKind.AnyMap => "Struct",
                IrTypeKind.Struct => $"Struct.{n.StructPrefix}{t.Name}",
                IrTypeKind.Function => "Function",
                _ => "Any"
            };
}
