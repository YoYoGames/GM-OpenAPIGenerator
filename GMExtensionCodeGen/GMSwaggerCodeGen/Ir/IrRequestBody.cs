using CodeGenCore.Ir;
using System.Collections.Immutable;

namespace GMSwaggerCodeGen.Ir
{
    /// <summary>
    /// One concrete (media-type, schema) pairing we’ve decided to support
    /// for the endpoint.  GML runtime helpers will look at <see cref="MediaType"/>
    /// to decide how to serialize <see cref="Schema"/>.
    /// </summary>
    public sealed record IrRequestBody(
        ImmutableArray<string> MediaTypes,   // every media-type we kept
        IrType Schema,       // schema from the “chosen” entry
        bool Required = false)
    {
        public string DefaultMediaType => MediaTypes[0];          // first one wins
        public bool HasChoice => MediaTypes.Length > 1 || DefaultMediaType == "*/*";
    }
}
