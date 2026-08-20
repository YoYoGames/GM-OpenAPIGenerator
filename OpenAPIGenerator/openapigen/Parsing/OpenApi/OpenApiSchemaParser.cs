using codegencore.Model;
using openapigen.Emitters.Gml;
using Microsoft.OpenApi;
using openapigen.Helpers;
using openapigen.Model;
using System.Collections.Immutable;

namespace openapigen.Parsing.OpenApi
{
    internal sealed class OpenApiSchemaParser
    {
        private readonly OpenApiDocument _doc;
        private readonly BuildContext _ctx = new();
        private readonly HashSet<string> _usedNames = new(StringComparer.Ordinal);

        public OpenApiSchemaParser(OpenApiDocument doc)
        {
            _doc = doc;
        }

        // ====================================================================
        // ENTRY
        // ====================================================================

        public IrWebCompilation Build()
        {
            // 0) Reserve the component namespace before anything is built. Inline names are minted
            //    *while* components are being walked, so a name synthesised for one component's
            //    property could otherwise claim a component that has not been registered yet — and
            //    EnsureDeclForComponent would then drop the real one on its ContainsKey guard.
            foreach (var name in _doc.Components?.Schemas?.Keys ?? [])
                _ctx.ReservedSchemaNames.Add(name);

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
            var operations = new List<(string Path, string Verb, OpenApiOperation Op)>();
            foreach (var (path, item) in _doc.Paths)
            {
                if (item.Operations is null) continue;

                foreach (var (verb, op) in item.Operations)
                    operations.Add((path, verb.ToString(), op));
            }

            // Names are assigned in two passes rather than in document order. An author-chosen
            // operationId is public API; a URL-derived name is synthetic. Taking the authored ones
            // first means a synthetic name can never displace one just by appearing earlier in the
            // document.
            var names = new string?[operations.Count];

            for (var i = 0; i < operations.Count; i++)
            {
                var intended = NameUtils.IntendedEndpointFuncName(operations[i].Op.OperationId);
                if (intended.Length > 0)
                    names[i] = Deduplicate(intended);
            }

            for (var i = 0; i < operations.Count; i++)
            {
                if (names[i] is not null) continue;

                var (path, verb, op) = operations[i];
                names[i] = GmlEndpointName.Make(Tags(op).FirstOrDefault() ?? "", verb, path, _usedNames);
            }

            for (var i = 0; i < operations.Count; i++)
            {
                var (path, verb, op) = operations[i];
                _ctx.Endpoints.Add(ToEndpoint(path, verb, op, names[i]!));
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

        private static ImmutableArray<string> Tags(OpenApiOperation op) =>
            op.Tags?.Select(t => t.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .ToImmutableArray() ?? [];

        private IrHttpEndpoint ToEndpoint(string path, string verb, OpenApiOperation op, string opName)
        {
            var parameters = op.Parameters?.Select(ToParam).ToImmutableArray() ?? [];
            var tags = Tags(op);

            return new IrHttpEndpoint(
                Name: opName,
                OperationId: string.IsNullOrWhiteSpace(op.OperationId) ? null : op.OperationId,
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

        /// <summary>
        /// Resolves a name collision so emission always produces unique GML function names.
        /// </summary>
        /// <remarks>
        /// Renaming here is a repair, not an outcome: for an author-chosen <c>operationId</c> it means
        /// the generated public API no longer matches what the spec asked for.
        /// <c>NoDuplicateEndpointNamesRule</c> reports that by comparing each endpoint's name against
        /// <see cref="NameUtils.IntendedEndpointFuncName"/>. Policy stays in the validation layer;
        /// this only keeps the emitted file compilable.
        /// </remarks>
        private string Deduplicate(string name)
        {
            if (_usedNames.Add(name))
                return name;

            var i = 2;
            string candidate;
            do candidate = $"{name}_{i++}";
            while (!_usedNames.Add(candidate));

            return candidate;
        }

        private IrParam ToParam(IOpenApiParameter p)
        {
            var location = p.In switch
            {
                ParameterLocation.Path => IrLocation.Path,
                ParameterLocation.Query => IrLocation.Query,
                ParameterLocation.Header => IrLocation.Header,
                ParameterLocation.Cookie => IrLocation.Cookie,
                _ => throw new NotSupportedException(
                    $"Parameter '{p.Name}' uses unsupported location '{p.In}'. " +
                    "Supported: path, query, header, cookie.")
            };

            // Some real-world specs write path parameters as "{id}" instead of "id". Normalise so
            // one malformed parameter cannot make the whole document ungeneratable.
            var name = NormalizeParamName(p.Name!);

            var schema = EnsureSchema(p.Schema!, $"param_{name}");
            return new IrParam(name, schema, location, p.Required, p.Schema?.Default?.ToString(), p.Description);
        }

        private static string NormalizeParamName(string raw)
        {
            var name = raw?.Trim() ?? string.Empty;

            if (name.Length > 1 && name[0] == '{' && name[^1] == '}')
            {
                var stripped = name[1..^1].Trim();
                Console.Error.WriteLine(
                    $"[openapigen] warning: parameter name '{name}' is brace-wrapped; reading it as '{stripped}'.");
                return stripped;
            }

            return name;
        }

        // ====================================================================
        // REQUEST / RESPONSE
        // ====================================================================

        /// <summary>Media types with a built-in converter, most preferred first.</summary>
        private static readonly string[] ExactSupported =
        {
            "application/json",
            "application/x-www-form-urlencoded",
            "multipart/form-data",
            "text/plain",
            "*/*"
        };

        /// <summary>
        /// RFC 6839 structured suffix. <c>application/merge-patch+json</c>, <c>application/hal+json</c>
        /// and the rest are JSON on the wire and serialise identically, so they are supported as a
        /// family rather than enumerated. They keep their own media type on the request: a server
        /// dispatches on it, and a merge-patch PATCH is not a plain JSON PATCH.
        /// </summary>
        private static bool IsJsonFamily(string mediaType) =>
            mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase) &&
            mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

        private static bool IsSupportedMediaType(string mediaType) =>
            ExactSupported.Contains(mediaType, StringComparer.OrdinalIgnoreCase) || IsJsonFamily(mediaType);

        /// <summary>
        /// Preference rank: exact JSON first, then the +json family, then the remaining exact types in
        /// their declared order.
        /// </summary>
        private static int MediaTypeRank(string mediaType)
        {
            if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (IsJsonFamily(mediaType))
                return 1;

            var i = Array.FindIndex(ExactSupported, m => string.Equals(m, mediaType, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? int.MaxValue : i + 1;
        }

        private IrRequestBody? PickBody(IOpenApiRequestBody rb, string ownerHint)
        {
            var supported = rb.Content?.Where(c => IsSupportedMediaType(c.Key)).ToArray() ?? [];
            if (supported.Length == 0)
            {
                // Don't let the body silently vanish from the generated signature.
                var declared = rb.Content?.Keys ?? [];
                if (declared.Count > 0)
                    Console.Error.WriteLine(
                        $"[openapigen] warning: '{ownerHint}' declares only unsupported body media " +
                        $"type(s) [{string.Join(", ", declared)}]; no body parameter generated. " +
                        $"Supported: {string.Join(", ", ExactSupported)}, and any application/*+json " +
                        $"subtype. Note a media type carrying parameters (\"application/json; charset=utf-8\") " +
                        $"is matched literally and will not be recognised.");
                return null;
            }

            var mediaTypes = supported.Select(s => s.Key).OrderBy(MediaTypeRank).ToImmutableArray();
            var schema = supported.First(s => s.Key == mediaTypes[0]).Value.Schema!;

            return new IrRequestBody(
                mediaTypes,
                EnsureSchema(schema, ownerHint),
                rb.Required
            );
        }

        /// <summary>
        /// Picks the success response schema. Prefers an explicit 2xx (including the "2XX" wildcard),
        /// then falls back to "default" — many specs put the success body there and declare only
        /// error codes explicitly.
        /// </summary>
        private IrValueSchema? PickResponse(OpenApiResponses? reps, string ownerHint)
        {
            if (reps is null) return null;

            return Pick(IsSuccessCode) ?? Pick(c => c.Equals("default", StringComparison.OrdinalIgnoreCase));

            IrValueSchema? Pick(Func<string, bool> codeMatches)
            {
                foreach (var (code, r) in reps)
                {
                    if (!codeMatches(code)) continue;

                    // Same support rule as the request side, and best-ranked rather than whichever
                    // the document happens to list first.
                    var mt = r.Content?
                        .Where(c => IsSupportedMediaType(c.Key))
                        .OrderBy(c => MediaTypeRank(c.Key))
                        .Select(c => c.Value)
                        .FirstOrDefault();

                    if (mt?.Schema is null) continue;

                    return EnsureSchema(mt.Schema, ownerHint);
                }

                return null;
            }
        }

        private static bool IsSuccessCode(string code) =>
            code.Length > 0 && code[0] == '2';

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
            // RFC 7235 auth scheme names are case-insensitive; a spec may legally write "Bearer".
            SecuritySchemeType.Http when IsScheme(s, "basic") => new IrAuthScheme.Basic(name),
            SecuritySchemeType.Http when IsScheme(s, "bearer") => new IrAuthScheme.Bearer(name),
            SecuritySchemeType.Http => throw new NotSupportedException(
                $"Security scheme '{name}' uses http scheme '{s.Scheme}', which is not supported. " +
                "Supported http schemes: basic, bearer."),
            SecuritySchemeType.ApiKey => new IrAuthScheme.ApiKey(name, s.Name!, s.In switch
            {
                ParameterLocation.Header => IrLocation.Header,
                ParameterLocation.Cookie => IrLocation.Cookie,
                _ => IrLocation.Query
            }),
            SecuritySchemeType.OpenIdConnect => new IrAuthScheme.OpenIdConnect(name, s.OpenIdConnectUrl!.OriginalString),
            SecuritySchemeType.OAuth2 => new IrAuthScheme.OAuth2(name, GetAllScopes(s.Flows!)),
            _ => throw new NotSupportedException(
                $"Security scheme '{name}' has unsupported type '{s.Type}'.")
        };

        private static bool IsScheme(IOpenApiSecurityScheme s, string expected) =>
            string.Equals(s.Scheme, expected, StringComparison.OrdinalIgnoreCase);

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
                EnsureDeclForComponent(id, ResolveComponent(id));

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

            if (BareType(s) == JsonSchemaType.Array && s.Items is not null)
            {
                var elemSchema = EnsureSchema(s.Items, ownerHint + "_item");
                return new IrValueSchema.Simple(
                    WithNullability(s, new IrType.Array(ExtractType(elemSchema)))
                );
            }

            // Free-form object ({"type":"object"} with no properties): a plain GML struct map.
            // Declaring a named constructor for it would emit an empty struct plus a no-op validator.
            if (IsFreeFormObject(s))
                return new IrValueSchema.Simple(WithNullability(s, IrType.AnyMap));

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

        /// <summary>
        /// The declared type with JSON Schema's null marker masked off. JsonSchemaType is a [Flags]
        /// enum, and both `nullable: true` and 3.1's ["string","null"] arrive as a union, so a type
        /// test that compares for equality stops recognising a nullable value as the type it is.
        /// Every type test below goes through this.
        /// </summary>
        private static JsonSchemaType? BareType(IOpenApiSchema s)
        {
            if (s.Type is not { } t) return null;

            var bare = t & ~JsonSchemaType.Null;
            return bare == 0 ? null : bare;
        }

        private static bool IsPrimitive(IOpenApiSchema s) =>
            BareType(s) is JsonSchemaType.String or JsonSchemaType.Integer or JsonSchemaType.Number or JsonSchemaType.Boolean;

        private static bool IsSimplePrimitive(IOpenApiSchema s) =>
            IsPrimitive(s) && (s.Enum is null || s.Enum.Count == 0);

        private static bool IsObjectLike(IOpenApiSchema s) =>
            BareType(s) == JsonSchemaType.Object || s.Properties is { Count: > 0 };

        /// <summary>An object with no declared properties and no typed additionalProperties.</summary>
        private static bool IsFreeFormObject(IOpenApiSchema s) =>
            BareType(s) == JsonSchemaType.Object
            && s.Properties is not { Count: > 0 }
            && s.AdditionalProperties is null
            && s.Enum is not { Count: > 0 };

        /// <summary>
        /// True when the declared type union includes null — `nullable: true` in 3.0, or an explicit
        /// "null" member in a 3.1 type array.
        /// </summary>
        private static bool IsNullable(IOpenApiSchema s) =>
            s.Type is { } declared && declared.HasFlag(JsonSchemaType.Null);

        /// <summary>
        /// Carries declared nullability into the IR rather than discarding it. Nullability is part of
        /// the contract the spec states, and <see cref="IrType.Nullable"/> is what both the validator
        /// emitter and the Feather renderer key off to relax a presence check.
        /// </summary>
        private static IrType WithNullability(IOpenApiSchema s, IrType t) =>
            IsNullable(s) ? IrType.MakeNullable(t) : t;

        private static IrType ToPrimitiveType(IOpenApiSchema s) => WithNullability(s, BareType(s) switch
        {
            JsonSchemaType.Boolean => IrType.Bool,
            JsonSchemaType.Integer => s.Format == "int64" ? IrType.Int64 : IrType.Int32,
            JsonSchemaType.Number => s.Format == "float" ? IrType.Float : IrType.Double,
            JsonSchemaType.String => s.Format is "binary" or "byte" ? IrType.Buffer : IrType.String,
            _ => IrType.Any
        });

        private IOpenApiSchema Deref(IOpenApiSchema s) =>
            s is OpenApiSchemaReference r ? ResolveComponent(r.Reference.Id!) : s;

        /// <summary>
        /// Resolves a "#/components/schemas/{id}" reference, naming the offender when it dangles —
        /// a missing component otherwise surfaces as a bare NullReferenceException.
        /// </summary>
        private IOpenApiSchema ResolveComponent(string id)
        {
            if (_doc.Components?.Schemas is not { } schemas)
                throw new InvalidOperationException(
                    $"Schema '#/components/schemas/{id}' is referenced but the document declares no components/schemas.");

            if (!schemas.TryGetValue(id, out var schema) || schema is null)
                throw new InvalidOperationException(
                    $"Unresolved reference '#/components/schemas/{id}'. " +
                    "Check the spelling, or that the component is declared in this document.");

            return schema;
        }

        /// <summary>
        /// Names an inline (anonymous) schema from its owner hint, normalised to PascalCase so the
        /// emitted constructor reads as a type: "uploadAvatar_body" becomes "UploadAvatarBody".
        /// </summary>
        /// <remarks>
        /// The counter is advanced past any name already taken by a declared component or an earlier
        /// inline schema. Without that, a hint colliding with a component overwrote it — or, when the
        /// inline was minted first, made the real component get dropped by
        /// <see cref="EnsureDeclForComponent"/>'s re-entry guard. An inline name is invented by this
        /// tool rather than chosen by the spec author, so shifting it is silent by design: there is no
        /// contract to break and nothing the user could do about it.
        /// </remarks>
        private string MakeInlineName(string hint)
        {
            var baseName = ToPascalCase(hint);
            if (baseName.Length == 0)
                baseName = "Anonymous";

            var n = _ctx.InlineCounters.TryGetValue(baseName, out var seen) ? seen : 0;

            string candidate;
            do
            {
                candidate = n == 0 ? baseName : $"{baseName}{n + 1}";
                n++;
            }
            while (_ctx.ReservedSchemaNames.Contains(candidate) || _ctx.Decls.ContainsKey(candidate));

            _ctx.InlineCounters[baseName] = n;
            _ctx.ReservedSchemaNames.Add(candidate);
            return candidate;
        }

        private static string ToPascalCase(string raw)
        {
            var words = NameUtils.ToSnake(raw).Split('_', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        }

        private static IrAuthPolicy NoAuthPolicy => new([new IrAuthRequirementSet([new IrAuthRequirement.None()])]);

        private sealed class BuildContext
        {
            internal readonly Dictionary<string, IrSchema> Decls = new();

            /// <summary>
            /// Every schema name spoken for — declared components (seeded before anything is built)
            /// plus inline names already minted. Guards <see cref="MakeInlineName"/>.
            /// </summary>
            internal readonly HashSet<string> ReservedSchemaNames = new(StringComparer.Ordinal);

            internal readonly Dictionary<IOpenApiSchema, string> InlineNames = new();
            internal readonly Dictionary<string, int> InlineCounters = new();
            internal readonly List<IrHttpEndpoint> Endpoints = new();
            internal readonly List<IrAuthScheme> AuthSchemes = new();
        }
    }
}
