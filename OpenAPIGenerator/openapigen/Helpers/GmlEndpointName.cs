using System.Text;
using System.Text.RegularExpressions;

namespace GMSwaggerCodeGen.Emitters.Gml
{
    public static partial class GmlEndpointName
    {
        private static readonly Regex pathParamRegex = PathParamRegex();

        public static string Make(
            string? tag,
            string verb,
            string pathTemplate,
            ISet<string>? usedNames = null)
        {
            var prefix = ToSnake(tag);
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "default";

            var verbToken = ToSnake(verb); // "GET" -> "get"

            // Split path
            var segments = pathTemplate
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            // Drop leading segment if it matches the tag prefix
            if (segments.Count > 0 && SameToken(segments[0], prefix))
                segments.RemoveAt(0);

            // Decide if we need _by_id: only when the LAST segment is a path param
            bool endsWithParam = segments.Count > 0 && IsParam(segments[^1]);

            // Collect literal tokens (ignore all params)
            var literals = segments
                .Where(s => !IsParam(s))
                .Select(ToSnake)
                .Where(s => s.Length > 0)
                .ToList();

            // If nothing literal remains, keep a stable token
            var middle = literals.Count > 0 ? $"_{string.Join("_", literals)}" : string.Empty;

            var baseName = $"{prefix}_{verbToken}{middle}";
            var name = endsWithParam ? $"{baseName}_by_id" : baseName;

            // GML-friendly start char
            if (!(char.IsLetter(name[0]) || name[0] == '_'))
                name = "_" + name;

            // Uniqueness with numeric suffixes (no hashes)
            if (usedNames is null) return name;
            if (usedNames.Add(name)) return name;

            int i = 2;
            string candidate;
            do candidate = $"{name}_{i++}";
            while (!usedNames.Add(candidate));

            return candidate;
        }

        private static bool IsParam(string segment) => pathParamRegex.IsMatch(segment);

        private static bool SameToken(string segment, string token)
            => string.Equals(ToSnake(segment), ToSnake(token), StringComparison.OrdinalIgnoreCase);

        // "walletId", "auth-scheme", "OAuth2" -> "wallet_id", "auth_scheme", "oauth2"
        private static string ToSnake(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            s = s.Trim()
                 .Replace("-", "_")
                 .Replace(".", "_")
                 .Replace(" ", "_");

            var sb = new StringBuilder(s.Length * 2);
            char? prev = null;

            foreach (var ch in s)
            {
                if (prev is not null)
                {
                    var p = prev.Value;
                    if ((char.IsLower(p) && char.IsUpper(ch)) ||
                        (char.IsLetter(p) && char.IsDigit(ch)) ||
                        (char.IsDigit(p) && char.IsLetter(ch)))
                    {
                        if (sb.Length > 0 && sb[^1] != '_')
                            sb.Append('_');
                    }
                }

                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
                prev = ch;
            }

            var token = Regex.Replace(sb.ToString(), "_{2,}", "_")
                             .Trim('_')
                             .ToLowerInvariant();

            return token;
        }

        [GeneratedRegex(@"^\{[^}]+\}$", RegexOptions.Compiled)]
        private static partial Regex PathParamRegex();
    }
}

