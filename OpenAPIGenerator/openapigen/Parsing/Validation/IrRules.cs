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
    /// Generated endpoint function names must be unique — GML has a single global function scope,
    /// so a duplicate silently shadows an operation.
    /// </summary>
    public sealed class NoDuplicateEndpointNamesRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrWebCompilation comp)
        {
            foreach (var group in comp.Endpoints.GroupBy(e => e.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                yield return new IrDiagnostic(
                    "IR_SYM_001",
                    $"{group.Count()} operations generate the same function name '{group.Key}': " +
                    string.Join(", ", group.Select(e => $"{e.Verb} {e.PathTemplate}")),
                    IrSeverity.Error,
                    group.Key);
            }
        }
    }

    /// <summary>
    /// Schema names must be unique for the same reason: one constructor per name.
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
}
