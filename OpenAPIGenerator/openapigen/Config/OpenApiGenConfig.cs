using System.Text.Json.Serialization;

namespace openapigen.Config
{
    public sealed class OpenApiGenConfig
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public string Input { get; set; } = "./openapi.json";
        public string Prefix { get; set; } = "gm";

        public GmlOutputConfig Gml { get; set; } = new();
        public DocsOutputConfig Docs { get; set; } = new();
    }

    public sealed class GmlOutputConfig
    {
        public bool Enabled { get; set; } = true;
        public string OutputFolder { get; set; } = "./build";
    }

    public sealed class DocsOutputConfig
    {
        public bool Enabled { get; set; } = false;
        public string OutputFolder { get; set; } = "./build";
    }
}
