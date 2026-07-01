using System.Text;
using System.Text.Json;

namespace openapigen.Config
{
    public sealed class ProjectInitializer(ConfigSchemaService schema, JsonSerializerOptions jsonOptions)
    {
        private readonly ConfigSchemaService _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        private readonly JsonSerializerOptions _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));

        public int Init(string folder, string configFileName = "config.json", string schemaFileName = "openapigen.schema.json")
        {
            try
            {
                var outDir = Path.GetFullPath(folder);
                Directory.CreateDirectory(outDir);

                var configPath = Path.Combine(outDir, configFileName);
                var schemaPath = Path.Combine(outDir, schemaFileName);

                _ = _schema.WriteSchemaBesideConfig<OpenApiGenConfig>(configPath, schemaFileName);

                var cfg = new OpenApiGenConfig { Schema = $"./{schemaFileName}" };
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
