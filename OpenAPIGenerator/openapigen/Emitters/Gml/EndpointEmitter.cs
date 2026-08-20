using codegencore.Model;
using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;
using System.Text.RegularExpressions;

namespace openapigen.Emitters.Gml
{
    internal static class EndpointEmitter
    {
        private static readonly Regex PathVar = new(@"\{([^}]+)\}", RegexOptions.Compiled);

        // Generator-owned temporaries use __name__ so a spec parameter can never shadow them.
        private const string BaseUrlVar = "__base_url__";
        private const string UrlVar = "__url__";
        private const string ContentTypeVar = "__content_type__";
        private const string SecurityVar = "__security__";
        private const string ParamsVar = "__params__";
        private const string HeadersVar = "__headers__";
        private const string WhereVar = "__where__";

        public static void Emit(IrHttpEndpoint ep, IrWebCompilation compilation, GmlWriter w, GmlNaming n)
        {
            var resolver = new SchemaResolver(compilation);
            var args = EndpointSignature.Build(ep);
            var sig = EndpointSignature.ToGmlParameters(args);

            var fnName = $"{n.Pub}{ep.Name}";

            var body = args.FirstOrDefault(a => a.Kind == EndpointArgKind.Body);
            var contentType = args.FirstOrDefault(a => a.Kind == EndpointArgKind.ContentType);

            w.JsDoc(js =>
            {
                js.Line($"@func {fnName}({string.Join(", ", sig)})");

                var summary = ep.Description ?? ep.Summary;
                if (!string.IsNullOrEmpty(summary)) js.Description(summary);

                foreach (var a in args)
                {
                    var type = a.Schema is null ? "Any" : SchemaJsDoc.ToJsDoc(a.Schema, n, resolver);
                    var name = a.Required && a.Kind == EndpointArgKind.Parameter ? a.Name : $"[{a.Name}]";
                    js.Param(new ParamDoc(name, type, a.Description));
                }
            });

            w.Function(fnName, sig, fn =>
            {
                fn.Assign(BaseUrlVar, $"{n.Priv}options_get_rest_url()", VariableScope.Local).Line();

                var ctExpr = "undefined";
                if (body is not null)
                {
                    ctExpr = ContentTypeVar;

                    if (contentType is null)
                        fn.Assign(ContentTypeVar, $"\"{ep.Body!.MediaTypes[0]}\"", VariableScope.Local).Line();
                    else
                        fn.Assign(ContentTypeVar, EndpointSignature.ContentTypeArg, VariableScope.Local).Line();
                }

                fn.Comment("argument validation");
                fn.Assign(WhereVar, "_GMFUNCTION_", VariableScope.Local).Line();

                foreach (var a in args)
                {
                    if (a.Schema is null) continue;

                    // The content-type is validated through its temporary, which is what the rest
                    // of the body path uses.
                    var expr = a.Kind == EndpointArgKind.ContentType ? ContentTypeVar : a.Name;

                    // An argument with a default is never actually absent, but validating it as
                    // optional keeps an explicit `undefined` from throwing.
                    var required = a.Required && a.DefaultLiteral is null;

                    ValueSchemaValidatorEmitter.Emit(fn, expr, a.Schema, required, resolver, n, WhereVar, a.SpecName);
                }

                fn.Line();

                fn.Comment("build url path");
                fn.Assign(UrlVar, $"$\"{{{BaseUrlVar}}}{CleanPath(ep, args, n)}\"", VariableScope.Local).Line();

                var paramExpr = EmitStructArg(fn, args, IrLocation.Query, ParamsVar, "create query params struct", resolver);
                var headerExpr = EmitStructArg(fn, args, IrLocation.Header, HeadersVar, "create header params struct", resolver);

                var secExpr = BuildSecurityExpr(ep.Auth);
                if (secExpr != "undefined")
                    fn.Assign(SecurityVar, secExpr, VariableScope.Local).Line();

                var secArg = secExpr == "undefined" ? "undefined" : SecurityVar;

                fn.Return(r => r.Call($"{n.Priv}create_request", new[]
                {
                    UrlVar,
                    paramExpr,
                    $"\"{ep.Verb}\"",
                    headerExpr,
                    body is not null ? EndpointSignature.BodyArg : "undefined",
                    ctExpr,
                    secArg,
                    "undefined",
                    EndpointSignature.CallbackArg,
                    "_GMFUNCTION_"
                }));
            }).Line();
        }

