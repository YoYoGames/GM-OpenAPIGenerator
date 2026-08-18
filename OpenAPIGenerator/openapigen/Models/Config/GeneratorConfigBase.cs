using System.Text.Json.Serialization;

namespace openapigen.Models.Config
{
    /// <summary>
    /// Base class for a single generated output: an on/off switch plus a destination file.
    /// </summary>
    public abstract class GeneratorConfigBase : IGeneratorConfig
    {
        /// <inheritdoc />
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public abstract string OutputFile { get; set; }
    }
}
