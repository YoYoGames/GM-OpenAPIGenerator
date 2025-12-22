using CommandLine;
using GMSwaggerCodeGen.Emitters;
using GMSwaggerCodeGen.Emitters.Docs;
using GMSwaggerCodeGen.Emitters.Gml;
using GMSwaggerCodeGen.Helpers;
using GMSwaggerCodeGen.Parsing.OpenApi;

namespace GMSwaggerCodeGen
{
    internal sealed class Options
    {
        [Option('i', "input", Required = true,
                HelpText = "Path to schema file (OpenAPI).")]
        public FileInfo Input { get; set; } = default!;

        [Option('o', "output",
                HelpText = "Output directory (default: ./build).",
                Default = "build")]
        public string Output { get; set; } = default!;

        [Option("lang",
                HelpText = "Comma-separated list of targets (gml, docs).",
                Separator = ',',
                Default = new[] { "gml", "docs" })]
        public IEnumerable<string> Languages { get; set; } = default!;

        [Option("prefix", 
            HelpText = "Namespace prefix for generated names (default: gm)",
            Default = "gm")]
        public string Namespace { get; set; } = default!;
    }

    public static class Program
    {
        public static int Main(string[] args)
            => Parser.Default.ParseArguments<Options>(args)
                .MapResult(RunGeneration, errs => 1);

        private static int RunGeneration(Options opt)
        {
            if (!opt.Input.Exists)
            {
                Console.Error.WriteLine($"Input file not found: {opt.Input}");
                return 2;
            }

            var outDir = new DirectoryInfo(opt.Output);
            outDir.Create();

            var compilation = OpenApiSchemaLoader.LoadFromFile(opt.Input.FullName);

            var naming = new GmlNaming(opt.Namespace);
            var emitters = new Dictionary<string, IIrEmitter>(StringComparer.OrdinalIgnoreCase)
            {
                ["gml"] = new HttpGmlEmitter(naming),
                ["docs"] = new DocsEmitter(naming),
            };

            foreach (var lang in opt.Languages)
            {
                if (!emitters.TryGetValue(lang, out var em))
                {
                    Console.Error.WriteLine($"Unknown lang '{lang}'.");
                    return 3;
                }

                var targetDir = outDir.FullName;
                Directory.CreateDirectory(targetDir);

                Console.WriteLine($"[CodeGen] {lang.ToUpper()} -> {targetDir}");
                em.Emit(compilation, targetDir);
            }

            Console.WriteLine("[CodeGen] Success [x]");
            return 0;
        }
    }
}