        /// <summary>
        /// Emits a struct literal collecting all arguments at one location, or returns "undefined"
        /// when there are none. Keys that are not valid GML identifiers are quoted.
        /// </summary>
        private static string EmitStructArg(
            GmlWriter fn,
            IReadOnlyList<EndpointArg> args,
            IrLocation location,
            string varName,
            string comment,
            SchemaResolver resolver)
        {
            var matching = args.Where(a => a.Location == location).ToList();
            if (matching.Count == 0)
                return "undefined";

            var entries = matching.Select(a => $"{StructKey(a.SpecName)} : {ValueExpr(a, resolver)}");

            fn.Comment(comment);
            fn.Assign(varName, "{ " + string.Join(", ", entries) + " }", VariableScope.Local).Line();

            return varName;
        }

        /// <summary>
        /// The expression written into a query or header struct for one argument. Booleans are spelt
        /// out because `string(true)` is "1" in GML. Driven by the declared type, not a runtime
        /// `is_bool`: a boolean argument also accepts 1 and 0, which would then go out differently.
        /// </summary>
        private static string ValueExpr(EndpointArg a, SchemaResolver resolver)
        {
            if (!IsBool(a.Schema, resolver))
                return a.Name;

            var spelled = $"({a.Name} ? \"true\" : \"false\")";

            // Only a required argument without a default can never arrive undefined. For every other
            // one, undefined has to survive: it is how _build_url and the header loop know to skip a
            // parameter rather than send it empty.
            return a.Required && a.DefaultLiteral is null
                ? spelled
                : $"(is_undefined({a.Name}) ? undefined : {spelled})";
        }

        /// <summary>True when the declared type is a boolean, nullable or not.</summary>
        private static bool IsBool(IrValueSchema? schema, SchemaResolver resolver)
        {
            if (schema is null || resolver.Unalias(schema) is not IrValueSchema.Simple simple)
                return false;

            var type = simple.Type is IrType.Nullable nullable ? nullable.Underlying : simple.Type;
            return type is IrType.Builtin { Kind: BuiltinKind.Bool };
        }

        /// <summary>
        /// A struct-literal key must be a bare identifier or a quoted string; header names such as
        /// "X-Trace" and query keys such as "filter[id]" are only legal in quoted form.
        /// </summary>
        private static string StructKey(string name) =>
            NameUtils.IsValidIdent(name) ? name : $"\"{name.Replace("\"", "\\\"")}\"";

        /// <summary>
        /// Substitutes path placeholders with their GML arguments, URL-encoding each value.
        /// </summary>
        private static string CleanPath(IrHttpEndpoint ep, IReadOnlyList<EndpointArg> args, GmlNaming n) =>
            PathVar.Replace(ep.PathTemplate, m =>
            {
                var specName = m.Groups[1].Value;
                var arg = args.FirstOrDefault(a =>
                    a.Location == IrLocation.Path &&
                    string.Equals(a.SpecName, specName, StringComparison.Ordinal));

                var expr = arg?.Name ?? NameUtils.ParamName(specName);
                return $"{{{n.Priv}url_encode({expr})}}";
            });

        private static string BuildSecurityExpr(IrAuthPolicy policy)
        {
            // Canonical "no auth": a single alternative containing IrAuthRequirement.None
            if (policy.Alternatives.Length == 1 &&
                policy.Alternatives[0].Requirements.Length == 1 &&
                policy.Alternatives[0].Requirements[0] is IrAuthRequirement.None)
                return "undefined";

            // Flatten to a unique list of scheme names - _apply_auth switches on each element as a string
            var names = policy.Alternatives
                .SelectMany(alt => alt.Requirements.OfType<IrAuthRequirement.Scheme>())
                .Select(s => $"\"{s.SchemeName}\"")
                .Distinct()
                .ToArray();

            return names.Length == 0 ? "undefined" : "[ " + string.Join(", ", names) + " ]";
        }
    }
}
