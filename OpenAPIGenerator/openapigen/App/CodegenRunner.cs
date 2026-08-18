using openapigen.Config;
using openapigen.Models.Config;
using openapigen.Model;
using openapigen.Parsing.OpenApi;
using openapigen.Planning;
using openapigen.Utils;
using System.Text;
using System.Text.Json;

namespace openapigen.App
{
    /// <summary>
    /// Orchestrates generation, from configuration through to emitter execution.
    /// </summary>
    public sealed class CodegenRunner
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConfigSchemaService _schema;

        public CodegenRunner(JsonSerializerOptions jsonOptions, ConfigSchemaService schema)
        {
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        /// <summary>
        /// Runs the pipeline for a config file.
        /// </summary>
        /// <returns>0 on success; a non-zero exit code describing the failing stage otherwise.</returns>
        public int RunFromConfig(string configPath)
        {
            // 1. Locate config
            // 2. Refresh the JSON schema beside it (editor autocomplete) and patch $schema
            // 3. Load and resolve config
            // 4. Parse the OpenAPI spec into IR
            // 5. Run each enabled emitter

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

            ResolvedConfig rc;
            try
            {
                rc = ConfigResolver.Resolve(cfg, fullConfigPath, PathUtils.ResolvePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 5;
            }

            IrWebCompilation ir;
            try
            {
                ir = OpenApiSchemaLoader.LoadFromFile(rc.InputPath, rc.Raw.RequireOperationId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to parse spec: {ex.Message}");
                return 6;
            }

            var emitters = EmitterBuilder.Build(rc);

            if (emitters.Count == 0)
            {
                Console.WriteLine("[openapigen] No outputs enabled in config. Nothing to generate.");
                return 0;
            }

            foreach (var (key, emitter) in emitters)
            {
                try
                {
                    emitter.Emit(ir, rc.OutputRoot);
                    Console.WriteLine($"[openapigen] {key} -> {Describe(rc, key)}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{key}] Failed: {ex.Message}");
                    return 30;
                }
            }

            Console.WriteLine("[openapigen] Success [x]");
            return 0;
        }

        private static string Describe(ResolvedConfig rc, string key)
        {
            var file = key switch
            {
                "schemas" => rc.Raw.Code.Schemas?.OutputFile,
                "endPoints" => rc.Raw.Code.EndPoints?.OutputFile,
                "helpers" => rc.Raw.Code.Helpers?.OutputFile,
                "controller.createEvent" => rc.Raw.Controller.CreateEvent?.OutputFile,
                "controller.cleanupEvent" => rc.Raw.Controller.CleanupEvent?.OutputFile,
                "controller.httpAsyncEvent" => rc.Raw.Controller.HttpAsyncEvent?.OutputFile,
                "docs.schemas" => rc.Raw.Docs.Schemas?.OutputFile,
                "docs.functions" => rc.Raw.Docs.Functions?.OutputFile,
                _ => null
            };

            return file.ResolvePath(rc.OutputRoot);
        }
    }
}
