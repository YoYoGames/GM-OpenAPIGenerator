using openapigen.Emitters.Docs;
using openapigen.Emitters.Gml;
using openapigen.Helpers;
using openapigen.Config;
using openapigen.Model;
using openapigen.Parsing.OpenApi;
using System.Text;
using System.Text.Json;

namespace openapigen.App
{
    public sealed class CodegenRunner
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConfigSchemaService _schema;

        public CodegenRunner(JsonSerializerOptions jsonOptions, ConfigSchemaService schema)
        {
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public int RunFromConfig(string configPath)
        {
            var fullConfigPath = Path.GetFullPath(configPath);
            if (!File.Exists(fullConfigPath))
            {
                Console.Error.WriteLine($"Config file not found: {fullConfigPath}");
                return 3;
            }

            try
            {
                var modified = _schema.EnsureSchemaBesideConfigAndPatchConfigJson<OpenApiGenConfig>(fullConfigPath);
                if (modified)
                    Console.WriteLine("[openapigen] Updated config '$schema' to the latest schema.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write schema/patch config: {ex.Message}");
                return 5;
            }

            OpenApiGenConfig cfg;
            try
            {
                var json = File.ReadAllText(fullConfigPath, Encoding.UTF8);
                cfg = JsonSerializer.Deserialize<OpenApiGenConfig>(json, _jsonOptions)
                      ?? throw new JsonException("Empty or invalid configuration.");
            }
            catch (JsonException je)
            {
                Console.Error.WriteLine($"Config JSON error: {je.Message}");
                return 5;
            }

            var cfgDir = Path.GetDirectoryName(fullConfigPath)!;
            var inputPath = Path.GetFullPath(Path.Combine(cfgDir, cfg.Input));

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Input spec not found: {inputPath}");
                return 3;
            }

            IrWebCompilation ir;
            try
            {
                ir = OpenApiSchemaLoader.LoadFromFile(inputPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to parse spec: {ex.Message}");
                return 6;
            }

            var naming = new GmlNaming(cfg.Prefix);

            if (cfg.Gml.Enabled)
            {
                var outDir = Path.GetFullPath(Path.Combine(cfgDir, cfg.Gml.OutputFolder));
                Console.WriteLine($"[openapigen] GML -> {outDir}");
                try
                {
                    new HttpGmlEmitter(naming).Emit(ir, outDir);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[GML] Failed: {ex.Message}");
                    return 30;
                }
            }

            if (cfg.Docs.Enabled)
            {
                var outDir = Path.GetFullPath(Path.Combine(cfgDir, cfg.Docs.OutputFolder));
                Console.WriteLine($"[openapigen] Docs -> {outDir}");
                try
                {
                    new DocsEmitter(naming).Emit(ir, outDir);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Docs] Failed: {ex.Message}");
                    return 30;
                }
            }

            Console.WriteLine("[openapigen] Success [x]");
            return 0;
        }

        public int RunDirect(string inputPath, string outputDir, string prefix, bool emitDocs)
        {
            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Input spec not found: {inputPath}");
                return 3;
            }

            IrWebCompilation ir;
            try
            {
                ir = OpenApiSchemaLoader.LoadFromFile(inputPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to parse spec: {ex.Message}");
                return 6;
            }

            var naming = new GmlNaming(prefix);
            var outDir = Path.GetFullPath(outputDir);

            Console.WriteLine($"[openapigen] GML -> {outDir}");
            try
            {
                new HttpGmlEmitter(naming).Emit(ir, outDir);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GML] Failed: {ex.Message}");
                return 30;
            }

            if (emitDocs)
            {
                Console.WriteLine($"[openapigen] Docs -> {outDir}");
                try
                {
                    new DocsEmitter(naming).Emit(ir, outDir);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Docs] Failed: {ex.Message}");
                    return 30;
                }
            }

            Console.WriteLine("[openapigen] Success [x]");
            return 0;
        }
    }
}
