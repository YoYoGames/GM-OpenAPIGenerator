
using CodeGenCore.Ir;
using GMSwaggerCodeGen.Emitters.Gml;
using GMSwaggerCodeGen.Helpers;
using GMSwaggerCodeGen.Ir;
using Microsoft.OpenApi;
using System.Collections.Immutable;
using System.Text.Json;

namespace GMSwaggerCodeGen.Parsing.OpenApi
{
    /// <summary>
    /// Converts <see cref="OpenApiDocument"/> → <see cref="WebIrCompilation"/>.
    /// </summary>
    internal sealed class OpenApiSchemaParser(OpenApiDocument doc)
    {
        private readonly OpenApiDocument _doc = doc;
        private readonly BuildContext _ctx = new();

        public IrWebCompilation Build()
        {
            // Parse the authentication schemes
            if (_doc.Components?.SecuritySchemes is not null)
            {
                foreach (var (name, scheme) in _doc.Components.SecuritySchemes)
                    _ctx.AuthSchemes.Add(ToAuthScheme(name, scheme));
            }

            // Parse the schemas (constructors)
            if (_doc.Components?.Schemas is not null)
            {
                foreach (var (name, schema) in _doc.Components.Schemas)
                {
                    BuildStruct(name, schema);
                }
            }

            // Parse the end points
            foreach (var (path, item) in _doc.Paths)
            {
                if (item.Operations is not null)
                {
                    foreach (var (verb, op) in item.Operations)
                        _ctx.Endpoints.Add(ToEndpoint(path, verb.ToString(), op));
                }
            }

            return new IrWebCompilation([.. _ctx.Endpoints.OrderBy(ep => ep.Name)], [.. _ctx.Structs], [.. _ctx.AuthSchemes]);
        }

        private IrHttpEndpoint ToEndpoint(string path, string verb, OpenApiOperation op)
        {
            var pars = op.Parameters?.Select(ToParam).ToImmutableArray() ?? [];

            var tags = op.Tags?
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToImmutableArray()
                ?? [];
            var operationId = op.OperationId ?? throw new ArgumentNullException($"{path} :: {verb} :: operationId is required.");

            return new IrHttpEndpoint(
                Name: NameUtils.EndpointFuncName(operationId, tags.Length > 0 ? tags[0] : null),
                Verb: verb.ToUpperInvariant(),
                PathTemplate: path,
                Parameters: pars,
                Body: op.RequestBody is null ? null : PickBody(op.RequestBody),
                ResponseSchema: PickResponse(op.Responses),
                Auth: ResolveAuth(op), 
                op.Summary, 
                op.Description, 
                Tags: tags!);
        }

        private IrParam ToParam(IOpenApiParameter p)
        {
            var loc = p.In switch
            {
                ParameterLocation.Path => IrLocation.Path,
                ParameterLocation.Query => IrLocation.Query,
                ParameterLocation.Header => IrLocation.Header,
                _ => throw new NotSupportedException($"Param location {p.In} not supported.")
            };
            return new IrParam(p.Name!, ToIrType(p.Schema!), loc, p.Required, p.Schema?.Default?.ToString(), p.Description);
        }

        private static readonly string[] _preferOrder =
        {
            "application/json",
            "application/*+json",
            "application/x-www-form-urlencoded",
            "multipart/form-data",
            "text/plain",
            "*/*"
        };

        private IrRequestBody? PickBody(IOpenApiRequestBody rb)
        {
            // 1) keep *only* encodings we know how to serialize
            var supported = rb.Content?
                              .Where(c => _preferOrder.Contains(c.Key))
                              .ToArray() ?? [];
            if (supported.Length == 0)
                return null;                                    // ignore XML etc.

            // 2) stable ordering: by our preference list
            var mediaTypes = _preferOrder
                             .Where(mt => supported.Any(s => s.Key == mt))
                             .ToImmutableArray();

            // 3) choose the first entry’s schema as “canonical”
            var chosenMt = mediaTypes[0];
            var schema = supported.First(s => s.Key == chosenMt).Value.Schema!;
            var irSchema = ToIrType(schema);

            return new IrRequestBody(mediaTypes, irSchema, rb.Required);
        }

