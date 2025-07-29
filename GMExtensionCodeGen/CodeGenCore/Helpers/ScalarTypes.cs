
namespace CodeGenCore.Helpers
{
    public static class ScalarTypes
    {
        private static readonly HashSet<string> _numeric = new(StringComparer.OrdinalIgnoreCase)
        { "bool", "uint8", "int8", "uint16", "int16", "uint32", "int32", "uint64", "float", "double" };
        private static readonly HashSet<string> _all = new(_numeric, StringComparer.OrdinalIgnoreCase)
        { "string" };

        public static bool IsKnown(string name) => _all.Contains(name);
        public static bool IsNumeric(string name) => _numeric.Contains(name);
    }
}
