using openapigen.Models.Config;
using System.Text;
using System.Text.Json;

namespace openapigen.Config
{
    public sealed class ProjectInitializer(ConfigSchemaService schema, JsonSerializerOptions jsonOptions)
    {
        private readonly ConfigSchemaService _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        private readonly JsonSerializerOptions _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));

        public int Init(string folder, bool force = false, string configFileName = "config.json", string schemaFileName = "openapigen.schema.json")
        {
            try
            {
                var outDir = Path.GetFullPath(folder);
                Directory.CreateDirectory(outDir);

                var configPath = Path.Combine(outDir, configFileName);
                var schemaPath = Path.Combine(outDir, schemaFileName);

                // The config carries the user's own settings; the schema beside it is derived output
                // with nothing to lose, so that one is always refreshed.
                if (File.Exists(configPath) && !force)
                {
                    Console.Error.WriteLine(
                        $"'{configFileName}' already exists in {outDir}. " +
                        "Re-run with --force to overwrite it.");
                    return 98;
                }

                _ = _schema.WriteSchemaBesideConfig<OpenApiGenConfig>(configPath, schemaFileName);

                var cfg = new OpenApiGenConfig
                {
                    Schema = $"./{schemaFileName}",
                    Root = "./"
                };

                var json = JsonSerializer.Serialize(cfg, _jsonOptions);
                File.WriteAllText(configPath, json, new UTF8Encoding(false));

                Console.WriteLine($"[openapigen] Wrote: {configPath}");
                Console.WriteLine($"[openapigen] Wrote: {schemaPath}");
                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine($"  1. Edit '{configFileName}' — set \"input\" to your OpenAPI spec path.");
                Console.WriteLine($"  2. Run: openapigen --config {configPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 98;
            }
        }
    }
}
