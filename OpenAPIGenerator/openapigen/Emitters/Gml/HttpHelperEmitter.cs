using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;

namespace openapigen.Emitters.Gml
{
    internal static class HttpHelperEmitter
    {
        public static void Emit(IrWebCompilation ir, GmlWriter w, GmlNaming n)
        {
            EmitOptions(w, n);
            w.Line();
            EmitUrlEncode(w, n);
            w.Line();
            EmitUtils(w, ir, n);
            w.Line();
            EmitRequest(w, ir, n);
        }

        private static void EmitOptions(GmlWriter w, GmlNaming n)
        {
            // Read per call: caching in a static would pin the URL for the whole session, so a game
            // that switches environment at runtime could never change it.
            w.JsDoc(b => b.Returns("String"))
             .Function($"{n.Priv}options_get_rest_url", [], fn =>
             {
                 fn.Return($"extension_get_option_value(\"{n.StructPrefix}\", \"server_rest_url\")");
             }).Line();

            w.JsDoc(b => b.Returns("Bool"))
             .Function($"{n.Priv}options_is_debug", [], fn =>
             {
                 fn.Return($"bool(extension_get_option_value(\"{n.StructPrefix}\", \"debug_logging\"))");
             }).Line();
        }

        /// <summary>
        /// Percent-encodes a value for use in a URL path segment or query value. Unreserved
        /// characters (RFC 3986 2.3) pass through; everything else is encoded from its UTF-8 bytes.
        /// </summary>
        private static void EmitUrlEncode(GmlWriter w, GmlNaming n)
        {
            w.JsDoc(b => b
                    .Param(new ParamDoc("__value__", "Any", "Value to percent-encode."))
                    .Returns("String")
                    .Tag("ignore"))
             .Function($"{n.Priv}url_encode", ["__value__"], fn =>
             {
                 fn.If("is_undefined(__value__)", ifBody => ifBody.Return("\"\"")).Line();

                 fn.Assign("__text__", "string(__value__)", VariableScope.Local);
                 fn.Assign("__buffer__", "buffer_create(string_byte_length(__text__) + 1, buffer_fixed, 1)", VariableScope.Local);
                 fn.Line("buffer_write(__buffer__, buffer_text, __text__);");
                 fn.Assign("__size__", "buffer_tell(__buffer__)", VariableScope.Local);
                 fn.Assign("__out__", "\"\"", VariableScope.Local).Line();

                 fn.For("var __i__ = 0", "__i__ < __size__", "__i__++", loop =>
                 {
                     loop.Assign("__byte__", "buffer_peek(__buffer__, __i__, buffer_u8)", VariableScope.Local);
                     loop.If(
                         "(__byte__ >= 48 && __byte__ <= 57) || (__byte__ >= 65 && __byte__ <= 90) " +
                         "|| (__byte__ >= 97 && __byte__ <= 122) || __byte__ == 45 || __byte__ == 46 " +
                         "|| __byte__ == 95 || __byte__ == 126",
                         ifBody => ifBody.Line("__out__ += chr(__byte__);"),
                         elseBody =>
                         {
                             elseBody.Assign("__hex__", "\"0123456789ABCDEF\"", VariableScope.Local);
                             elseBody.Line("__out__ += \"%\" + string_char_at(__hex__, (__byte__ >> 4) + 1) " +
                                           "+ string_char_at(__hex__, (__byte__ & 15) + 1);");
                         });
                 }).Line();

                 fn.Line("buffer_delete(__buffer__);");
                 fn.Return("__out__");
             }).Line();
        }

