using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using openapigen.Model;
using openapigen.Parsing.OpenApi;
using System.Text;

namespace GMSwaggerCodeGen.Parsing.OpenApi
{
    /// <summary>
    /// Entry point: load an OpenAPI 3.x YAML/JSON file and return a
    /// <see cref="WebIrCompilation"/> ready for emitters.
    /// </summary>
    public static class OpenApiSchemaLoader
    {
        public static IrWebCompilation LoadFromFile(string path)
        {
            var jsonText = File.ReadAllText(path);
            var jsonBytes = Encoding.UTF8.GetBytes(jsonText);
            using var ms = new MemoryStream(jsonBytes);

            var settings = new OpenApiReaderSettings{};
            var result = OpenApiDocument.Load(ms,
                format: "json",         // <-- explicitly JSON
                settings: settings);

            // 3) Pull out the parsed document
            OpenApiDocument doc = result.Document!;

            var comp = new OpenApiSchemaParser(doc).Build();

            // Lightweight validation: each Path param must appear in template
            foreach (var ep in comp.Endpoints)
            {
                var missingPathVars = ep.Parameters
                                         .Where(p => p.Location == IrLocation.Path &&
                                                     !ep.PathTemplate.Contains($"{{{p.Name}}}"))
                                         .Select(p => p.Name)
                                         .ToArray();
                if (missingPathVars.Length > 0)
                    throw new InvalidOperationException(
                        $"Endpoint '{ep.Name}' has path params not present in the template: " +
                        string.Join(", ", missingPathVars));
            }

            return comp;
        }
    }
}
