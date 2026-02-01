using codegencore.Model;
using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using GMSwaggerCodeGen.Helpers;
using openapigen.Helpers;
using openapigen.Model;
using System.Text.RegularExpressions;

namespace openapigen.Emitters.Gml
{
    internal static class EndpointEmitter
    {
        private static readonly Regex PathVar = new(@"\{([^}]+)\}", RegexOptions.Compiled);

        private const string BaseUrlVar = "__base_url__";
        private const string UrlVar = "__url__";
        private const string ContentTypeVar = "__content_type__";
        private const string SecurityVar = "__security__";

        public static void Emit(IrHttpEndpoint ep, IrWebCompilation compilation, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(compilation);

            var ordered = ep.Parameters.OrderByDescending(p => p.Required).ToList();
            var sig = ordered.Select(p => p.Required
                    ? NameUtils.ParamName(p.Name)
                    : $"{NameUtils.ParamName(p.Name)} = undefined")
                .ToList();

            bool needsBody = ep.Body is not null;
            bool ctChoice = needsBody && ep.Body!.MediaTypes.Length > 1;

            if (needsBody) sig.Add("_body = undefined");
            if (ctChoice) sig.Add($"_content_type = \"{ep.Body!.MediaTypes[0]}\"");
            sig.Add("_callback = undefined");

            var fnName = $"{n.Pub}{ep.Name}";

            // JSDoc (keep it simple; schema jsdoc can be improved later)
            w.JsDoc(js =>
            {
                js.Line($"@func {fnName}()");
                if (!string.IsNullOrEmpty(ep.Description)) js.Description(ep.Description);

                foreach (var p in ordered)
                    js.Param(new ParamDoc(NameUtils.ParamName(p.Name), "Any", p.Description));

                if (needsBody)
                    js.Param(new ParamDoc("_body", "Any", "Request body.", true));

                if (ctChoice)
                    js.Param(new ParamDoc("_content_type", "String", "Selected content type.", true));

                js.Param(new ParamDoc("_callback", "Function", "Callback (status, data, request).", true));
            });

            w.Function(fnName, sig, fn =>
            {
                // base url
                fn.Assign(BaseUrlVar, $"{n.Priv}options_get_rest_url()", VariableScope.Static).Line();

                // content type
                var ctExpr = "undefined";
                if (needsBody)
                {
                    ctExpr = ContentTypeVar;

                    if (!ctChoice)
                        fn.Assign(ContentTypeVar, $"\"{ep.Body!.MediaTypes[0]}\"", VariableScope.Static).Line();
                    else
                        fn.Assign(ContentTypeVar, "_content_type").Line();
                }

                // validation (THROW-based)
                fn.Comment("argument validation");
                fn.Assign("_where", "_GMFUNCTION_", VariableScope.Local).Line();

                foreach (var p in ordered)
                {
                    var id = NameUtils.ParamName(p.Name);
                    ValueSchemaValidatorEmitter.Emit(fn, id, p.Schema, p.Required, resolver, n, "_where", p.Name);
                }

                if (needsBody)
                    ValueSchemaValidatorEmitter.Emit(fn, "_body", ep.Body!.Schema, required: ep.Body.Required, resolver, n, "_where", "_body");

                if (ctChoice)
                    ValueSchemaValidatorEmitter.Emit(fn, ctExpr, new IrValueSchema.Simple(IrType.String), required: false, resolver, n, "_where", "_content_type");

                // callback optional
                ValueSchemaValidatorEmitter.Emit(fn, "_callback", new IrValueSchema.Simple(IrType.Function), required: false, resolver, n, "_where", "_callback");

                fn.Line();

                // URL
                fn.Comment("build url path");
                fn.Assign(UrlVar, $"$\"{{{BaseUrlVar}}}{CleanPath(ep)}\"", VariableScope.Local).Line();

                // query params
                var qs = ep.Parameters.Where(p => p.Location == IrLocation.Query).ToList();
                var paramExpr = qs.Count == 0 ? "undefined" : "_params";
                if (qs.Count > 0)
                {
                    fn.Comment("create query params struct");
                    fn.Assign(paramExpr, "{ " + string.Join(", ", qs.Select(p => $"{p.Name} : {NameUtils.ParamName(p.Name)}")) + " }", VariableScope.Local).Line();
                }

                // security (OR of AND-sets)
                var secExpr = BuildSecurityExpr(ep.Auth);
                if (secExpr == "undefined")
                {
                    // no auth
                }
                else
                {
                    fn.Comment("create security alternatives (OR of AND)");
                    fn.Assign(SecurityVar, secExpr, VariableScope.Local).Line();
                }

                var secArg = secExpr == "undefined" ? "undefined" : SecurityVar;

                fn.Return(r => r.Call($"{n.Priv}create_request", new[]
                {
                    UrlVar,
                    paramExpr,
                    $"\"{ep.Verb}\"",
                    needsBody ? "_body" : "undefined",
                    ctExpr,
                    secArg,
                    "_callback",
                    "_GMFUNCTION_"
                }));
            }).Line();
        }

        private static string CleanPath(IrHttpEndpoint ep) =>
            PathVar.Replace(ep.PathTemplate, m => $"{{{NameUtils.ParamName(m.Groups[1].Value)}}}");

        private static string BuildSecurityExpr(IrAuthPolicy policy)
        {
            // Canonical "no auth": a single alternative that contains IrAuthRequirement.None
            if (policy.Alternatives.Length == 1 &&
                policy.Alternatives[0].Requirements.Length == 1 &&
                policy.Alternatives[0].Requirements[0] is IrAuthRequirement.None)
                return "undefined";

            // Build: [ ["SchemeA","SchemeB"], ["SchemeC"] ]
            // None inside an alternative means "no auth for that alt" => [] (satisfiable immediately)
            var altExprs = new List<string>();

            foreach (var alt in policy.Alternatives)
            {
                if (alt.Requirements.Any(r => r is IrAuthRequirement.None))
                {
                    altExprs.Add("[]");
                    continue;
                }

                var schemes = alt.Requirements
                    .OfType<IrAuthRequirement.Scheme>()
                    .Select(s => $"\"{s.SchemeName}\"")
                    .ToArray();

                if (schemes.Length == 0)
                    altExprs.Add("[]");
                else
                    altExprs.Add("[ " + string.Join(", ", schemes) + " ]");
            }

            if (altExprs.Count == 0) return "undefined";
            return "[ " + string.Join(", ", altExprs) + " ]";
        }
    }
}
