using codegencore.Model;
using openapigen.Emitters.Gml;
using Microsoft.OpenApi;
using openapigen.Model;
using System.Collections.Immutable;

namespace openapigen.Parsing.OpenApi
{
    internal sealed class OpenApiSchemaParser
    {
        private readonly OpenApiDocument _doc;
        private readonly BuildContext _ctx = new();

        public OpenApiSchemaParser(OpenApiDocument doc)
        {
            _doc = doc;
        }

        // ====================================================================
        // ENTRY
        // ====================================================================

        public IrWebCompilation Build()
        {
            // 1) Auth schemes
            if (_doc.Components?.SecuritySchemes is not null)
            {
                foreach (var (name, scheme) in _doc.Components.SecuritySchemes)
                    _ctx.AuthSchemes.Add(ToAuthScheme(name, scheme));
            }

            // 2) Component schemas
            if (_doc.Components?.Schemas is not null)
            {
                foreach (var (name, schema) in _doc.Components.Schemas)
                    EnsureDeclForComponent(name, schema);
            }

            // 3) Endpoints
            foreach (var (path, item) in _doc.Paths)
            {
                if (item.Operations is null) continue;

                foreach (var (verb, op) in item.Operations)
                    _ctx.Endpoints.Add(ToEndpoint(path, verb.ToString(), op));
            }

            return new IrWebCompilation(
                _ctx.Endpoints.OrderBy(e => e.Name).ToImmutableArray(),
                _ctx.Decls.Values.OrderBy(d => d.Name).ToImmutableArray(),
                _ctx.AuthSchemes.OrderBy(a => a.Name).ToImmutableArray()
            );
        }

        // ====================================================================
        // ENDPOINTS
        // ====================================================================

        private IrHttpEndpoint ToEndpoint(string path, string verb, OpenApiOperation op)
        {
            var parameters = op.Parameters?.Select(ToParam).ToImmutableArray() ?? [];
            var tags = op.Tags?.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToImmutableArray() ?? [];

            var opName = op.OperationId
                ?? GmlEndpointName.Make(tags.FirstOrDefault() ?? "", verb, path);

            return new IrHttpEndpoint(
                Name: GmlEndpointName.Make(tags.FirstOrDefault() ?? "", verb, path),
                Verb: verb.ToUpperInvariant(),
                PathTemplate: path,
                Parameters: parameters,
                Body: op.RequestBody is null ? null : PickBody(op.RequestBody, $"{opName}_body"),
                ResponseSchema: PickResponse(op.Responses, $"{opName}_response"),
                Auth: ResolveAuth(op),
                Summary: op.Summary,
                Description: op.Description,
                Tags: tags
            );
        }

        private IrParam ToParam(IOpenApiParameter p)
        {
            var location = p.In switch
            {
                ParameterLocation.Path => IrLocation.Path,
                ParameterLocation.Query => IrLocation.Query,
                ParameterLocation.Header => IrLocation.Header,
                ParameterLocation.Cookie => IrLocation.Cookie,
                _ => throw new NotSupportedException()
            };

            var schema = EnsureSchema(p.Schema!, $"param_{p.Name}");
            return new IrParam(p.Name!, schema, location, p.Required, p.Schema?.Default?.ToString(), p.Description);
        }

        // ====================================================================
        // REQUEST / RESPONSE
        // ====================================================================

        private static readonly string[] PreferOrder =
        {
            "application/json",
            "application/*+json",
            "application/x-www-form-urlencoded",
            "multipart/form-data",
            "text/plain",
            "*/*"
        };

        private IrRequestBody? PickBody(IOpenApiRequestBody rb, string ownerHint)
        {
            var supported = rb.Content?.Where(c => PreferOrder.Contains(c.Key)).ToArray() ?? [];
            if (supported.Length == 0) return null;

            var mediaTypes = PreferOrder.Where(mt => supported.Any(s => s.Key == mt)).ToImmutableArray();
            var schema = supported.First(s => s.Key == mediaTypes[0]).Value.Schema!;

            return new IrRequestBody(
                mediaTypes,
                EnsureSchema(schema, ownerHint),
                rb.Required
            );
        }

        private IrValueSchema? PickResponse(OpenApiResponses? reps, string ownerHint)
        {
            if (reps is null) return null;

            foreach (var (code, r) in reps)
            {
                if (!code.StartsWith("2")) continue;

                var mt = r.Content?.FirstOrDefault(c => PreferOrder.Contains(c.Key)).Value;
                if (mt?.Schema is null) continue;

                return EnsureSchema(mt.Schema, ownerHint);
            }

            return null;
        }