        private static void EmitUtils(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            var schemeList = string.Join(", ", ir.AuthSchemes.Select(s => $"\"{s.Name}\""));

            w.JsDoc(b => b
                .Param(new ParamDoc("__where__", "String", "Caller location for error messages."))
                .Returns("Id.Instance")
                .Tag("ignore"))
             .Function($"{n.Priv}get_singleton", ["__where__"], fn =>
             {
                 fn.Assign("__singleton__", $"instance_create_depth(0, 0, 0, obj{n.Priv}core)", VariableScope.Static);
                 fn.With("__singleton__", inner => inner.Return("self"));
                 fn.Line($"show_error($\"{{__where__}} :: Failed to get the obj{n.Priv}core singleton.\", true);");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_token_id", "String", $"One of: {schemeList}"))
                .Param(new ParamDoc("_token", "String", null)))
             .Function($"{n.Pub}request_auth_set_token", ["_token_id", "_token"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("__instance__.auth_tokens", AccessorKind.Struct, "_token_id"), "_token");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_token_id", "String", $"One of: {schemeList}"))
                .Returns("String"))
             .Function($"{n.Priv}request_auth_get_token", ["_token_id"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("__instance__.auth_tokens", AccessorKind.Struct, "_token_id"));
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_content_type", "String", null))
                .Param(new ParamDoc("_function", "Function", "function(_body, _header_ds_map) → String|Id.Buffer")))
             .Function($"{n.Pub}request_body_set_converter", ["_content_type", "_function"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("__instance__.type_converters", AccessorKind.Struct, "_content_type"), "_function");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_content_type", "String", null))
                .Returns("Function"))
             .Function($"{n.Priv}request_body_get_converter", ["_content_type"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("__instance__.type_converters", AccessorKind.Struct, "_content_type"));
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_code", "Real", null))
                .Param(new ParamDoc("_hook", "Function", null)))
             .Function($"{n.Pub}request_response_set_hook", ["_code", "_hook"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("__instance__.response_hooks", AccessorKind.Map, "_code"), "_hook");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_code", "Real", null))
                .Returns("Function"))
             .Function($"{n.Priv}request_response_get_hook", ["_code"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("__instance__.response_hooks", AccessorKind.Map, "_code"));
             }).Line();
        }

