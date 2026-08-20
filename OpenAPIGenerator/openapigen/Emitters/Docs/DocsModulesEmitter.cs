using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;
using openapigen.Utils;
using System.Text;

namespace openapigen.Emitters.Docs
{
    /// <summary>
    /// The module blocks that tie the generated partials together.
    ///
    /// <see cref="DocsFunctionsEmitter"/> and <see cref="DocsSchemasEmitter"/> emit
    /// <c>@func_partial</c> and <c>@struct_partial</c> blocks, which describe an individual symbol but
    /// place it nowhere. A partial that no module references is documented and unreachable. This
    /// emitter produces the pages that reference them.
    ///
    /// References are written out one per function rather than as a glob. A glob only works when the
    /// group name is a prefix of the function name, and these names are
    /// <c>&lt;prefix&gt;_&lt;verb&gt;_&lt;noun&gt;</c> — so <c>*application*</c> would also capture
    /// <c>get_application_configuration</c> and file it under the wrong page.
    /// </summary>
    public sealed class DocsModulesEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);
            var groups = GroupEndpoints(ir);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Documentation Modules (auto-generated, DO NOT EDIT)").Line();

                foreach (var (group, endpoints) in groups)
                {
                    w.JsDoc(b =>
                    {
                        b.Line($"@module {naming.Prefix}_{NameUtils.ToSnake(group)}");
                        b.Line($"@title {Titleise(group)}");
                        b.Line($"@desc The {Titleise(group)} endpoints.");
                        b.Line("");
                        b.Line("@section_func Functions");
                        foreach (var ep in endpoints.OrderBy(e => e.Name, StringComparer.Ordinal))
                            b.Line($"@ref {naming.Pub}{ep.Name}");
                        b.Line("@section_end");
                        b.Line("@module_end");
                    });
                    w.Line();
                }

                // One page for the structs. A glob is safe here: every generated struct carries the
                // prefix and nothing else does.
                var structs = ir.Schemas.OfType<IrSchema.Struct>().ToList();
                if (structs.Count == 0)
                    return;

                w.JsDoc(b =>
                {
                    b.Line($"@module {naming.Prefix}_schemas");
                    b.Line("@title Schemas");
                    b.Line("@desc The structs used by the generated functions.");
                    b.Line("");
                    b.Line("@section_struct Structs");
                    b.Line($"@ref {naming.StructPrefix}*");
                    b.Line("@section_end");
                    b.Line("@module_end");
                });
                w.Line();
            });
        }

        /// <summary>
        /// Groups the endpoints into pages.
        ///
        /// A spec that tags its operations has already stated how it wants to be organised, so the
        /// tag wins. A spec with a single tag across every operation — or none at all — has said
        /// nothing, and the first path segment is the next best statement of intent: it is the
        /// resource the endpoint acts on, which is how REST references are conventionally arranged.
        /// </summary>
        private static List<(string Group, List<IrHttpEndpoint> Endpoints)> GroupEndpoints(IrWebCompilation ir)
        {
            var distinctTags = ir.Endpoints
                .Select(e => e.Tags.FirstOrDefault())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var useTags = distinctTags > 1;

            return ir.Endpoints
                .GroupBy(e => useTags ? e.Tags.FirstOrDefault() ?? "general" : FirstPathSegment(e.PathTemplate),
                         StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (g.Key, g.ToList()))
                .ToList();
        }

        /// <summary>The resource an endpoint acts on, or "general" for a root-level path.</summary>
        private static string FirstPathSegment(string pathTemplate)
        {
            foreach (var segment in pathTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                // A path that starts with a parameter names no resource.
                if (segment.StartsWith('{')) continue;
                return segment;
            }

            return "general";
        }

        /// <summary>"auth_scheme" and "AuthScheme" both become "Auth Scheme".</summary>
        private static string Titleise(string group)
        {
            var words = NameUtils.ToSnake(group).Split('_', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var word in words)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(word[0])).Append(word[1..]);
            }

            return sb.Length == 0 ? group : sb.ToString();
        }
    }
}
