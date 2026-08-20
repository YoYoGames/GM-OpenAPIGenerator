using openapigen.Models.Config;
using openapigen.Utils;

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
            var enabled = Enumerate().Where(o => o.Config.Enabled).ToList();

            foreach (var (key, cfg) in enabled)
            {
                if (string.IsNullOrWhiteSpace(cfg.OutputFile))
                    throw new InvalidOperationException(
                        $"Output '{key}' is enabled but its 'outputFile' is empty.");
            }

            // Two outputs writing to one file is silent data loss: whichever emitter runs second
            // wins, and the order is an implementation detail of EmitterBuilder.
            var destinations = enabled
                .Select(o => (o.Key, Path: o.Config.OutputFile.ResolvePath(OutputRoot)))
                .ToList();

            var clash = destinations
                .GroupBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (clash is not null)
                throw new InvalidOperationException(
                    $"Outputs {string.Join(" and ", clash.Select(d => $"'{d.Key}'"))} both resolve to " +
                    $"'{clash.Key}'. Each output needs its own file - one would silently overwrite the other.");

            // Escaping 'root' is suspicious rather than wrong: pointing root at a .yyp while sending
            // docs to a sibling folder is a reasonable layout. Say it out loud and continue.
            foreach (var (key, path) in destinations)
            {
                if (Path.GetRelativePath(OutputRoot, path).StartsWith("..", StringComparison.Ordinal))
                    Console.Error.WriteLine(
                        $"[openapigen] warning: output '{key}' resolves outside 'root': {path}");
            }
        }

        private IEnumerable<(string Key, IGeneratorConfig Config)> Enumerate()
        {
            if (Raw.Code.EndPoints is { } a) yield return ("code.endPoints", a);
            if (Raw.Code.Schemas is { } b) yield return ("code.schemas", b);
            if (Raw.Code.Helpers is { } c) yield return ("code.helpers", c);
            if (Raw.Controller.CreateEvent is { } d) yield return ("controller.createEvent", d);
            if (Raw.Controller.CleanupEvent is { } e) yield return ("controller.cleanupEvent", e);
            if (Raw.Controller.HttpAsyncEvent is { } f) yield return ("controller.httpAsyncEvent", f);
            if (Raw.Docs.Schemas is { } g) yield return ("docs.schemas", g);
            if (Raw.Docs.Functions is { } h) yield return ("docs.functions", h);
            if (Raw.Docs.Modules is { } i) yield return ("docs.modules", i);
        }
    }
}
