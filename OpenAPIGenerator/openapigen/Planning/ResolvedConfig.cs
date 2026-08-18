using openapigen.Models.Config;

namespace openapigen.Planning
{
    /// <summary>
    /// A validated configuration with every path resolved to an absolute location.
    /// </summary>
    public sealed class ResolvedConfig
    {
        public OpenApiGenConfig Raw { get; }
        public string ConfigPath { get; }
        public string BaseDir { get; }
        public string InputPath { get; }
        public string OutputRoot { get; }

        public string Prefix => string.IsNullOrWhiteSpace(Raw.Prefix) ? "gm" : Raw.Prefix.Trim();

        public ResolvedConfig(OpenApiGenConfig raw, string configPath, string baseDir, string inputPath, string outputRoot)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            ConfigPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            BaseDir = baseDir ?? throw new ArgumentNullException(nameof(baseDir));
            InputPath = inputPath ?? throw new ArgumentNullException(nameof(inputPath));
            OutputRoot = outputRoot ?? throw new ArgumentNullException(nameof(outputRoot));
        }

        public void Validate()
        {
            foreach (var c in Enumerate())
            {
                if (c.Enabled && string.IsNullOrWhiteSpace(c.OutputFile))
                    throw new InvalidOperationException(
                        $"An enabled output in the config has an empty 'outputFile'.");
            }
        }

        private IEnumerable<IGeneratorConfig> Enumerate()
        {
            if (Raw.Code.EndPoints is { } a) yield return a;
            if (Raw.Code.Schemas is { } b) yield return b;
            if (Raw.Code.Helpers is { } c) yield return c;
            if (Raw.Controller.CreateEvent is { } d) yield return d;
            if (Raw.Controller.CleanupEvent is { } e) yield return e;
            if (Raw.Controller.HttpAsyncEvent is { } f) yield return f;
            if (Raw.Docs.Schemas is { } g) yield return g;
            if (Raw.Docs.Functions is { } h) yield return h;
        }
    }
}
