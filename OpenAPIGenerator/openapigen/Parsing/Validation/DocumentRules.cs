using Microsoft.OpenApi;
using System.Collections.Immutable;

namespace openapigen.Parsing.Validation
{
    /// <summary>
    /// A rule that runs against the raw OpenAPI document, before it is parsed into IR.
    ///
    /// Most policy belongs in <see cref="IIrRule"/>, which sees a finished compilation and can talk
    /// about endpoints and schemas rather than JSON. A document rule exists for the narrower case of
    /// input that cannot survive parsing at all - where the parser would fail before any IR exists to
    /// validate.
    /// </summary>
    public interface IDocumentRule
    {
        /// <summary>Validates a loaded document and returns any diagnostics.</summary>
        IEnumerable<IrDiagnostic> Validate(OpenApiDocument doc);
    }

    /// <summary>
    /// Runs a set of document rules. Mirrors <see cref="IrValidator"/> so both stages report through
    /// the same diagnostic channel.
    /// </summary>
    public sealed class DocumentValidator(params IDocumentRule[] rules)
    {
        private readonly IDocumentRule[] _rules = rules;

        public ImmutableArray<IrDiagnostic> Validate(OpenApiDocument doc) =>
            [.. _rules.SelectMany(r => r.Validate(doc))];
    }

    /// <summary>
    /// Rejects a cycle of components that are nothing but a <c>$ref</c> to the next: the chain never
    /// reaches a concrete type. It has to run before the parser dereferences anything, because
    /// resolving such a cycle recurses forever inside Microsoft.OpenApi and .NET cannot catch the
    /// resulting StackOverflowException. Only whole-schema refs form an edge, so a schema that
    /// recurses through a property is left alone.
    /// </summary>
    public sealed class SchemaReferenceCycleRule : IDocumentRule
    {
        public IEnumerable<IrDiagnostic> Validate(OpenApiDocument doc)
        {
            if (doc.Components?.Schemas is not { Count: > 0 } schemas)
                yield break;

            // Only Reference.Id is read. It is the one member that describes the edge itself rather
            // than the thing at the far end of it, so it is safe on a circular chain.
            var aliasEdges = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, schema) in schemas)
            {
                if (schema is OpenApiSchemaReference reference && reference.Reference?.Id is { } target)
                    aliasEdges[name] = target;
            }

            if (aliasEdges.Count == 0)
                yield break;

            var reported = new HashSet<string>(StringComparer.Ordinal);

            foreach (var start in aliasEdges.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (reported.Contains(start))
                    continue;

                if (FindCycle(start, aliasEdges) is not { } cycle)
                    continue;

                foreach (var member in cycle)
                    reported.Add(member);

                var chain = string.Join(" -> ", cycle.Append(cycle[0]));

                yield return new IrDiagnostic(
                    "IR_REF_001",
                    $"Schema reference cycle: {chain}. Every schema in this chain is only a $ref to " +
                    "the next, so it never reaches a concrete definition. Give one of them a real " +
                    "type, or drop the alias.",
                    IrSeverity.Error,
                    $"#/components/schemas/{cycle[0]}");
            }
        }

        /// <summary>
        /// Walks the alias chain from <paramref name="start"/>, returning the members of the cycle it
        /// closes, or null when the chain reaches a concrete schema.
        /// </summary>
        private static List<string>? FindCycle(string start, Dictionary<string, string> edges)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            var walk = new List<string>();
            var current = start;

            while (true)
            {
                if (order.TryGetValue(current, out var firstSeenAt))
                    return walk.Skip(firstSeenAt).ToList();

                order[current] = walk.Count;
                walk.Add(current);

                if (!edges.TryGetValue(current, out var next))
                    return null;

                current = next;
            }
        }
    }
}
