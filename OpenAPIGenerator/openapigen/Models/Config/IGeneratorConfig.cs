using System.Text.Json.Serialization;

namespace openapigen.Models.Config
{
    /// <summary>
    /// Base interface for a single generated output.
    /// </summary>
    public interface IGeneratorConfig
    {
        /// <summary>Whether this output is generated.</summary>
        [JsonPropertyName("enabled")]
        bool Enabled { get; set; }

        /// <summary>Output file path, resolved relative to the config's <c>root</c>.</summary>
        [JsonPropertyName("outputFile")]
        string OutputFile { get; set; }
    }
}
