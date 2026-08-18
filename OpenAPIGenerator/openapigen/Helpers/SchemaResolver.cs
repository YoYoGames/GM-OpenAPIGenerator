using codegencore.Model;
using openapigen.Model;

namespace openapigen.Helpers
{
    /// <summary>
    /// Looks schemas up by name and follows alias chains.
    /// </summary>
    internal sealed class SchemaResolver
    {
        /// <summary>Guards against a cyclic alias chain in a malformed spec.</summary>
        private const int MaxHops = 32;

        private readonly IReadOnlyDictionary<string, IrSchema> _map;

        public SchemaResolver(IrWebCompilation compilation)
        {
            _map = compilation.Schemas.ToDictionary(s => s.Name, s => s);
        }

        public bool TryGet(string name, out IrSchema schema) => _map.TryGetValue(name, out schema!);

        public IrSchema Get(string name) => _map[name];

        /// <summary>Resolves Simple(Named(alias)) to the alias target, repeatedly.</summary>
        public IrValueSchema Unalias(IrValueSchema schema)
        {
            var current = schema;

            for (var i = 0; i < MaxHops; i++)
            {
                if (current is not IrValueSchema.Simple simple) return current;
                if (simple.Type is not IrType.Named named) return current;
                if (!_map.TryGetValue(named.Name, out var decl)) return current;
                if (decl is not IrSchema.Alias a) return current;

                current = a.Schema;
            }

            return current;
        }

        /// <summary>Follows an alias declaration to the concrete declaration it names.</summary>
        public IrSchema UnaliasDecl(IrSchema decl)
        {
            var current = decl;

            for (var i = 0; i < MaxHops; i++)
            {
                if (current is not IrSchema.Alias a) return current;

                var target = Unalias(a.Schema);

                if (target is IrValueSchema.Simple s &&
                    s.Type is IrType.Named n &&
                    _map.TryGetValue(n.Name, out var next))
                {
                    current = next;
                    continue;
                }

                return current;
            }

            return current;
        }
    }
}
