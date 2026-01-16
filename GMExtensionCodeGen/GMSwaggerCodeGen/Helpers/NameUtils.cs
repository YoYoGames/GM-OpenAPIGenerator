using System.Text;
using System.Text.RegularExpressions;

namespace GMSwaggerCodeGen.Helpers
{
    public static partial class NameUtils
    {
        private static readonly Regex _var = MatchPathParameter();
        private static readonly Regex ident = MatchGmlIdentifier();

        // Case-insensitive exceptions
        public static readonly string[] Exceptions =
        {
            "OAuth2",
            "OAuth",
            "iPhone",
            "iOS",
            "eBay",
            "GitHub"
        };

        private static readonly Regex caseBoundary = MatchCaseBoundary();
        private static readonly Regex acronymBoundary = MatchAcronymBoundary();
        private static readonly Regex clean = Clean();

        public static string ToSnake(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // 1. Protect exceptions
            var protectedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string working = input;

            for (int i = 0; i < Exceptions.Length; i++)
            {
                string token = $"@@{i}@@";
                protectedMap[token] = $"_{Exceptions[i].ToLowerInvariant()}_";

                working = Regex.Replace(
                    working,
                    Regex.Escape(Exceptions[i]),
                    token,
                    RegexOptions.IgnoreCase);
            }

            // 2. Normal snake_case conversion
            working = acronymBoundary.Replace(working, "$1_$2");
            working = caseBoundary.Replace(working, "$1_$2");

            // 3. Restore exceptions
            foreach (var kv in protectedMap)
                working = working.Replace(kv.Key, kv.Value);

            // 4. Clean everything up
            working = clean.Replace(working, "_");

            return working.Trim('_').ToLowerInvariant();
        }



        public static bool IsValidIdent(string s) => ident.IsMatch(s) && !Reserved.Contains(s);

        public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            // Keywords
            "if","else","do","while","for","switch","case","break","continue","return","exit",
            "repeat","until","var","globalvar","static","enum","try","catch","finally","with",
            "function","new","delete","begin","end","method",

            // Constants
            "true","false","undefined","infinity","pi",

            // Globals
            "global","score", "room",
        };


        public static string ParamName(string raw) => "_" + ToSnake(raw);

        public static string CleanUrlPart(string pathTemplate)
        {
            var noVars = _var.Replace(pathTemplate, string.Empty).Trim('/');
            return ToSnake(noVars);
        }

        public static string EndpointFuncName(string operationId, string? group)
        {
            var groupPart = group is null ? string.Empty : $"{ToSnake(group)}_";
            return $"{groupPart}{ToSnake(operationId)}";
        }

        [GeneratedRegex(@"\{[^}]+\}", RegexOptions.Compiled)]
        private static partial Regex MatchPathParameter();
        [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled)]
        private static partial Regex MatchGmlIdentifier();
        [GeneratedRegex(@"([a-z0-9])([A-Z])", RegexOptions.Compiled)]
        private static partial Regex MatchCaseBoundary();
        [GeneratedRegex(@"([A-Z]{2,})([A-Z][a-z])", RegexOptions.Compiled)]
        private static partial Regex MatchAcronymBoundary();
        [GeneratedRegex(@"[^A-Za-z0-9]+", RegexOptions.Compiled)]
        private static partial Regex Clean();
    }
}
