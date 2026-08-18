using openapigen.Model;
using System.Collections.Immutable;

namespace openapigen.Parsing.Validation
{
    /// <summary>Diagnostic severity levels.</summary>
    public enum IrSeverity
    {
        /// <summary>Informational message.</summary>
        Info,

        /// <summary>Warning message; generation continues.</summary>
        Warning,

        /// <summary>Error; generation stops.</summary>
        Error
    }

    /// <summary>
    /// A single diagnostic produced by IR validation.
    /// </summary>
    public sealed record IrDiagnostic(
        string Code,
        string Message,
        IrSeverity Severity,
        string? Path = null);

    /// <summary>Interface for IR validation rules.</summary>
    public interface IIrRule
    {
        /// <summary>Validates a compilation and returns any diagnostics.</summary>
        IEnumerable<IrDiagnostic> Validate(IrWebCompilation comp);
    }

    /// <summary>
    /// Runs a set of rules over a parsed compilation.
    /// </summary>
    public sealed class IrValidator(params IIrRule[] rules)
    {
        private readonly IIrRule[] _rules = rules;

        public ImmutableArray<IrDiagnostic> Validate(IrWebCompilation comp) =>
            [.. _rules.SelectMany(r => r.Validate(comp))];
    }
}
