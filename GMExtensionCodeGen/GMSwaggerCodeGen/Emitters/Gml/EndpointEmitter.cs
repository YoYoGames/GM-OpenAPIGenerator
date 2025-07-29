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
                if (!string.IsNullOrEmpty(ep.Description)) js.Summary(ep.Description);
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

                if (needsBody) js.Param(new ParamDoc("_body", ep.Body!.Schema.JsDoc(n)));
                if (ctChoice) js.Param(new ParamDoc("_content_type", "String"));
                js.Param(new ParamDoc("_callback", "Function"));
            });

            /* function */
            w.Function(fnName, sig, fn =>
            {
                /* content-type const */
                var ctId = "undefined";
                if (needsBody)
                {
                    ctId = "_content_type";
                    if (!ctChoice)
                        fn.Assign(ctId, $"\"{ep.Body!.DefaultMediaType}\"", VariableScope.Static).Line();
                }

                /* validation */
                fn.Comment("argument validation");
                foreach (var p in ordered)
                    CheckBuilder.Emit(fn, NameUtils.ParamName(p.Name), p.Type, p.Required, n, "_GMFUNCTION_");

                if (needsBody) CheckBuilder.Emit(fn, "_body", ep.Body!.Schema, false, n, "_GMFUNCTION_");
                if (ctChoice) CheckBuilder.Emit(fn, "_content_type", IrType.String, false, n, "_GMFUNCTION_");
                CheckBuilder.Emit(fn, "_callback", IrType.Function, false, n, "_GMFUNCTION_");
                fn.Line();

                /* URL */
                fn.Comment("build url path");
                fn.Assign("_url", $"$\"{{{n.Mac}SERVER_URL}}{CleanPath(ep)}\"", VariableScope.Local).Line();

                /* query params */
                var qs = ep.Parameters.Where(p => p.Location == IrLocation.Query).ToList();
                var paramId = qs.Count == 0 ? "undefined" : "_params";
                if (qs.Count > 0)
                {
                    fn.Comment("create query params struct");
                    fn.Assign(paramId, "{ " + string.Join(", ", qs.Select(p => $"{p.Name} : {NameUtils.ParamName(p.Name)}")) + " }", VariableScope.Local).Line();
                }

                /* security */
                var secId = ep.Auth is IrNoAuth ? "undefined" : "_security";
                if (ep.Auth is not IrNoAuth)
                {
                    fn.Comment("create required security array");
                    fn.Assign(secId, "[ " + string.Join(", ", Schemes(ep.Auth!).Select(s => $"\"{s}\"")) + " ]", VariableScope.Local).Line();
                }

                fn.Return(r => r.Call($"{n.Priv}create_request",
                         ["_url", paramId, $"\"{ep.Verb}\"",
                      needsBody ? "_body" : "undefined",
                      ctId, secId, "_callback", "_GMFUNCTION_"]));
            })
            .Line();
        }

        private static string CleanPath(IrHttpEndpoint ep) =>
            PathVar.Replace(ep.PathTemplate, m => $"{{{NameUtils.ParamName(m.Groups[1].Value)}}}");

        private static IEnumerable<string> Schemes(IrAuthRequirement req) => req switch
        {
            IrNoAuth => [],
            IrBasicAuth b => [b.Name],
            IrBearerAuth b => [b.Name],
            IrApiKeyAuth k => [k.Name],
            IrOAuth2Auth o => [o.Name],
            _ => []
        };
    }

}