        // ====================================================================
        // AUTH
        // ====================================================================

        private IrAuthPolicy ResolveAuth(OpenApiOperation op)
        {
            if (op.Security is not null)
            {
                if (op.Security.Count == 0)
                    return NoAuthPolicy;

                return MapSecurity(op.Security);
            }

            return _doc.Security is { Count: > 0 }
                ? MapSecurity(_doc.Security)
                : NoAuthPolicy;
        }

        private IrAuthPolicy MapSecurity(IList<OpenApiSecurityRequirement> sec)
        {
            var alts = ImmutableArray.CreateBuilder<IrAuthRequirementSet>();

            foreach (var req in sec)
            {
                var and = ImmutableArray.CreateBuilder<IrAuthRequirement>();

                foreach (var (schemeRef, scopes) in req)
                {
                    var id = schemeRef?.Reference?.Id;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (_doc.Components?.SecuritySchemes?.ContainsKey(id!) != true) continue;

                    and.Add(new IrAuthRequirement.Scheme(id!, [.. scopes]));
                }

                if (and.Count > 0)
                    alts.Add(new IrAuthRequirementSet(and.ToImmutable()));
            }

            return alts.Count > 0
                ? new IrAuthPolicy(alts.ToImmutable())
                : NoAuthPolicy;
        }

        private static IrAuthScheme ToAuthScheme(string name, IOpenApiSecurityScheme s) => s.Type switch
        {
            SecuritySchemeType.Http when s.Scheme == "basic" => new IrAuthScheme.Basic(name),
            SecuritySchemeType.Http when s.Scheme == "bearer" => new IrAuthScheme.Bearer(name),
            SecuritySchemeType.ApiKey => new IrAuthScheme.ApiKey(name, s.Name!, s.In switch
            {
                ParameterLocation.Header => IrLocation.Header,
                ParameterLocation.Cookie => IrLocation.Cookie,
                _ => IrLocation.Query
            }),
            SecuritySchemeType.OpenIdConnect => new IrAuthScheme.OpenIdConnect(name, s.OpenIdConnectUrl!.OriginalString),
            SecuritySchemeType.OAuth2 => new IrAuthScheme.OAuth2(name, GetAllScopes(s.Flows!)),
            _ => throw new NotSupportedException()
        };

        private static ImmutableArray<string> GetAllScopes(OpenApiOAuthFlows flows)
        {
            var set = new HashSet<string>();
            foreach (var f in new[] { flows.AuthorizationCode, flows.ClientCredentials, flows.Implicit, flows.Password })
                if (f?.Scopes is not null)
                    foreach (var k in f.Scopes.Keys)
                        set.Add(k);
            return set.OrderBy(x => x).ToImmutableArray();
        }

        // ====================================================================
        // SCHEMAS
        // ====================================================================

        private void EnsureDeclForComponent(string name, IOpenApiSchema schema)
        {
            if (_ctx.Decls.ContainsKey(name)) return;

            _ctx.Decls[name] = new IrSchema.Alias(
                name,
                new IrValueSchema.Simple(IrType.Any),
                schema.Description
            );

            _ctx.Decls[name] = BuildDecl(name, Deref(schema), name);
        }

        private IrValueSchema EnsureSchema(IOpenApiSchema schema, string ownerHint)
        {
            if (schema is OpenApiSchemaReference r)
            {
                var id = r.Reference.Id!;
                EnsureDeclForComponent(id, _doc.Components!.Schemas![id]);

                if (r.Enum is not null)
                    return new IrValueSchema.Simple(new IrType.Named(NamedKind.Enum, id));
                return new IrValueSchema.Simple(new IrType.Named(NamedKind.Struct, id));
            }

            var s = Deref(schema);

            if (s.OneOf is { Count: > 0 })
                return new IrValueSchema.OneOf(
                    s.OneOf.Select(x => EnsureSchema(x, ownerHint)).ToImmutableArray());

            if (s.AnyOf is { Count: > 0 })
                return new IrValueSchema.AnyOf(
                    s.AnyOf.Select(x => EnsureSchema(x, ownerHint)).ToImmutableArray());

            if (s.AllOf is { Count: > 0 })
                return new IrValueSchema.AllOf(
                    s.AllOf.Select(x => EnsureSchema(x, ownerHint)).ToImmutableArray());

            if (IsSimplePrimitive(s))
                return new IrValueSchema.Simple(ToPrimitiveType(s));

            if (s.Type == JsonSchemaType.Array && s.Items is not null)
            {
                var elemSchema = EnsureSchema(s.Items, ownerHint + "_item");
                return new IrValueSchema.Simple(
                    new IrType.Array(ExtractType(elemSchema))
                );
            }

            // inline complex schema → named
            if (!_ctx.InlineNames.TryGetValue(schema, out var name))
            {
                name = MakeInlineName(ownerHint);
                _ctx.InlineNames[schema] = name;
                _ctx.Decls[name] = new IrSchema.Alias(name, new IrValueSchema.Simple(IrType.Any), s.Description);
                _ctx.Decls[name] = BuildDecl(name, s, name);
            }

            if (schema.Enum is not null)
                return new IrValueSchema.Simple(new IrType.Named(NamedKind.Enum, name));
            return new IrValueSchema.Simple(new IrType.Named(NamedKind.Struct, name));
        }

