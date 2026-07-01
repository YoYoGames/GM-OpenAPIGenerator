using NDesk.Options;
using openapigen.App;
using openapigen.Config;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace openapigen
{
    public static class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static int Main(string[] args)
        {
            string? configPath = null;
            string? initDir = null;
            string? inputPath = null;
            string outputDir = "build";
            string prefix = "gm";
            bool docs = false;
            bool showHelp = false;

            var options = new OptionSet {
                { "c|config=",  "Path to JSON config file.",                          v => configPath = v },
                { "i|input=",   "OpenAPI spec file (direct mode, no config needed).", v => inputPath  = v },
                { "o|output=",  "Output directory (default: build).",                 v => outputDir  = v },
                { "prefix=",    "Name prefix for generated symbols (default: gm).",   v => prefix     = v },
                { "docs",       "Also emit JSDoc files (direct mode only).",          v => docs       = v != null },
                { "init=",      "Bootstrap a config.json in the given folder.",       v => initDir    = v },
                { "h|help",     "Show this help text.",                               v => showHelp   = v != null },
            };

            try
            {
                var extras = options.Parse(args);

                if (!string.IsNullOrWhiteSpace(initDir))
                {
                    var schemaSvc   = new ConfigSchemaService(JsonOptions);
                    var initializer = new ProjectInitializer(schemaSvc, JsonOptions);
                    return initializer.Init(initDir);
                }

                if (showHelp || (string.IsNullOrWhiteSpace(configPath) && string.IsNullOrWhiteSpace(inputPath)) || extras.Count > 0)
                {
                    ShowUsage(options);
                    return showHelp ? 0 : 1;
                }

                var schemaSvc2 = new ConfigSchemaService(JsonOptions);
                var runner     = new CodegenRunner(JsonOptions, schemaSvc2);

                if (!string.IsNullOrWhiteSpace(configPath))
                    return runner.RunFromConfig(configPath!);

                return runner.RunDirect(inputPath!, outputDir, prefix, docs);
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine(e.Message);
                ShowUsage(options);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 99;
            }
        }

        private static void ShowUsage(OptionSet options)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  openapigen --config <path/to/config.json>");
            Console.WriteLine("  openapigen --input <spec.json> [--output <dir>] [--prefix <name>] [--docs]");
            Console.WriteLine("  openapigen --init <folder>");
            Console.WriteLine();
            options.WriteOptionDescriptions(Console.Out);
        }
    }
}
