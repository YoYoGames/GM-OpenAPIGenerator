namespace openapigen.Utils
{
    /// <summary>
    /// Utilities for path resolution and manipulation.
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// Resolves a path relative to a base directory.
        /// Expands environment variables and tilde (~) to the user home directory.
        /// </summary>
        /// <param name="path">Path to resolve (may be relative or absolute).</param>
        /// <param name="baseDir">Base directory for relative paths.</param>
        /// <returns>Fully resolved absolute path, or empty string if input is null/whitespace.</returns>
        public static string ResolvePath(this string? path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            // Only a leading '~' is the home directory, matching the shell convention this imitates.
            // Replacing every '~' mangled legitimate filenames: "my~helpers.gml" became
            // "myC:\Users\<user>helpers.gml".
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var withHome = path switch
            {
                "~" => home,
                ['~', '/' or '\\', ..] => home + path[1..],
                _ => path
            };

            var expanded = Environment.ExpandEnvironmentVariables(withHome);

            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(baseDir, expanded));
        }
    }
}