        private IrSchema BuildDecl(string name, IOpenApiSchema s, string ownerHint)
        {
            if (s.Enum is { Count: > 0 })
            {
                var literals = s.Enum.Select(e => e?.ToString() ?? "").ToImmutableArray();
                return new IrSchema.Enum(name, ToPrimitiveType(s), literals, s.Description);
            }

            if (IsObjectLike(s))
            {
                var fields = ImmutableArray.CreateBuilder<IrField>();

                if (s.Properties is not null)
                {
                    foreach (var (prop, propSchema) in s.Properties)
                    {
                        fields.Add(new IrField(
                            prop,
                            EnsureSchema(propSchema, ownerHint + "_" + prop),
                            s.Required?.Contains(prop) ?? false,
                            propSchema.ReadOnly,
                            propSchema.WriteOnly,
                            propSchema.Default?.ToString(),
                            propSchema.Description
                        ));
                    }
                }

                return new IrSchema.Struct(
                    name,
                    fields.ToImmutable(),
                    s.AdditionalPropertiesAllowed,
                    s.AdditionalProperties is null ? null : ExtractType(EnsureSchema(s.AdditionalProperties, ownerHint + "_ap")),
                    s.Description
                );
            }

            return new IrSchema.Alias(name, EnsureSchema(s, ownerHint), s.Description);
        }

        // ====================================================================
        // HELPERS
        // ====================================================================

        private static IrType ExtractType(IrValueSchema schema) =>
            schema is IrValueSchema.Simple s ? s.Type : IrType.Any;

        private static bool IsPrimitive(IOpenApiSchema s) =>
            s.Type is JsonSchemaType.String or JsonSchemaType.Integer or JsonSchemaType.Number or JsonSchemaType.Boolean;

        private static bool IsSimplePrimitive(IOpenApiSchema s) =>
            IsPrimitive(s) && (s.Enum is null || s.Enum.Count == 0);

        private static bool IsObjectLike(IOpenApiSchema s) =>
            s.Type == JsonSchemaType.Object || s.Properties is { Count: > 0 };

        private static IrType ToPrimitiveType(IOpenApiSchema s) => s.Type switch
        {
            JsonSchemaType.Boolean => IrType.Bool,
            JsonSchemaType.Integer => s.Format == "int64" ? IrType.Int64 : IrType.Int32,
            JsonSchemaType.Number => s.Format == "float" ? IrType.Float : IrType.Double,
            JsonSchemaType.String => s.Format is "binary" or "byte" ? IrType.Buffer : IrType.String,
            _ => IrType.Any
        };

        private IOpenApiSchema Deref(IOpenApiSchema s) =>
            s is OpenApiSchemaReference r ? _doc.Components!.Schemas![r.Reference.Id!] : s;

        private string MakeInlineName(string hint)
        {
            var baseName = new string(hint.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            if (!_ctx.InlineCounters.TryGetValue(baseName, out var n))
                n = 0;
            _ctx.InlineCounters[baseName] = n + 1;
            return n == 0 ? baseName : $"{baseName}_{n + 1}";
        }

        private static IrAuthPolicy NoAuthPolicy => new([new IrAuthRequirementSet([new IrAuthRequirement.None()])]);

        private sealed class BuildContext
        {
            internal readonly Dictionary<string, IrSchema> Decls = new();
            internal readonly Dictionary<IOpenApiSchema, string> InlineNames = new();
            internal readonly Dictionary<string, int> InlineCounters = new();
            internal readonly List<IrHttpEndpoint> Endpoints = new();
            internal readonly List<IrAuthScheme> AuthSchemes = new();
        }
    }
}
