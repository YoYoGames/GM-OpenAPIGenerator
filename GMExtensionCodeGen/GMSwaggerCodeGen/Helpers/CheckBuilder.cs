using CodeGenCore.Helpers;
using CodeGenCore.Ir;
using CodeGenCore.Writers;
using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.CStyle;

namespace GMSwaggerCodeGen.Helpers
{
    internal static class CheckBuilder
    {
        public static void Emit(ICodeWriter w, string id, IrType t, bool required, GmlNaming n, string where = "_where")
        {
            string pred = t switch
            {
                _ when t.IsCollection => t.FixedLength is null ? $"!is_array({id})" : $"(!is_array({id}) || array_length({id}) != {t.FixedLength})",

                { Kind: IrTypeKind.Scalar, IsStringScalar: true } => $"!is_string({id})",
                { Kind: IrTypeKind.Scalar } => $"!is_real({id})",
                { Kind: IrTypeKind.Function } => $"!is_callable({id})",
                { Kind: IrTypeKind.AnyMap } => $"!is_struct({id})",

                { Kind: IrTypeKind.Struct } =>
                    $"!is_struct({id}) || {id}[$ \"__uid\"] != {StringHash.ToUInt32($"{n.StructPrefix}{t.Name}")}",

                _ => string.Empty
            };

            bool isStruct = t.Kind == IrTypeKind.Struct && !t.IsCollection;

            if (pred == string.Empty) return;

            if (required)
            {
                w.Line($"if ({pred}) show_error($\"{{{where}}} :: {id.Replace("\"", "'")} expected {Display(t, n)}\", true);");
                if (isStruct) w.Line($"{id}.validate({where});");
            }
            else
            {
                if (isStruct)
                {
                    w.If($"!is_undefined({id})", body =>
                    {
                        body.Line($"if ({pred}) show_error($\"{{{where}}} :: {id.Replace("\"", "'")} expected {Display(t, n)}\", true);");
                        if (isStruct) body.Line($"{id}.validate({where});");
                    });
                }
                else 
                {
                    w.Line($"if (!is_undefined({id}) && {pred}) show_error($\"{{{where}}} :: {id.Replace("\"", "'")} expected {Display(t, n)}\", true);");
                }
            }
        }

        private static string Display(IrType t, GmlNaming n) =>
            t.Kind == IrTypeKind.Struct ? $"{n.StructPrefix}{t.Name}" : t.Name;
    }

}
