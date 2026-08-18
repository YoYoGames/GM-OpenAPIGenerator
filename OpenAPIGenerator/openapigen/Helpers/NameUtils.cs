using System.Text;
using System.Text.RegularExpressions;

namespace openapigen.Helpers
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



        /// <summary>
        /// True when <paramref name="s"/> can be written as a bare GML identifier. Callers that
        /// emit a name into code must fall back to accessor syntax — <c>self[$ "end"]</c> for a
        /// struct member, a quoted key in a struct literal — whenever this returns false.
        /// </summary>
        public static bool IsValidIdent(string s) => ident.IsMatch(s) && !Reserved.Contains(s);

        /// <summary>
        /// GML keywords and reserved identifiers. Compared case-insensitively: GML keywords are
        /// lowercase, but escaping a differently-cased match is harmless while missing one emits
        /// code that does not compile.
        /// </summary>
        public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            // Control flow
            "if","then","else","do","while","for","switch","case","default","break","continue",
            "return","exit","repeat","until","with",

            // Declarations
            "var","globalvar","static","enum","function","constructor","method","new","delete",
            "begin","end",

            // Exceptions
            "try","catch","finally","throw",

            // Word operators
            "and","or","not","xor","div","mod",

            // Scope / instance keywords
            "self","other","all","noone","global","local",

            // Literals and constants
            "true","false","undefined","infinity","pi","nan",

            // Argument access
            "argument","argument_count","argument0","argument1","argument2","argument3",
            "argument4","argument5","argument6","argument7","argument8","argument9",
            "argument10","argument11","argument12","argument13","argument14","argument15",

            // Built-in variables that are not writable as plain struct members
            "score","room","async_load","event_data",
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
