using System.Text.Json.Serialization;

namespace openapigen.Models.Config.Docs
{
    /// <summary>
    /// Feather documentation stubs consumed by the GameMaker extension doc tooling.
    /// </summary>
    public sealed class DocsConfig
    {
        /// <summary>Struct documentation partials.</summary>
        [JsonPropertyName("schemas")]
        public DocsSchemasConfig? Schemas { get; set; } = new() { Enabled = false };

        /// <summary>Endpoint function documentation partials.</summary>
        [JsonPropertyName("functions")]
        public DocsFunctionsConfig? Functions { get; set; } = new() { Enabled = false };
    }

    /// <summary>Schema doc stubs.</summary>
    public sealed class DocsSchemasConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./schemas_codegen.js";
    }

    /// <summary>Endpoint doc stubs.</summary>
    public sealed class DocsFunctionsConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./function_codegen.js";
    }
}
