using codegencore.Model;
using openapigen.Helpers;
using openapigen.Model;

namespace openapigen.Parsing.Validation
{
    /// <summary>
    /// Every operation should declare an <c>operationId</c>.
    ///
    /// Generated function names are permanent public API, and <c>operationId</c> is the only
    /// author-controlled, stable source for them: a name derived from the path changes whenever the
    /// URL is refactored, silently breaking every caller. Errors by default; downgrade to a warning
    /// for third-party specs that cannot be edited.
    /// </summary>
    public sealed class OperationIdRequiredRule(bool required) : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrWebCompilation comp)
        {
            foreach (var ep in comp.Endpoints.Where(e => string.IsNullOrWhiteSpace(e.OperationId)))
            {
                yield return new IrDiagnostic(
                    "IR_OP_001",
                    $"Operation has no 'operationId'; the generated name '{ep.Name}' was derived from " +
                    "its path and will change if the path does. Add an operationId, or set " +
                    "\"requireOperationId\": false in the config to accept derived names.",
                    required ? IrSeverity.Error : IrSeverity.Warning,
                    $"{ep.Verb} {ep.PathTemplate}");
            }
        }
    }

    /// <summary>
    /// Every declared path parameter must appear in its path template.
    /// </summary>
    public sealed class PathParamsDeclaredRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrWebCompilation comp)
        {
            foreach (var ep in comp.Endpoints)
            {
                foreach (var p in ep.Parameters.Where(p => p.Location == IrLocation.Path))
                {
                    if (ep.PathTemplate.Contains($"{{{p.Name}}}", StringComparison.Ordinal))
                        continue;

                    yield return new IrDiagnostic(
                        "IR_PATH_001",
                        $"Path parameter '{p.Name}' is not present in the path template.",
                        IrSeverity.Error,
                        $"{ep.Verb} {ep.PathTemplate}");
                }
            }
        }
    }

    /// <summary>
    /// Two operations must not ask for the same generated function name - GML has a single global
    /// function scope, so one would shadow the other.
    ///
    /// The parser resolves the clash with a numeric suffix so the emitted file still compiles, which
    /// means the duplicate never survives into the IR. What survives is the *evidence*: an endpoint
    /// whose <c>operationId</c> no longer produces its own name was the loser of a collision. Checking
    /// for that is what makes this rule reachable at all - grouping the finished IR by name cannot
    /// work, because the parser has already made those names unique.
    ///
    /// Errors by default; a warning when <c>requireOperationId</c> is false, matching
    /// <see cref="OperationIdRequiredRule"/> - that flag means a third-party spec the user cannot edit,
    /// and making it ungeneratable would defeat its purpose. The rename is reported either way.
    /// </summary>
    public sealed class NoDuplicateEndpointNamesRule(bool required) : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrWebCompilation comp)
        {
            var renamed = comp.Endpoints
                .Select(e => (Endpoint: e, Intended: NameUtils.IntendedEndpointFuncName(e.OperationId)))
                .Where(x => x.Intended.Length > 0
                            && !string.Equals(x.Intended, x.Endpoint.Name, StringComparison.Ordinal))
                .ToList();

            foreach (var group in renamed.GroupBy(x => x.Intended, StringComparer.Ordinal))
            {
                var holder = comp.Endpoints.FirstOrDefault(e =>
                    string.Equals(e.Name, group.Key, StringComparison.Ordinal));

                var claimants = new List<string>();
                if (holder is not null)
                    claimants.Add($"{holder.Verb} {holder.PathTemplate} (kept '{group.Key}')");

                claimants.AddRange(group.Select(x =>
                    $"{x.Endpoint.Verb} {x.Endpoint.PathTemplate} (renamed to '{x.Endpoint.Name}')"));

                yield return new IrDiagnostic(
                    "IR_SYM_001",
                    $"{claimants.Count} operations generate the same function name '{group.Key}': " +
                    string.Join(", ", claimants) + ". " +
                    "Generated names are permanent public API and the suffix is positional - reordering " +
                    "the spec would move it to a different operation. Give each a distinct operationId.",
                    required ? IrSeverity.Error : IrSeverity.Warning,
                    group.Key);
            }
        }
    }

    /// <summary>
    /// Schema names must be unique: one constructor per name.
    ///
    /// **Unreachable by construction, and kept deliberately.** Components are keyed by name by OpenAPI
    /// itself, inline-vs-inline collisions are resolved by the inline counter, and inline-vs-component
    /// collisions are prevented by the parser reserving the component namespace before it builds
    /// anything. This rule is retained as a cheap assertion in case a future change introduces a third
    /// way to register a schema name.
    /// </summary>
    public sealed class NoDuplicateSchemaNamesRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrWebCompilation comp)
        {
            foreach (var group in comp.Schemas.GroupBy(s => s.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                yield return new IrDiagnostic(
                    "IR_SYM_002",
                    $"{group.Count()} schemas share the name '{group.Key}'.",
                    IrSeverity.Error,
                    group.Key);
            }
        }
    }

    /// <summary>
    /// An alias must eventually name something concrete. <see cref="SchemaReferenceCycleRule"/>
    /// catches this in the document; this is the same invariant over the IR the parser built, so a
    /// future change to <c>BuildDecl</c> surfaces as a message rather than a stack overflow.
    /// </summary>
    public sealed class NoSelfReferentialAliasRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrWebCompilation comp)
        {
            var byName = comp.Schemas.ToDictionary(s => s.Name, s => s, StringComparer.Ordinal);

            foreach (var alias in comp.Schemas.OfType<IrSchema.Alias>())
            {
                if (!ResolvesToItself(alias, byName))
                    continue;

                yield return new IrDiagnostic(
                    "IR_SYM_003",
                    $"Schema '{alias.Name}' is an alias for itself, so it never names a concrete type. " +
                    "This is a generator bug rather than a spec error - please report the spec that " +
                    "produced it.",
                    IrSeverity.Error,
                    alias.Name);
            }
        }

        private static bool ResolvesToItself(IrSchema.Alias alias, Dictionary<string, IrSchema> byName)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { alias.Name };
            var current = alias.Target;

            while (current is IrValueSchema.Simple { Type: IrType.Named named })
            {
                if (string.Equals(named.Name, alias.Name, StringComparison.Ordinal))
                    return true;

                // A different cycle that never reaches this alias is not this rule's finding.
                if (!seen.Add(named.Name) || !byName.TryGetValue(named.Name, out var next))
                    return false;

                if (next is not IrSchema.Alias link)
                    return false;

                current = link.Target;
            }

            return false;
        }
    }
}
