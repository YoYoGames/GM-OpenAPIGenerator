using CodeGenCore.Ir;
using CodeGenCore.Writers;
using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.CStyle;
using CodeGenCore.Writers.Lang.Gml;
using GMSwaggerCodeGen.Helpers;
using GMSwaggerCodeGen.Ir;
using System.Text.RegularExpressions;

namespace GMSwaggerCodeGen.Emitters.Gml
{

    internal static class EndpointEmitter
    {
        private static readonly Regex PathVar = new(@"\{([^}]+)\}", RegexOptions.Compiled);

        private static readonly string internalBaseUrlField = "__base_url__";
        private static readonly string internalUrlField = "__url__";

        private static readonly string internalContentTypeField = "__content_type__";
        private static readonly string internalSecurityField = "__security__";

        public static void Emit(IrHttpEndpoint ep, ICodeWriter w, GmlNaming n)
        {
            var ordered = ep.Parameters.OrderByDescending(p => p.Required).ToList();
            var sig = ordered.Select(p => p.Required
                                    ? NameUtils.ParamName(p.Name)
                                    : $"{NameUtils.ParamName(p.Name)} = undefined")
                             .ToList();

            bool needsBody = ep.Body is not null;
            bool ctChoice = needsBody && ep.Body!.HasChoice;

            if (needsBody) sig.Add("_body = undefined");
            if (ctChoice) sig.Add($"_content_type = \"{ep.Body!.DefaultMediaType}\"");
            sig.Add("_callback = undefined");

            var fnName = $"{n.Pub}{ep.Name}";

            /* JSDoc */
            w.JsDoc(js =>
            {
                js.Line($"@func {fnName}()");
                if (!string.IsNullOrEmpty(ep.Description)) js.Description(ep.Description);
                foreach (var p in ordered)
                {
                    var desc = p.Description;
                    if (p.Type.IsEnum)
                    {
                        var options = string.Join(" | ", p.Type.EnumLiterals!);
                        desc = $"{desc?.Trim()}( one of: {options}).";
                    }

                    js.Param(new ParamDoc(NameUtils.ParamName(p.Name), p.Type.JsDoc(n), desc));
                }

                var dataType = ep.ResponseSchema?.JsDoc(n) ?? "Undefined";

                if (needsBody) js.Param(new ParamDoc("_body", ep.Body!.Schema.JsDoc(n), "The body to be included in the http request.", true));
                if (ctChoice) js.Param(new ParamDoc("_content_type", "String", "The type of the body (this will be used by the mapper to convert the body argument to the correct type).", true));
                js.Param(new ParamDoc("_callback", "Function", $"The function - with signature (status: real, data: {dataType}, request: Struct.{n.StructPrefix}Request) - that will be executed upon request completion.", true));
            });

            /* function */
            w.Function(fnName, sig, fn =>
            {
                /* base user const */
                fn.Assign(internalBaseUrlField, $"{n.Priv}options_get_rest_url()", VariableScope.Static).Line();

                /* content-type const */
                var ctId = "undefined";
                if (needsBody)
                {
                    ctId = internalContentTypeField;
                    if (!ctChoice)
                        fn.Assign(ctId, $"\"{ep.Body!.DefaultMediaType}\"", VariableScope.Static).Line();
                    else
                        fn.Assign(ctId, $"_content_type").Line();
                }

                /* validation */
                fn.Comment("argument validation");
                foreach (var p in ordered)
                    CheckBuilder.Emit(fn, NameUtils.ParamName(p.Name), p.Type, p.Required, n, "_GMFUNCTION_");

                if (needsBody) CheckBuilder.Emit(fn, "_body", ep.Body!.Schema, false, n, "_GMFUNCTION_", "_body");
                if (ctChoice) CheckBuilder.Emit(fn, ctId, IrType.String, false, n, "_GMFUNCTION_", "_content_type");
                CheckBuilder.Emit(fn, "_callback", IrType.Function, false, n, "_GMFUNCTION_", "_callback");
                fn.Line();

                /* URL */
                fn.Comment("build url path");
                fn.Assign(internalUrlField, $"$\"{{{internalBaseUrlField}}}{CleanPath(ep)}\"", VariableScope.Local).Line();

                /* query params */
                var qs = ep.Parameters.Where(p => p.Location == IrLocation.Query).ToList();
                var paramId = qs.Count == 0 ? "undefined" : "_params";
                if (qs.Count > 0)
                {
                    fn.Comment("create query params struct");
                    fn.Assign(paramId, "{ " + string.Join(", ", qs.Select(p => $"{p.Name} : {NameUtils.ParamName(p.Name)}")) + " }", VariableScope.Local).Line();
                }

                /* security */

                // Rules:
                // - only 1 auth entry and it's IrNoAuth -> no `__security__` declaration, pass `undefined`
                // - otherwise: always declare `__security__` and map IrNoAuth -> undefined, others -> "<name>"

                var authCount = ep.Auth.Length;
                var singleNoAuth = authCount == 1 && ep.Auth[0] is IrNoAuth;

                // If it's a single NoAuth, we never declare `__security__`,
                // and the argument passed to create_request will be the literal `undefined`
                var secId = singleNoAuth ? "undefined" : internalSecurityField;

                if (!singleNoAuth)  // if there are auths
                {
                    fn.Comment("create required security array");
                    // Each entry is already a JS expression: either "undefined" or "\"name\""
                    var entries = ep.Auth
                        .Select(Schemes)   // Schemes returns JS tokens
                        .ToArray();

                    fn.Assign(secId, "[ " + string.Join(", ", entries) + " ]", VariableScope.Local).Line();
                }

                fn.Return(r => r.Call($"{n.Priv}create_request", [internalUrlField, paramId, $"\"{ep.Verb}\"",
                      needsBody ? "_body" : "undefined",
                      ctId, secId, "_callback", "_GMFUNCTION_"]));
            })
            .Line();
        }

        private static string CleanPath(IrHttpEndpoint ep) =>
            PathVar.Replace(ep.PathTemplate, m => $"{{{NameUtils.ParamName(m.Groups[1].Value)}}}");

        private static string Schemes(IrAuthRequirement req) => req switch
        {
            // NoAuth maps to JS `undefined`
            IrNoAuth => "undefined",

            // Others map to JS string literals
            IrBasicAuth b => $"\"{b.Name}\"",
            IrBearerAuth b => $"\"{b.Name}\"",
            IrApiKeyAuth k => $"\"{k.Name}\"",
            IrOAuth2Auth o => $"\"{o.Name}\"",

            _ => string.Empty
        };
    }
}