        private static void EmitRequest(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.JsDoc(b => b
                .Param(new ParamDoc("_url",          "String",           null))
                .Param(new ParamDoc("_params",        "Struct|Undefined", null))
                .Param(new ParamDoc("_method",        "String",           null))
                .Param(new ParamDoc("_headers",       "Struct|Undefined", "Per-request header parameters from the spec."))
                .Param(new ParamDoc("_body",          "Any",              null))
                .Param(new ParamDoc("_content_type",  "String|Undefined", null))
                .Param(new ParamDoc("_security",      "Array|Undefined",  null))
                .Param(new ParamDoc("_cookies",       "Struct|Undefined", "Per-request cookies merged with the cookie jar on send."))
                .Param(new ParamDoc("_callback",      "Function",         null))
                .Param(new ParamDoc("__where__",         "String",           null)))
             .Struct($"{n.StructPrefix}Request",
                ["_url", "_params", "_method", "_headers", "_body", "_content_type", "_security", "_cookies", "_callback", "__where__"],
                body =>
                {
                    body.Assign("__", w => w.StructLiteral([
                        new("url",          "_url"),
                        new("params",       "_params"),
                        new("http_method",  "_method"),
                        new("headers",      "_headers"),
                        new("content_type", "_content_type"),
                        new("raw_body",     "undefined"),
                        new("callback",     "_callback"),
                        new("security",     "_security"),
                        new("cookies",      "_cookies"),
                        new("where",        "__where__"),
                    ], multiline: true)).Line();

                    body.Assign("attempts", "0").Line();

                    body.JsDoc(b => b.Returns("Function"))
                        .Assign("get_callback", w => w.Method([], fn =>
                        {
                            fn.Return("__.callback");
                        }), VariableScope.Static).Line();

                    body.JsDoc(b => b.Returns("Real"))
                        .Assign("send", w => w.Method([], fn =>
                        {
                            fn.Assign("__id__",   "-1",   VariableScope.Local)
                              .Assign("__self__", "self", VariableScope.Local);

                            fn.With("__", inner =>
                            {
                                inner.Assign("__params__", "params ?? {}", VariableScope.Local)
                                     .Assign("__header__", "ds_map_create()", VariableScope.Local).Line();

                                // Endpoint header parameters go in first so authentication, which
                                // the caller does not control, always wins on a name clash.
                                inner.Comment("endpoint header parameters");
                                inner.If("!is_undefined(headers)", ifBody =>
                                {
                                    ifBody.Assign("__header_keys__", "struct_get_names(headers)", VariableScope.Local);
                                    ifBody.Assign("__header_count__", "array_length(__header_keys__)", VariableScope.Local);
                                    ifBody.For("var __i__ = 0", "__i__ < __header_count__", "__i__++", loop =>
                                    {
                                        loop.Assign("__k__", "__header_keys__[__i__]", VariableScope.Local);
                                        loop.Assign("__v__", "headers[$ __k__]", VariableScope.Local);
                                        loop.If("!is_undefined(__v__)", set =>
                                            set.Line("__header__[? __k__] = string(__v__);"));
                                    });
                                }).Line();

                                inner.Comment("inject security");
                                inner.Assign("__sec_count__", "is_array(security) ? array_length(security) : 0", VariableScope.Local);
                                inner.For("var __i__ = 0", "__i__ < __sec_count__", "__i__++", loop =>
                                    loop.Line($"__self__._apply_auth(__header__, __params__, security[__i__], where);")).Line();

                                inner.Assign("__instance__", $"{n.Priv}get_singleton(where)", VariableScope.Local).Line();

                                inner.Comment("cookies: jar entries first, then per-request overrides");
                                inner.Assign("__cookie_parts__", "[]",              VariableScope.Local)
                                     .Assign("__j__",            "0",              VariableScope.Local)
                                     .Assign("__jar_keys__",   "struct_get_names(__instance__.cookie_jar)", VariableScope.Local)
                                     .Assign("__jar_count__",  "array_length(__jar_keys__)", VariableScope.Local);
                                inner.For("var __i__ = 0", "__i__ < __jar_count__", "__i__++", loop =>
                                {
                                    loop.Assign("__k__", "__jar_keys__[__i__]", VariableScope.Local)
                                        .Line("__cookie_parts__[__j__++] = $\"{__k__}={__instance__.cookie_jar[$ __k__]}\";");
                                });
                                inner.If("!is_undefined(cookies)", ifBody =>
                                {
                                    ifBody.Assign("__exp_keys__",  "struct_get_names(cookies)",    VariableScope.Local)
                                          .Assign("__exp_count__", "array_length(__exp_keys__)",      VariableScope.Local);
                                    ifBody.For("var __i__ = 0", "__i__ < __exp_count__", "__i__++", loop =>
                                    {
                                        loop.Assign("__k__", "__exp_keys__[__i__]", VariableScope.Local)
                                            .Line("__cookie_parts__[__j__++] = $\"{__k__}={cookies[$ __k__]}\";");
                                    });
                                });
                                inner.If("__j__ > 0", ifBody =>
                                    ifBody.Line("__header__[? \"Cookie\"] = string_join_ext(\"; \", __cookie_parts__, 0, __j__);")).Line();

                                inner.Assign("__url__", "__self__._build_url(url, __params__)", VariableScope.Local).Line();

                                inner.If("!is_undefined(raw_body)", ifBody =>
                                {
                                    ifBody.Comment("set Content-Type before converter so it can override (e.g. multipart boundary)");
                                    ifBody.Line("__header__[? \"Content-Type\"] = content_type;");
                                    ifBody.Assign("__processed__", "__self__._process_body(raw_body, content_type, __header__, where)", VariableScope.Local);
                                    ifBody.Line("__id__ = http_request(__url__, http_method, __header__, __processed__);");
                                    ifBody.If("!is_string(__processed__)", elseBody =>
                                        elseBody.Line("buffer_delete(__processed__);"));
                                }, elseBody =>
                                {
                                    elseBody.Line("__id__ = http_request(__url__, http_method, __header__, \"\");");
                                });
                                inner.Line();

                                inner.Line("__instance__.requests[? __id__] = __self__;");
                                inner.Line("ds_map_destroy(__header__);");
                            });

                            fn.Line("attempts++;");
                            fn.Return("__id__");
                        }), VariableScope.Static).Line();

                    body.JsDoc(b => b.Returns("Real"))
                        .Assign("retry", w => w.Method([], fn => fn.Return("send()")), VariableScope.Static).Line();

                    body.JsDoc(b => b
                            .Param(new ParamDoc("_body",         "Any",       null))
                            .Param(new ParamDoc("_content_type", "String",    null))
                            .Param(new ParamDoc("_header",       "Id.DsMap",  "Converter may mutate this (e.g. multipart sets boundary)."))
                            .Param(new ParamDoc("__where__",        "String",    null))
                            .Returns("String|Id.Buffer")
                            .Tag("ignore"))
                        .Assign("_process_body", w => w.Method(["_body", "_content_type", "_header", "__where__"], fn =>
                        {
                            fn.Assign("__conv__", $"{n.Priv}request_body_get_converter(_content_type)", VariableScope.Local);
                            fn.If("!is_callable(__conv__)", ifBody =>
                                ifBody.Line($"show_error($\"{{__where__}} :: No converter for '{{_content_type}}'.\", true);"));
                            fn.Line("_body = __conv__(_body, _header);");
                            fn.If("!is_string(_body) && (!is_handle(_body) || !string_starts_with(string(_body), \"ref buffer\"))", ifBody =>
                                ifBody.Line($"show_error($\"{{__where__}} :: Body converter must return a string or buffer.\", true);"));
                            fn.Line("// feather ignore once GM1045");
                            fn.Return("_body");
                        }), VariableScope.Static).Line();

                    body.JsDoc(b => b
                            .Param(new ParamDoc("_url_base", "String",           null))
                            .Param(new ParamDoc("_params",   "Struct|Undefined", null, Optional: true))
                            .Returns("String")
                            .Tag("ignore"))
                        .Assign("_build_url", w => w.Method(["_url_base", "_params = undefined"], fn =>
                        {
                            fn.If("is_undefined(_params)", ifBody => ifBody.Return("_url_base")).Line();

                            fn.Assign("__pairs__",  "[]",                        VariableScope.Local)
                              .Assign("__n__",      "0",                         VariableScope.Local)
                              .Assign("__keys__",   "struct_get_names(_params)", VariableScope.Local)
                              .Assign("__count__",  "array_length(__keys__)",    VariableScope.Local).Line();

                            fn.For("var __i__ = 0", "__i__ < __count__", "__i__++", loop =>
                            {
                                loop.Assign("__key__",   "__keys__[__i__]",                    VariableScope.Local)
                                    .Assign("__value__", "struct_get(_params, __key__)",       VariableScope.Local);

                                // An undefined value means "not supplied": leave it out entirely.
                                loop.If("is_undefined(__value__)", ifBody => ifBody.Line("continue;"));

                                loop.Assign("__enc_key__", $"{n.Priv}url_encode(__key__)", VariableScope.Local);

                                // Array values repeat the key, which is the common convention.
                                loop.If("is_array(__value__)", arrBody =>
                                {
                                    arrBody.Assign("__alen__", "array_length(__value__)", VariableScope.Local);
                                    arrBody.For("var __a__ = 0", "__a__ < __alen__", "__a__++", inner =>
                                        inner.Line($"__pairs__[__n__++] = $\"{{__enc_key__}}={{{n.Priv}url_encode(__value__[__a__])}}\";"));
                                }, elseBody =>
                                    elseBody.Line($"__pairs__[__n__++] = $\"{{__enc_key__}}={{{n.Priv}url_encode(__value__)}}\";"));
                            }).Line();

                            fn.If("__n__ == 0", ifBody => ifBody.Return("_url_base"));

                            // Preserve any query string the base URL already carries.
                            fn.Assign("__sep__", "string_pos(\"?\", _url_base) == 0 ? \"?\" : \"&\"", VariableScope.Local);
                            fn.Return("_url_base + __sep__ + string_join_ext(\"&\", __pairs__, 0, __n__)");
                        }), VariableScope.Static).Line();

                    body.JsDoc(b => b
                            .Param(new ParamDoc("_header", "Id.DsMap", null))
                            .Param(new ParamDoc("_params", "Struct",   null))
                            .Param(new ParamDoc("_scheme", "String",   null))
                            .Param(new ParamDoc("__where__",  "String",   null))
                            .Tag("ignore"))
                        .Assign("_apply_auth", w => w.Method(["_header", "_params", "_scheme", "__where__"], fn =>
                        {
                            fn.Assign("missing", w => w.Method(["__where__", "_token"], inner =>
                                inner.Line("show_debug_message($\"{__where__} :: missing credential for '{_token}', skipping auth.\");")),
                                VariableScope.Static).Line();

                            fn.Switch("_scheme", sw =>
                            {
                                foreach (var s in ir.AuthSchemes)
                                {
                                    var sn = s.Name;
                                    var snIdent = System.Text.RegularExpressions.Regex.Replace(sn, @"[^A-Za-z0-9_]", "_");
                                    sw.Case($"\"{sn}\"", caseBody =>
                                    {
                                        caseBody.Assign($"__{snIdent}_token__", $"{n.Priv}request_auth_get_token(\"{sn}\")", VariableScope.Local);
                                        caseBody.If($"is_undefined(__{snIdent}_token__)", ifBody =>
                                        {
                                            ifBody.Line($"missing(__where__, \"{sn}\");");
                                            ifBody.Line("break;");
                                        });

                                        switch (s)
                                        {
                                            case IrAuthScheme.Basic:
                                                caseBody.Line($"_header[? \"Authorization\"] = $\"Basic {{base64_encode(__{snIdent}_token__)}}\";");
                                                break;
                                            case IrAuthScheme.Bearer:
                                            case IrAuthScheme.OAuth2:
                                            case IrAuthScheme.OpenIdConnect:
                                                caseBody.Line($"_header[? \"Authorization\"] = $\"Bearer {{__{snIdent}_token__}}\";");
                                                break;
                                            case IrAuthScheme.ApiKey apiKey when apiKey.In == IrLocation.Header:
                                                caseBody.Line($"_header[? \"{apiKey.ParamName}\"] = __{snIdent}_token__;");
                                                break;
                                            case IrAuthScheme.ApiKey apiKey when apiKey.In == IrLocation.Query:
                                                caseBody.Line($"_params[$ \"{apiKey.ParamName}\"] = __{snIdent}_token__;");
                                                break;
                                            case IrAuthScheme.ApiKey apiKey when apiKey.In == IrLocation.Cookie:
                                                // Appended to whatever the cookie jar already set.
                                                caseBody.Assign("__existing__", "_header[? \"Cookie\"]", VariableScope.Local);
                                                caseBody.Line(
                                                    $"_header[? \"Cookie\"] = is_undefined(__existing__) " +
                                                    $"? $\"{apiKey.ParamName}={{__{snIdent}_token__}}\" " +
                                                    $": $\"{{__existing__}}; {apiKey.ParamName}={{__{snIdent}_token__}}\";");
                                                break;
                                        }
                                    });
                                }

                                sw.Case("undefined", caseBody => { });
                                sw.Default(d =>
                                    d.Line("show_debug_message($\"{__where__} :: No auth rule for '{_scheme}'.\");"));
                            });
                        }), VariableScope.Static).Line();

                    body.Comment("body processing deferred to send() so the converter can see the live header");
                    body.If("!is_undefined(_body)", ifBody =>
                        ifBody.Line("__.raw_body = _body;"));
                }).Line();

            EmitCreateRequest(w, n);
            w.Line();
            EmitCookieApi(w, n);
        }

