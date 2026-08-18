using System.Text.Json.Serialization;

namespace openapigen.Models.Config.Code
{
    /// <summary>
    /// The generated GML client code: endpoint wrappers, schema structs and internal helpers.
    /// </summary>
    public sealed class CodeConfig
    {
        /// <summary>One wrapper function per OpenAPI operation.</summary>
        [JsonPropertyName("endPoints")]
        public EndPointsConfig? EndPoints { get; set; } = new();

        /// <summary>Constructors and validators for the component schemas.</summary>
        [JsonPropertyName("schemas")]
        public SchemasConfig? Schemas { get; set; } = new();

        /// <summary>Request struct, auth, cookie jar and body converters.</summary>
        [JsonPropertyName("helpers")]
        public HelpersConfig? Helpers { get; set; } = new();
    }

    /// <summary>Endpoint wrapper functions.</summary>
    public sealed class EndPointsConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./generated_http.gml";
    }

    /// <summary>Schema constructors and validators.</summary>
    public sealed class SchemasConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./generated_schemas.gml";
    }

    /// <summary>Internal runtime helpers.</summary>
    public sealed class HelpersConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./generated_helpers.gml";
    }
}
