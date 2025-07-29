using System.Text.RegularExpressions;

namespace GMSwaggerCodeGen.Helpers
{
    public static partial class NameUtils
    {
        private static readonly Regex _var = MatchPathParameter();
        private static readonly Regex snake = MatchCaseSwitch();
        private static readonly Regex clean = MatchIllegalCharacters();
        private static readonly Regex ident = MatchGmlIdentifier();

        public static string ToSnake(string s) => clean.Replace(snake.Replace(s, "$1_$2"), "_").Trim('_').ToLowerInvariant();

        public static bool IsValidIdent(string s) => ident.IsMatch(s) && !Reserved.Contains(s);

        public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            "if","else","do","while","for","switch","case","break","continue","return","exit",
            "repeat","until","var","globalvar","static","enum","try","catch","with","in","of",
            "true","false","undefined","infinity","nan","function","new","delete","begin","end","method"
        };

        public static string ParamName(string raw) => "_" + ToSnake(raw);

        public static string CleanUrlPart(string pathTemplate)
        {
            var noVars = _var.Replace(pathTemplate, string.Empty).Trim('/');
            return ToSnake(noVars);
        }

        public static string EndpointFuncName(string operationId)
        {
            return ToSnake(operationId);
        }

        [GeneratedRegex(@"([a-z0-9])([A-Z])", RegexOptions.Compiled)]
        private static partial Regex MatchCaseSwitch();
        [GeneratedRegex(@"[^a-zA-Z0-9]+", RegexOptions.Compiled)]
        private static partial Regex MatchIllegalCharacters();
        [GeneratedRegex(@"\{[^}]+\}", RegexOptions.Compiled)]
        private static partial Regex MatchPathParameter();
        [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled)]
        private static partial Regex MatchGmlIdentifier();
    }
}