        private static void EmitCreateRequest(GmlWriter w, GmlNaming n)
        {
            w.JsDoc(b => b
                .Param(new ParamDoc("_url",          "String",           null))
                .Param(new ParamDoc("_params",        "Struct|Undefined", null))
                .Param(new ParamDoc("_method",        "String",           null))
                .Param(new ParamDoc("_headers",       "Struct|Undefined", "Header parameters declared by the endpoint."))
                .Param(new ParamDoc("_body",          "Any",              null))
                .Param(new ParamDoc("_content_type",  "String|Undefined", null))
                .Param(new ParamDoc("_security",      "Array|Undefined",  null))
                .Param(new ParamDoc("_cookies",       "Struct|Undefined", null))
                .Param(new ParamDoc("_callback",      "Function",         null))
                .Param(new ParamDoc("__where__",         "String",           null))
                .Returns("Real")
                .Tag("ignore"))
             .Function($"{n.Priv}create_request",
                ["_url", "_params", "_method", "_headers", "_body", "_content_type", "_security", "_cookies", "_callback", "__where__"],
                fn =>
                {
                    fn.Assign("__req__", $"new {n.StructPrefix}Request(_url, _params, _method, _headers, _body, _content_type, _security, _cookies, _callback, __where__)", VariableScope.Local)
                      .Return("__req__.send()");
                }).Line();
        }

