using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using openapigen.Emitters.Gml;
using openapigen.Helpers;
using openapigen.Model;
using openapigen.Utils;

namespace openapigen.Emitters.Docs
{
    /// <summary>Feather documentation partials for the generated schema structs.</summary>
    public sealed class DocsSchemasEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);
            var resolver = new SchemaResolver(ir);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Schema Documentation (auto-generated, DO NOT EDIT)").Line();
                foreach (var s in ir.Schemas.OfType<IrSchema.Struct>())
                    EmitSchemaDocs(s, w, naming, resolver);

                EmitRequestDocs(w, naming);
            });
        }

        /// <summary>
        /// The request struct is not a schema, but every callback is typed with it, so without a
        /// partial each of those references links to a page that does not exist. Its methods go in
        /// the description: the grammar has <c>@member</c> for values and no equivalent for methods.
        /// </summary>
        private static void EmitRequestDocs(GmlWriter w, GmlNaming n)
        {
            w.JsDoc(b =>
            {
                b.Line($"@struct_partial {n.StructPrefix}Request");
                b.Line("@desc The in-flight HTTP request, handed to every callback as its third argument.");
                b.Line("Call `retry()` on it to send the same request again - useful from a response hook that");
                b.Line("has just refreshed a credential. `get_callback()` returns the callback it will invoke.");
                b.Line($"@member {{Real}} attempts How many times this request has been sent, including retries.");
                b.Line("@struct_end");
            });

            w.Line();
        }

        private static void EmitSchemaDocs(IrSchema.Struct s, GmlWriter w, GmlNaming n, SchemaResolver resolver)
        {
            var structName = n.StructPrefix + s.Name;

            w.JsDoc(b =>
            {
                b.Line($"@struct_partial {structName}");

                foreach (var f in StructSchemaEmitter.BuildFields(s))
                {
                    var jsType = SchemaJsDoc.ToJsDoc(f.Field.Schema, n, resolver, JsDocFlavour.GmExtDocs);

                    // The member name, not f.Arg. @member describes what the struct holds, so it
                    // takes the name the constructor assigns to - "userId", not the "_user_id"
                    // argument that supplies it. The GML side documents the constructor with
                    // @param and correctly uses f.Arg there.
                    var name = f.Field.Required ? f.Field.Name : $"[{f.Field.Name}]";

                    var desc = f.Field.Description ?? string.Empty;
                    b.Line($"@member {{{jsType}}} {name} {desc}".TrimEnd());
                }

                b.Line("@struct_end");
            });

            w.Line();
        }
    }

    /// <summary>Feather documentation partials for the generated endpoint functions.</summary>
    public sealed class DocsFunctionsEmitter(EmitterSettings settings, GmlNaming naming) : IIrEmitter
    {
        public void Emit(IrWebCompilation ir, string root)
        {
            var layout = new EmitterLayout(root, settings);
            var resolver = new SchemaResolver(ir);

            FileEmitHelpers.WriteGml(layout.FullPath, w =>
            {
                w.Section("Endpoint Documentation (auto-generated, DO NOT EDIT)").Line();
                foreach (var ep in ir.Endpoints)
                    EmitEndpointDocs(ep, w, naming, resolver);
            });
        }

        private static void EmitEndpointDocs(IrHttpEndpoint ep, GmlWriter w, GmlNaming n, SchemaResolver resolver)
        {
            // Same builder the GML emitter uses, so the documented signature always matches the
            // generated one.
            var args = EndpointSignature.Build(ep);
            var sig = EndpointSignature.ToGmlParameters(args);
            var fnName = $"{n.Pub}{ep.Name}";

            w.JsDoc(js =>
            {
                js.Line($"@func_partial {fnName}");

                var summary = ep.Description ?? ep.Summary;
                if (!string.IsNullOrEmpty(summary)) js.Description(summary);

                foreach (var a in args)
                {
                    var type = a.Schema is null ? "Any" : SchemaJsDoc.ToJsDoc(a.Schema, n, resolver, JsDocFlavour.GmExtDocs);
                    var name = a.Required && a.Kind == EndpointArgKind.Parameter ? a.Name : $"[{a.Name}]";
                    js.Param(new ParamDoc(name, type, a.Description));
                }

                // With no declared response schema the payload is genuinely unknown, which "Any"
                // states plainly.
                var responseType = ep.ResponseSchema is null
                    ? "Any"
                    : $"{SchemaJsDoc.ToJsDoc(ep.ResponseSchema, n, resolver, JsDocFlavour.GmExtDocs)}|Undefined";

                js.Line("");
                js.Tag("event", "callback");
                js.Tag("member", "{Real} _status");
                js.Tag("member", $"{{{responseType}}} _data");
                js.Tag("member", $"{{Struct.{n.StructPrefix}Request}} _request");
                js.Tag("event_end");
                js.Line("@func_end");
            });

            w.Function(fnName, sig, _ => { }).Line();
        }
    }
}
