using openapigen.Models.Config.Code;
using openapigen.Models.Config.Controller;
using openapigen.Models.Config.Docs;
using System.Text.Json.Serialization;

namespace openapigen.Models.Config
{
    /// <summary>
    /// Root configuration for the OpenAPI GML generator.
    /// </summary>
    public sealed class OpenApiGenConfig
    {
        /// <summary>JSON schema URI for editor validation.</summary>
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        /// <summary>OpenAPI 3.x specification file (JSON or YAML), relative to this config.</summary>
        [JsonPropertyName("input")]
        public string? Input { get; set; } = "./openapi.json";

        /// <summary>Base directory every <c>outputFile</c> resolves against, relative to this config.</summary>
        [JsonPropertyName("root")]
        public string? Root { get; set; } = "./";

        /// <summary>Namespace prefix for generated symbols; GML has no namespaces.</summary>
        [JsonPropertyName("prefix")]
        public string Prefix { get; set; } = "gm";

        /// <summary>
        /// Require every operation to declare an <c>operationId</c> (default: true).
        ///
        /// Generated function names are permanent public API. <c>operationId</c> is the only
        /// author-controlled, stable source for them - names derived from the URL change whenever
        /// the path is refactored, and they collide. Set this to false only for a third-party spec
        /// you cannot edit; names then fall back to the path/verb/tag derivation.
        /// </summary>
        [JsonPropertyName("requireOperationId")]
        public bool RequireOperationId { get; set; } = true;

        /// <summary>Generated GML client code.</summary>
        [JsonPropertyName("code")]
        public CodeConfig Code { get; set; } = new();

        /// <summary>Controller object event bodies.</summary>
        [JsonPropertyName("controller")]
        public ControllerConfig Controller { get; set; } = new();

        /// <summary>Feather documentation stubs.</summary>
        [JsonPropertyName("docs")]
        public DocsConfig Docs { get; set; } = new();
    }
}
