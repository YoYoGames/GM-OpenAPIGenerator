using codegencore.Model;
using openapigen.Model;

namespace openapigen.Helpers
{
    /// <summary>What role an argument plays in a generated endpoint wrapper.</summary>
    internal enum EndpointArgKind
    {
        /// <summary>A path, query or header parameter from the spec.</summary>
        Parameter,
        /// <summary>The request body.</summary>
        Body,
        /// <summary>The selected media type, emitted only when the body allows several.</summary>
        ContentType,
        /// <summary>The completion callback.</summary>
        Callback
    }

    /// <summary>
    /// One argument of a generated endpoint wrapper, already named and ordered.
    /// </summary>
    /// <param name="Name">GML argument identifier, including the leading underscore.</param>
    /// <param name="SpecName">Original name from the spec, used in error messages and query keys.</param>
    /// <param name="Schema">Value schema, or null for arguments with a fixed type.</param>
    /// <param name="Required">Whether the argument must be supplied.</param>
    /// <param name="DefaultLiteral">GML literal used as the default, or null for <c>undefined</c>.</param>
    internal sealed record EndpointArg(
        string Name,
        string SpecName,
        IrValueSchema? Schema,
        bool Required,
        string? DefaultLiteral,
        string? Description,
        EndpointArgKind Kind,
        IrLocation? Location);

    /// <summary>
    /// Single source of truth for an endpoint's generated signature.
    ///
    /// Both the GML emitter and the docs emitter build from this, so the documented signature can
    /// never drift from the emitted one (they previously disagreed about cookie parameters).
    /// </summary>
    internal static class EndpointSignature
    {
        /// <summary>Argument names the generator owns; a spec parameter may not take them.</summary>
        private static readonly string[] ReservedArgNames = ["_body", "_content_type", "_callback"];

        public const string BodyArg = "_body";
        public const string ContentTypeArg = "_content_type";
        public const string CallbackArg = "_callback";

        /// <summary>
        /// Builds the ordered argument list: required parameters first, then optional ones, then
        /// body / content-type / callback.
        /// </summary>
        /// <remarks>
        /// Cookie parameters are deliberately excluded - the generated cookie jar captures and
        /// injects cookies automatically, so exposing them as arguments would be misleading.
        /// </remarks>
        public static List<EndpointArg> Build(IrHttpEndpoint ep)
        {
            var args = new List<EndpointArg>();
            var used = new HashSet<string>(ReservedArgNames, StringComparer.Ordinal);

            var ordered = ep.Parameters
                .Where(p => p.Location != IrLocation.Cookie)
                .OrderByDescending(p => p.Required)
                .ToList();

            foreach (var p in ordered)
            {
                var name = Unique(NameUtils.ParamName(p.Name), used);

                args.Add(new EndpointArg(
                    Name: name,
                    SpecName: p.Name,
                    Schema: p.Schema,
                    Required: p.Required,
                    DefaultLiteral: p.Required ? null : GmlLiteral.For(p.DefaultLiteral, p.Schema),
                    Description: p.Description,
                    Kind: EndpointArgKind.Parameter,
                    Location: p.Location));
            }

            if (ep.Body is not null)
            {
                args.Add(new EndpointArg(
                    Name: BodyArg,
                    SpecName: BodyArg,
                    Schema: ep.Body.Schema,
                    // Always defaulted: the body follows optional parameters in the signature, so
                    // GML cannot express it as mandatory. Validation still enforces requiredness.
                    Required: ep.Body.Required,
                    DefaultLiteral: null,
                    Description: "The body to be included in the http request.",
                    Kind: EndpointArgKind.Body,
                    Location: null));

                if (ep.Body.MediaTypes.Length > 1)
                {
                    args.Add(new EndpointArg(
                        Name: ContentTypeArg,
                        SpecName: ContentTypeArg,
                        Schema: new IrValueSchema.Simple(IrType.String),
                        Required: false,
                        DefaultLiteral: $"\"{ep.Body.MediaTypes[0]}\"",
                        Description: "The content-type used by the body converter.",
                        Kind: EndpointArgKind.ContentType,
                        Location: null));
                }
            }

            args.Add(new EndpointArg(
                Name: CallbackArg,
                SpecName: CallbackArg,
                Schema: new IrValueSchema.Simple(IrType.Function),
                Required: false,
                DefaultLiteral: null,
                Description: "Callback with signature (status, data, request).",
                Kind: EndpointArgKind.Callback,
                Location: null));

            return args;
        }

        /// <summary>Renders the argument list for a GML function declaration.</summary>
        public static List<string> ToGmlParameters(IReadOnlyList<EndpointArg> args) =>
            args.Select(a => a.Required && a.Kind == EndpointArgKind.Parameter
                    ? a.Name
                    : $"{a.Name} = {a.DefaultLiteral ?? "undefined"}")
                .ToList();

        private static string Unique(string name, HashSet<string> used)
        {
            if (used.Add(name))
                return name;

            var i = 2;
            string candidate;
            do candidate = $"{name}_{i++}";
            while (!used.Add(candidate));

            return candidate;
        }
    }
}
