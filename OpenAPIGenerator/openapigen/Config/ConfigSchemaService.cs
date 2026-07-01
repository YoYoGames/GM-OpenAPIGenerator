using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace openapigen.Config
{
    public sealed class ConfigSchemaService
    {
        private readonly JsonSerializerOptions _options;
        private readonly Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public ConfigSchemaService(JsonSerializerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public string DefaultSchemaFileName { get; init; } = "openapigen.schema.json";

        public string WriteSchemaBesideConfig<TConfig>(string fullConfigPath, string? schemaFileName = null)
        {
            var cfgDir = Path.GetDirectoryName(Path.GetFullPath(fullConfigPath))!;
            Directory.CreateDirectory(cfgDir);
            var schemaName = string.IsNullOrWhiteSpace(schemaFileName) ? DefaultSchemaFileName : schemaFileName!;
            var schemaPath = Path.Combine(cfgDir, schemaName);
            JsonNode schema = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TConfig));
            File.WriteAllText(schemaPath, schema.ToString(), _utf8NoBom);
            return schemaPath;
        }

        public bool EnsureSchemaBesideConfigAndPatchConfigJson<TConfig>(string fullConfigPath, string? schemaFileName = null)
        {
            var schemaName = string.IsNullOrWhiteSpace(schemaFileName) ? DefaultSchemaFileName : schemaFileName!;
            _ = WriteSchemaBesideConfig<TConfig>(fullConfigPath, schemaName);

            var raw = File.ReadAllText(fullConfigPath, Encoding.UTF8);
            JsonNode node = JsonNode.Parse(raw, new JsonNodeOptions { PropertyNameCaseInsensitive = false })
                          ?? throw new JsonException("Config JSON parsed to null.");

            if (node is not JsonObject obj)
                throw new JsonException("Config root must be a JSON object.");

            var desired = $"./{schemaName}";
            var current = obj["$schema"]?.GetValue<string>();

            if (!string.Equals(current, desired, StringComparison.Ordinal))
            {
                obj["$schema"] = desired;
                var patched = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fullConfigPath, patched, _utf8NoBom);
                return true;
            }

            return false;
        }

        public void WriteDefaultConfig<TConfig>(string fullConfigPath, TConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg, _options);
            File.WriteAllText(fullConfigPath, json, _utf8NoBom);
        }
    }
}