        private IrType? PickResponse(OpenApiResponses? reps)
        {
            if (reps is not null)
            {
                foreach (var (code, r) in reps)
                {
                    if (!code.StartsWith('2')) continue;
                    var mt = r.Content?.FirstOrDefault(c => _preferOrder.Contains(c.Key)).Value;
                    if (mt is null) continue;
                    return ToIrType(mt.Schema!);
                }
            }
            return null;                                // no JSON response
        }

        private ImmutableArray<IrAuthRequirement> ResolveAuth(OpenApiOperation op)
        {
            // Operation-level security overrides global security:
            // - null  → fall back to document
            // - empty → explicitly "no auth"
            if (op.Security is not null)
            {
                if (op.Security.Count == 0)
                    return [new IrNoAuth()];

                return MapReqs(op.Security);
            }

            if (_doc.Security is { Count: > 0 })
                return MapReqs(_doc.Security);

            return [new IrNoAuth()];

            // Map a whole list of OpenApiSecurityRequirement → list of IrAuthRequirement
            ImmutableArray<IrAuthRequirement> MapReqs(IList<OpenApiSecurityRequirement> requirements)
            {
                var builder = ImmutableArray.CreateBuilder<IrAuthRequirement>();

                foreach (var r in requirements)
                {
                    foreach (var scheme in r.Keys)
                    {
                        var mapped = MapSingle(scheme, r);
                        if (mapped is not IrNoAuth)
                            builder.Add(mapped);
                    }
                }

                // If we couldn’t map anything, fall back to explicit "no auth"
                if (builder.Count == 0)
                    builder.Add(new IrNoAuth());

                return builder.ToImmutable();
            }

            IrAuthRequirement MapSingle(OpenApiSecuritySchemeReference scheme, OpenApiSecurityRequirement r)
            {
                if (scheme is null) return new IrNoAuth();

                var id = scheme.Reference.Id ?? string.Empty;
                var s = _doc.Components?.SecuritySchemes?[id];
                if (s is null) return new IrNoAuth();

                return s.Type switch
                {
                    SecuritySchemeType.Http when s.Scheme == "basic"
                        => new IrBasicAuth(id),

                    SecuritySchemeType.Http when s.Scheme == "bearer"
                        => new IrBearerAuth(id),

                    SecuritySchemeType.ApiKey
                        => new IrApiKeyAuth(
                               id,
                               s.In == ParameterLocation.Header ? IrLocation.Header : IrLocation.Query),

                    SecuritySchemeType.OAuth2
                        => new IrOAuth2Auth(id, [.. r[scheme]]),

                    _ => new IrNoAuth()
                };
            }
        }

        private static IOpenApiSchema Deref(IOpenApiSchema src, OpenApiDocument root)
        {
            return src switch
            {
                OpenApiSchemaReference r when r.Reference.Type == ReferenceType.Schema =>
                    root.Components!.Schemas![r.Reference.Id!],

                _ => src
            };
        }

