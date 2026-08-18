using codegencore.Model;
using openapigen.Model;
using System.Globalization;

namespace openapigen.Helpers
{
    /// <summary>
    /// Renders OpenAPI default values as GML literals.
    /// </summary>
    internal static class GmlLiteral
    {
        /// <summary>
        /// Converts a spec default to a GML literal, or returns null when there is no usable
        /// default (the caller then emits <c>undefined</c>).
        /// </summary>
        public static string? For(string? raw, IrValueSchema? schema)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var type = schema is IrValueSchema.Simple s ? s.Type : null;
            return For(raw, type);
        }

        public static string? For(string? raw, IrType? type)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var value = raw.Trim();

            return Unwrap(type) switch
            {
                IrType.Builtin b => b.Kind switch
                {
                    BuiltinKind.Bool => AsBool(value),
                    BuiltinKind.Int8 or BuiltinKind.UInt8 or
                    BuiltinKind.Int16 or BuiltinKind.UInt16 or
                    BuiltinKind.Int32 or BuiltinKind.UInt32 or
                    BuiltinKind.Int64 or BuiltinKind.UInt64 or
                    BuiltinKind.Float32 or BuiltinKind.Float64 => AsNumber(value),
                    BuiltinKind.String => Quote(value),
                    _ => null
                },

                // Enums are string-valued in the generated code.
                IrType.Named { Kind: NamedKind.Enum } => Quote(value),

                // Structs, arrays and anything unknown: no safe literal form.
                _ => null
            };
        }

        private static IrType? Unwrap(IrType? t) => t switch
        {
            IrType.Nullable n => Unwrap(n.Underlying),
            _ => t
        };

        private static string? AsBool(string value) => value.ToLowerInvariant() switch
        {
            "true" => "true",
            "false" => "false",
            _ => null
        };

        private static string? AsNumber(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d.ToString("R", CultureInfo.InvariantCulture)
                : null;

        private static string Quote(string value) =>
            "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