        private static void EmitCookieApi(GmlWriter w, GmlNaming n)
        {
            w.JsDoc(b => b
                .Param(new ParamDoc("__name__",  "String", null))
                .Param(new ParamDoc("__value2__", "String", null)))
             .Function($"{n.Pub}cookie_set", ["__name__", "__value2__"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("__instance__.cookie_jar", AccessorKind.Struct, "__name__"), "__value2__");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("__name__", "String", null))
                .Returns("String|Undefined"))
             .Function($"{n.Pub}cookie_get", ["__name__"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("__instance__.cookie_jar", AccessorKind.Struct, "__name__"));
             }).Line();

            w.JsDoc(b => b.Param(new ParamDoc("__name__", "String", null)))
             .Function($"{n.Pub}cookie_delete", ["__name__"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Line("struct_remove(__instance__.cookie_jar, __name__);");
             }).Line();

            w.JsDoc(_ => { })
             .Function($"{n.Pub}cookie_clear", [], fn =>
            {
                fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                  .Assign("__instance__.cookie_jar", "{}");
            }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_set_cookie_header", "String", "Raw Set-Cookie header value, may be comma-joined."))
                .Tag("ignore"))
             .Function($"{n.Priv}cookie_capture", ["_set_cookie_header"], fn =>
             {
                 fn.Assign("__instance__", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local);

                 // GameMaker comma-joins repeated Set-Cookie headers into one value.
                 fn.Assign("__parts__", "string_split(_set_cookie_header, \",\")", VariableScope.Local)
                   .Assign("__count__", "array_length(__parts__)", VariableScope.Local);

                 fn.For("var __i__ = 0", "__i__ < __count__", "__i__++", loop =>
                 {
                     // Everything after the first ';' is cookie attributes, not the value.
                     loop.Assign("__pair_parts__", "string_split(__parts__[__i__], \";\")", VariableScope.Local);
                     loop.If("array_length(__pair_parts__) == 0", ifBody => ifBody.Line("continue;"));

                     loop.Assign("__pair__", "string_trim(__pair_parts__[0])", VariableScope.Local)
                         .Assign("__eq__",   "string_pos(\"=\", __pair__)",   VariableScope.Local);
                     loop.If("__eq__ <= 0", ifBody => ifBody.Line("continue;"));

                     loop.Assign("__name__",  "string_trim(string_copy(__pair__, 1, __eq__ - 1))", VariableScope.Local)
                         .Assign("__value__", "string_copy(__pair__, __eq__ + 1, string_length(__pair__) - __eq__)", VariableScope.Local);
                     loop.If("string_length(__name__) > 0", ifBody =>
                         ifBody.Assign(w => w.Access("__instance__.cookie_jar", AccessorKind.Struct, "__name__"), "__value__"));
                 });
             }).Line();
        }
    }
}