        private IrType ToIrType(IOpenApiSchema src)
        {
            if (src is OpenApiSchemaReference r && r.Reference.Type == ReferenceType.Schema)
            {
                var compId = r.Reference.Id!;                  
                var target = _doc.Components!.Schemas![compId];

                BuildStruct(compId, target);
                return _ctx.Cache[target];
            }

            var schem = Deref(src, _doc);

            if (_ctx.Cache.TryGetValue(schem, out var hit)) return hit;

            // -------- primitives ----------
            if (schem.Type == JsonSchemaType.String && schem.Format is not "binary" and not "byte")
            {
                var literals = schem.Enum?.Select(e => e.ToString()!).ToImmutableArray() ?? [];

                return _ctx.Cache[schem] = new IrType(IrTypeKind.Scalar, "string", EnumLiterals: literals.Length == 0 ? null : literals);
            }

            if (schem.Type == JsonSchemaType.Integer)
                return _ctx.Cache[schem] = new(IrTypeKind.Scalar, schem.Format == "int64" ? "int64" : "int32");

            if (schem.Type == JsonSchemaType.Number)
                return _ctx.Cache[schem] = new(IrTypeKind.Scalar,
                                   schem.Format == "float" ? "float" : "double");

            if (schem.Type == JsonSchemaType.Boolean)
                return _ctx.Cache[schem] = new(IrTypeKind.Scalar, "bool");

            // -------- array ----------
            if (schem.Type == JsonSchemaType.Array)
            {
                var elem = ToIrType(schem.Items!);
                return _ctx.Cache[schem] = elem with { IsCollection = true };
            }

            // -------- object ----------
            if (schem.Type == JsonSchemaType.Object)
            {
                // Inline object with named props           (optional)
                if (schem?.Properties is { Count: > 0 })
                {
                    // we will ignore inline objects -> map
                    // otherwise we would need to make an inline struct (probably??)
                    return IrType.AnyMap;
                }

                // C. free-form object → map
                return IrType.AnyMap;
            }

            // (no other cases) -------------------------
            throw new InvalidOperationException("Unhandled schema kind");
        }

        private void BuildStruct(string name, IOpenApiSchema schema)
        {
            if (_ctx.NameMap.ContainsKey(schema)) return;
            _ctx.NameMap[schema] = name;

            // Only object-schemas with named properties can become structs.
            if (schema.Type != JsonSchemaType.Object ||   // not an object
                schema.Properties is null ||              // object but no fields
                schema.Properties.Count == 0)
            {
                // Cache it as AnyMap so later references don’t try again.
                _ctx.Cache[schema] = IrType.AnyMap;
                return;
            }

            // Prevent infinite recursion by caching a placeholder *before* walking props
            _ctx.Cache[schema] = new IrType(IrTypeKind.Struct, name);

            var fields = schema.Properties.Select(p =>
                new IrField(
                    p.Key,
                    ToIrType(p.Value),             // primitive / array / map
                    DefaultLiteral: null,
                    Required: schema.Required?.Contains(p.Key) ?? false,
                    p.Value.Description))
                .ToImmutableArray();

            _ctx.Structs.Add(new IrStruct(name, fields));
        }

        private static IrAuthScheme ToAuthScheme(string name, IOpenApiSecurityScheme s) => s.Type switch
        {
            SecuritySchemeType.Http when s.Scheme == "basic"
                => new IrBasicScheme(name),

            SecuritySchemeType.Http when s.Scheme == "bearer"
                => new IrBearerScheme(name),

            SecuritySchemeType.ApiKey
                => new IrApiKeyScheme(name, s.Name!,
                      s.In == ParameterLocation.Header ? IrLocation.Header : IrLocation.Query),

            SecuritySchemeType.OpenIdConnect
                => new IrOpenIdScheme(name, s.OpenIdConnectUrl!.OriginalString),

            SecuritySchemeType.OAuth2
                => new IrOAuth2Scheme(name, GetAllScopes(s.Flows!)),

            _ => throw new NotSupportedException($"Unsupported auth scheme {name}")
        };

        private static List<string> GetAllScopes(OpenApiOAuthFlows flows)
        {
            var list = new List<string>();
            foreach (var f in new[] { flows.AuthorizationCode, flows.ClientCredentials,
                              flows.Implicit, flows.Password })
                if (f?.Scopes is not null)
                    list.AddRange(f.Scopes.Keys);
            return list;
        }

        private sealed class BuildContext
        {
            internal readonly Dictionary<IOpenApiSchema, IrType> Cache = [];
            internal readonly Dictionary<IOpenApiSchema, string> NameMap = [];
            internal readonly List<IrStruct> Structs = [];
            internal readonly List<IrHttpEndpoint> Endpoints = [];
            internal readonly List<IrAuthScheme> AuthSchemes = [];
        }
    }
}
