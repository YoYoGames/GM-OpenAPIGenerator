using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Microsoft.OpenApi.YamlReader;
using openapigen.Model;
using openapigen.Parsing.Validation;

namespace openapigen.Parsing.OpenApi
{
    /// <summary>
    /// Entry point: load an OpenAPI 3.x YAML/JSON file and return an
    /// <see cref="IrWebCompilation"/> ready for emitters.
    /// </summary>
    public static class OpenApiSchemaLoader
    {
        private const string JsonFormat = "json";
        private const string YamlFormat = "yaml";

        public static IrWebCompilation LoadFromFile(string path, bool requireOperationId = true)
        {
            var format = DetectFormat(path);

            var settings = new OpenApiReaderSettings();
            settings.AddYamlReader();

            using var stream = new MemoryStream(File.ReadAllBytes(path));
            var result = OpenApiDocument.Load(stream, format: format, settings: settings);

            // A document that parsed is usable even if it breaks validation rules — plenty of
            // real-world specs omit a required 'description'. Only a null document is fatal.
            var doc = result.Document
                      ?? throw new InvalidOperationException(
                          BuildFailureMessage(result.Diagnostic, path, format));

            ReportDiagnosticWarnings(result.Diagnostic, path);

            var comp = new OpenApiSchemaParser(doc).Build();

            Validate(comp, requireOperationId);

            return comp;
        }

        /// <summary>
        /// Runs the IR rules and stops on any error, after reporting every diagnostic — one run
        /// should surface all the problems, not just the first.
        /// </summary>
        private static void Validate(IrWebCompilation comp, bool requireOperationId)
        {
            var validator = new IrValidator(
                // Naming
                new OperationIdRequiredRule(requireOperationId),
                new NoDuplicateEndpointNamesRule(requireOperationId),
                new NoDuplicateSchemaNamesRule(),

                // Structure
                new PathParamsDeclaredRule()
            );

            var diagnostics = validator.Validate(comp);
            if (diagnostics.Length == 0)
                return;

            foreach (var d in diagnostics.OrderByDescending(d => d.Severity))
                Console.Error.WriteLine(
                    $"[openapigen] {d.Severity.ToString().ToLowerInvariant()} {d.Code}: {d.Message}" +
                    (d.Path is null ? "" : $" @ {d.Path}"));

            var errors = diagnostics.Count(d => d.Severity == IrSeverity.Error);
            if (errors > 0)
                throw new InvalidOperationException($"IR validation failed with {errors} error(s).");
        }

        /// <summary>Picks the reader format from the file extension; anything unknown is treated as JSON.</summary>
        private static string DetectFormat(string path) =>
            Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".yaml" or ".yml" => YamlFormat,
                _ => JsonFormat
            };

        private const int MaxReportedDiagnostics = 10;

        /// <summary>
        /// Explains why nothing could be parsed. Without the diagnostics, a bad $ref or a non-spec
        /// JSON file reaches the caller as a bare NullReferenceException.
        /// </summary>
        private static string BuildFailureMessage(OpenApiDiagnostic? diagnostic, string path, string format)
        {
            var header = $"'{path}' did not parse into an OpenAPI document. " +
                         $"Check that it is a valid OpenAPI 3.x {format.ToUpperInvariant()} file.";

            if (diagnostic?.Errors is not { Count: > 0 } errors)
                return header;

            return header + Environment.NewLine + Format(errors);
        }

        /// <summary>
        /// Reports validation-rule violations without stopping generation — the document parsed, so
        /// the emitters can work with it.
        /// </summary>
        private static void ReportDiagnosticWarnings(OpenApiDiagnostic? diagnostic, string path)
        {
            if (diagnostic?.Errors is not { Count: > 0 } errors)
                return;

            Console.Error.WriteLine(
                $"[openapigen] warning: '{Path.GetFileName(path)}' has {errors.Count} OpenAPI " +
                $"validation issue(s); generating anyway.");
            Console.Error.WriteLine(Format(errors));
        }

        private static string Format(IList<OpenApiError> errors)
        {
            var shown = errors.Take(MaxReportedDiagnostics).Select(e => string.IsNullOrWhiteSpace(e.Pointer)
                ? $"  - {e.Message}"
                : $"  - {e.Pointer}: {e.Message}");

            var text = string.Join(Environment.NewLine, shown);

            if (errors.Count > MaxReportedDiagnostics)
                text += $"{Environment.NewLine}  … and {errors.Count - MaxReportedDiagnostics} more.";

            return text;
        }

    }
}
