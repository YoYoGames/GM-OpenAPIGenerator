using System.Text.Json.Serialization;

namespace openapigen.Models.Config.Controller
{
    /// <summary>
    /// Raw GML event bodies for the controller object that owns the request map and cookie jar.
    /// Point each output at the matching event file of your object, e.g.
    /// <c>./objects/obj_gm_core/Create_0.gml</c>.
    /// </summary>
    public sealed class ControllerConfig
    {
        /// <summary>Create event: converters, auth store, cookie jar, request maps.</summary>
        [JsonPropertyName("createEvent")]
        public CreateEventConfig? CreateEvent { get; set; } = new();

        /// <summary>Clean Up event: destroys the ds_maps allocated in Create.</summary>
        [JsonPropertyName("cleanupEvent")]
        public CleanupEventConfig? CleanupEvent { get; set; } = new();

        /// <summary>Async HTTP event: response dispatch, cookie capture, hooks and callbacks.</summary>
        [JsonPropertyName("httpAsyncEvent")]
        public HttpAsyncEventConfig? HttpAsyncEvent { get; set; } = new();
    }

    /// <summary>Controller Create event.</summary>
    public sealed class CreateEventConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./controller_create.gml";
    }

    /// <summary>Controller Clean Up event.</summary>
    public sealed class CleanupEventConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./controller_cleanup.gml";
    }

    /// <summary>Controller Async HTTP event.</summary>
    public sealed class HttpAsyncEventConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string OutputFile { get; set; } = "./controller_http.gml";
    }
}
