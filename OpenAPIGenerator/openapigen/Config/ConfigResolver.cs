using openapigen.Models.Config;
using openapigen.Planning;

namespace openapigen.Config
{
    public static class ConfigResolver
    {
        /// <summary>
        /// Resolves the config's relative paths against the config file's own directory and
        /// validates what it can before any generation starts.
        /// </summary>
        public static ResolvedConfig Resolve(OpenApiGenConfig cfg, string configPath, Func<string?, string, string> resolvePath)
        {
            ArgumentNullException.ThrowIfNull(cfg);
            ArgumentNullException.ThrowIfNull(resolvePath);
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("configPath is empty.", nameof(configPath));

            var fullConfigPath = Path.GetFullPath(configPath);
            var baseDir = Path.GetDirectoryName(fullConfigPath)!;

            var inputPath = resolvePath(cfg.Input, baseDir);
            var outputRoot = resolvePath(string.IsNullOrWhiteSpace(cfg.Root) ? "./" : cfg.Root, baseDir);

            if (string.IsNullOrWhiteSpace(inputPath))
                throw new InvalidOperationException("Missing 'input' (OpenAPI spec path) in config.");
            if (!File.Exists(inputPath))
                throw new InvalidOperationException($"Input spec not found: {inputPath}");
            if (string.IsNullOrWhiteSpace(outputRoot))
                throw new InvalidOperationException("Missing 'root' (output directory) in config.");

            // Fail early on a permissions problem rather than midway through generation.
            Directory.CreateDirectory(outputRoot);

            var resolved = new ResolvedConfig(cfg, fullConfigPath, baseDir, inputPath, outputRoot);
            resolved.Validate();
            return resolved;
        }
    }
}
