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
            EmitUtils(w, ir, n);
            w.Line();
            EmitRequest(w, ir, n);
        }

        private static void EmitOptions(GmlWriter w, GmlNaming n)
        {
            w.JsDoc(b => b.Returns("String"))
             .Function($"{n.Priv}options_get_rest_url", [], fn =>
             {
                 fn.Assign("_url", $"extension_get_option_value(\"{n.StructPrefix}\", \"server_rest_url\")", VariableScope.Static)
                   .Return("_url");
             }).Line();

            w.JsDoc(b => b.Returns("Bool"))
             .Function($"{n.Priv}options_is_debug", [], fn =>
             {
                 fn.Assign("_enabled", $"bool(extension_get_option_value(\"{n.StructPrefix}\", \"debug_logging\"))", VariableScope.Static)
                   .Return("_enabled");
             }).Line();
        }

        private static void EmitUtils(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            var schemeList = string.Join(", ", ir.AuthSchemes.Select(s => $"\"{s.Name}\""));

            w.JsDoc(b => b
                .Param(new ParamDoc("_where", "String", "Caller location for error messages."))
                .Returns("Id.Instance")
                .Tag("ignore"))
             .Function($"{n.Priv}get_singleton", ["_where"], fn =>
             {
                 fn.Assign("instance", $"instance_create_depth(0, 0, 0, obj{n.Priv}core)", VariableScope.Static);
                 fn.With("instance", inner => inner.Return("self"));
                 fn.Line($"show_error($\"{{_where}} :: Failed to get the obj{n.Priv}core singleton.\", true);");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_token_id", "String", $"One of: {schemeList}"))
                .Param(new ParamDoc("_token", "String", null)))
             .Function($"{n.Priv}request_auth_set_token", ["_token_id", "_token"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("_instance.auth_tokens", AccessorKind.Struct, "_token_id"), "_token");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_token_id", "String", $"One of: {schemeList}"))
                .Returns("String"))
             .Function($"{n.Priv}request_auth_get_token", ["_token_id"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("_instance.auth_tokens", AccessorKind.Struct, "_token_id"));
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_content_type", "String", null))
                .Param(new ParamDoc("_function", "Function", "function(_body, _header_ds_map) → String|Id.Buffer")))
             .Function($"{n.Priv}request_body_set_converter", ["_content_type", "_function"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("_instance.type_converters", AccessorKind.Struct, "_content_type"), "_function");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_content_type", "String", null))
                .Returns("Function"))
             .Function($"{n.Priv}request_body_get_converter", ["_content_type"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("_instance.type_converters", AccessorKind.Struct, "_content_type"));
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_code", "Real", null))
                .Param(new ParamDoc("_hook", "Function", null)))
             .Function($"{n.Priv}request_response_set_hook", ["_code", "_hook"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("_instance.response_hooks", AccessorKind.Map, "_code"), "_hook");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_code", "Real", null))
                .Returns("Function"))
             .Function($"{n.Priv}request_response_get_hook", ["_code"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("_instance.response_hooks", AccessorKind.Map, "_code"));
             }).Line();
        }

        private static void EmitRequest(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.JsDoc(b => b
                .Param(new ParamDoc("_url",          "String",           null))
                .Param(new ParamDoc("_params",        "Struct|Undefined", null))
                .Param(new ParamDoc("_method",        "String",           null))
                .Param(new ParamDoc("_body",          "Any",              null))
                .Param(new ParamDoc("_content_type",  "String|Undefined", null))
                .Param(new ParamDoc("_security",      "Array|Undefined",  null))
                .Param(new ParamDoc("_cookies",       "Struct|Undefined", "Per-request cookies merged with the cookie jar on send."))
                .Param(new ParamDoc("_callback",      "Function",         null))
                .Param(new ParamDoc("_where",         "String",           null)))
             .Struct($"{n.StructPrefix}Request",
                ["_url", "_params", "_method", "_body", "_content_type", "_security", "_cookies", "_callback", "_where"],
                body =>
                {
                    body.Assign("__", w => w.StructLiteral([
                        new("url",          "_url"),
                        new("params",       "_params"),
                        new("http_method",  "_method"),
                        new("content_type", "_content_type"),
                        new("raw_body",     "undefined"),
                        new("callback",     "_callback"),
                        new("security",     "_security"),
                        new("cookies",      "_cookies"),
                        new("where",        "_where"),
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
                            fn.Assign("_id",   "-1",   VariableScope.Local)
                              .Assign("_self", "self", VariableScope.Local);

                            fn.With("__", inner =>
                            {
                                inner.Assign("_params", "params ?? {}", VariableScope.Local)
                                     .Assign("_header", "ds_map_create()", VariableScope.Local).Line();

                                inner.Comment("inject security");
                                inner.Assign("_sec_count", "array_length(security)", VariableScope.Local);
                                inner.For("var _i = 0", "_i < _sec_count", "_i++", loop =>
                                    loop.Line($"_self._apply_auth(_header, _params, security[_i], where);")).Line();

                                inner.Assign("_instance", $"{n.Priv}get_singleton(where)", VariableScope.Local).Line();

                                inner.Comment("cookies: jar entries first, then per-request overrides");
                                inner.Assign("_cookie_parts", "[]",              VariableScope.Local)
                                     .Assign("_j",            "0",              VariableScope.Local)
                                     .Assign("_jar_keys",   "struct_get_names(_instance.cookie_jar)", VariableScope.Local)
                                     .Assign("_jar_count",  "array_length(_jar_keys)", VariableScope.Local);
                                inner.For("var _i = 0", "_i < _jar_count", "_i++", loop =>
                                {
                                    loop.Assign("_k", "_jar_keys[_i]", VariableScope.Local)
                                        .Line("_cookie_parts[_j++] = $\"{_k}={_instance.cookie_jar[$ _k]}\";");
                                });
                                inner.If("!is_undefined(cookies)", ifBody =>
                                {
                                    ifBody.Assign("_exp_keys",  "struct_get_names(cookies)",    VariableScope.Local)
                                          .Assign("_exp_count", "array_length(_exp_keys)",      VariableScope.Local);
                                    ifBody.For("var _i = 0", "_i < _exp_count", "_i++", loop =>
                                    {
                                        loop.Assign("_k", "_exp_keys[_i]", VariableScope.Local)
                                            .Line("_cookie_parts[_j++] = $\"{_k}={cookies[$ _k]}\";");
                                    });
                                });
                                inner.If("_j > 0", ifBody =>
                                    ifBody.Line("_header[? \"Cookie\"] = string_join_ext(\"; \", _cookie_parts, 0, _j);")).Line();

                                inner.Assign("_url", "_self._build_url(url, _params)", VariableScope.Local).Line();

                                inner.If("!is_undefined(raw_body)", ifBody =>
                                {
                                    ifBody.Comment("set Content-Type before converter so it can override (e.g. multipart boundary)");
                                    ifBody.Line("_header[? \"Content-Type\"] = content_type;");
                                    ifBody.Assign("_processed", "_self._process_body(raw_body, content_type, _header, where)", VariableScope.Local);
                                    ifBody.If("is_string(_processed)",
                                        thenBody => thenBody.Line("_id = http_request(_url, http_method, _header, _processed);"),
                                        elseBody =>
                                        {
                                            elseBody.Line("_id = http_request(_url, http_method, _header, _processed);");
                                            elseBody.Line("buffer_delete(_processed);");
                                        });
                                }, elseBody =>
                                {
                                    elseBody.Line("_id = http_request(_url, http_method, _header, \"\");");
                                });
                                inner.Line();

                                inner.Line("_instance.requests[? _id] = _self;");
                                inner.Line("ds_map_destroy(_header);");
                            });

                            fn.Line("attempts++;");
                            fn.Return("_id");
                        }), VariableScope.Static).Line();

                    body.JsDoc(b => b.Returns("Real"))
                        .Assign("retry", w => w.Method([], fn => fn.Return("send()")), VariableScope.Static).Line();

                    body.JsDoc(b => b
                            .Param(new ParamDoc("_body",         "Any",       null))
                            .Param(new ParamDoc("_content_type", "String",    null))
                            .Param(new ParamDoc("_header",       "Id.DsMap",  "Converter may mutate this (e.g. multipart sets boundary)."))
                            .Param(new ParamDoc("_where",        "String",    null))
                            .Returns("String|Id.Buffer")
                            .Tag("ignore"))
                        .Assign("_process_body", w => w.Method(["_body", "_content_type", "_header", "_where"], fn =>
                        {
                            fn.Assign("_conv", $"{n.Priv}request_body_get_converter(_content_type)", VariableScope.Local);
                            fn.If("!is_callable(_conv)", ifBody =>
                                ifBody.Line($"show_error($\"{{_where}} :: No converter for '{{_content_type}}'.\", true);"));
                            fn.Line("_body = _conv(_body, _header);");
                            fn.If("!is_string(_body) && (!is_handle(_body) || !string_starts_with(string(_body), \"ref buffer\"))", ifBody =>
                                ifBody.Line($"show_error($\"{{_where}} :: Body converter must return a string or buffer.\", true);"));
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
                            fn.Assign("_result", "[]", VariableScope.Static);
                            fn.Assign("_params_build_array", w => w.Method(["_key", "_array"], inner =>
                            {
                                inner.Assign("_len", "array_length(_array)", VariableScope.Local)
                                     .Assign("_r",   "array_create(_len)",  VariableScope.Local);
                                inner.For("var _i = 0", "_i < _len", "++_i",
                                    loop => loop.Line("_r[_i] = $\"{_key}={_array[_i]}\";"));
                                inner.Return("string_join_ext(\"&\", _r)");
                            }), VariableScope.Static).Line();

                            fn.If("is_undefined(_params)", ifBody => ifBody.Return("_url_base")).Line();

                            fn.Assign("_keys",   "struct_get_names(_params)", VariableScope.Local)
                              .Assign("_length", "array_length(_keys)",        VariableScope.Local)
                              .Assign("_j",      "0",                          VariableScope.Local);
                            fn.For("var _i = 0", "_i < _length", "_i++", loop =>
                            {
                                loop.Assign("_key",   "_keys[_i]",             VariableScope.Local)
                                    .Assign("_value", "struct_get(_params, _key)", VariableScope.Local);
                                loop.If("is_undefined(_value)", ifBody => ifBody.Line("continue;"));
                                loop.Line("_result[_j++] = is_array(_value) ? _params_build_array(_key, _value) : $\"{_key}={_value}\";");
                            }).Line();

                            fn.If("_j == 0", ifBody => ifBody.Return("_url_base"));
                            fn.Assign("_sep", "string_pos(\"?\", _url_base) == 0 ? \"?\" : \"&\"", VariableScope.Local);
                            fn.Return("_url_base + _sep + string_join_ext(\"&\", _result, 0, _j)");
                        }), VariableScope.Static).Line();

                    body.JsDoc(b => b
                            .Param(new ParamDoc("_header", "Id.DsMap", null))
                            .Param(new ParamDoc("_params", "Struct",   null))
                            .Param(new ParamDoc("_scheme", "String",   null))
                            .Param(new ParamDoc("_where",  "String",   null))
                            .Tag("ignore"))
                        .Assign("_apply_auth", w => w.Method(["_header", "_params", "_scheme", "_where"], fn =>
                        {
                            fn.Assign("missing", w => w.Method(["_where", "_token"], inner =>
                                inner.Line("show_debug_message($\"{_where} :: missing credential for '{_token}', skipping auth.\");")),
                                VariableScope.Static).Line();

                            fn.Switch("_scheme", sw =>
                            {
                                foreach (var s in ir.AuthSchemes)
                                {
                                    var sn = s.Name;
                                    sw.Case($"\"{sn}\"", caseBody =>
                                    {
                                        caseBody.Assign($"_{sn}_token", $"{n.Priv}request_auth_get_token(\"{sn}\")", VariableScope.Local);
                                        caseBody.If($"is_undefined(_{sn}_token)", ifBody =>
                                        {
                                            ifBody.Line($"missing(_where, \"{sn}\");");
                                            ifBody.Line("break;");
                                        });

                                        switch (s)
                                        {
                                            case IrAuthScheme.Basic:
                                                caseBody.Line($"_header[? \"Authorization\"] = $\"Simple {{base64_encode(_{sn}_token)}}\";");
                                                break;
                                            case IrAuthScheme.Bearer:
                                                caseBody.Line($"_header[? \"Authorization\"] = $\"Bearer {{_{sn}_token}}\";");
                                                break;
                                            case IrAuthScheme.ApiKey apiKey when apiKey.In == IrLocation.Header:
                                                caseBody.Line($"_header[? \"{apiKey.ParamName}\"] = _{sn}_token;");
                                                break;
                                            case IrAuthScheme.ApiKey apiKey when apiKey.In == IrLocation.Query:
                                                caseBody.Line($"_params[$ \"{apiKey.ParamName}\"] = _{sn}_token;");
                                                break;
                                        }
                                    });
                                }

                                sw.Case("undefined", caseBody => { });
                                sw.Default(d =>
                                    d.Line("show_debug_message($\"{_where} :: No auth rule for '{_scheme}'.\");"));
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
                .Param(new ParamDoc("_body",          "Any",              null))
                .Param(new ParamDoc("_content_type",  "String|Undefined", null))
                .Param(new ParamDoc("_security",      "Array|Undefined",  null))
                .Param(new ParamDoc("_cookies",       "Struct|Undefined", null))
                .Param(new ParamDoc("_callback",      "Function",         null))
                .Param(new ParamDoc("_where",         "String",           null))
                .Returns("Real")
                .Tag("ignore"))
             .Function($"{n.Priv}create_request",
                ["_url", "_params", "_method", "_body", "_content_type", "_security", "_cookies", "_callback", "_where"],
                fn =>
                {
                    fn.Assign("_req", $"new {n.StructPrefix}Request(_url, _params, _method, _body, _content_type, _security, _cookies, _callback, _where)", VariableScope.Local)
                      .Return("_req.send()");
                }).Line();
        }

        private static void EmitCookieApi(GmlWriter w, GmlNaming n)
        {
            w.JsDoc(b => b
                .Param(new ParamDoc("_name",  "String", null))
                .Param(new ParamDoc("_value", "String", null)))
             .Function($"{n.Pub}cookie_set", ["_name", "_value"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Assign(w => w.Access("_instance.cookie_jar", AccessorKind.Struct, "_name"), "_value");
             }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_name", "String", null))
                .Returns("String|Undefined"))
             .Function($"{n.Pub}cookie_get", ["_name"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Return(r => r.Access("_instance.cookie_jar", AccessorKind.Struct, "_name"));
             }).Line();

            w.JsDoc(b => b.Param(new ParamDoc("_name", "String", null)))
             .Function($"{n.Pub}cookie_delete", ["_name"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                   .Line("struct_remove(_instance.cookie_jar, _name);");
             }).Line();

            w.Function($"{n.Pub}cookie_clear", [], fn =>
            {
                fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local)
                  .Assign("_instance.cookie_jar", "{}");
            }).Line();

            w.JsDoc(b => b
                .Param(new ParamDoc("_set_cookie_header", "String", "Raw Set-Cookie header value, may be comma-joined."))
                .Tag("ignore"))
             .Function($"{n.Priv}cookie_capture", ["_set_cookie_header"], fn =>
             {
                 fn.Assign("_instance", $"{n.Priv}get_singleton(_GMFUNCTION_)", VariableScope.Local);
                 fn.Assign("_parts", "string_split(_set_cookie_header, \",\")", VariableScope.Local)
                   .Assign("_count", "array_length(_parts)", VariableScope.Local);

                 fn.For("var _i = 0", "_i < _count", "_i++", loop =>
                 {
                     loop.Assign("_pair_parts", "string_split(_parts[_i], \";\")", VariableScope.Local);
                     loop.If("array_length(_pair_parts) == 0", ifBody => ifBody.Line("continue;"));
                     loop.Assign("_pair", "string_trim(_pair_parts[0])", VariableScope.Local)
                         .Assign("_eq",   "string_pos(\"=\", _pair)",   VariableScope.Local);
                     loop.If("_eq <= 0", ifBody => ifBody.Line("continue;"));
                     loop.Assign("_name",  "string_trim(string_copy(_pair, 1, _eq - 1))", VariableScope.Local)
                         .Assign("_value", "string_copy(_pair, _eq + 1, string_length(_pair) - _eq)", VariableScope.Local);
                     loop.If("string_length(_name) > 0", ifBody =>
                         ifBody.Assign(w => w.Access("_instance.cookie_jar", AccessorKind.Struct, "_name"), "_value"));
                 });
             }).Line();
        }
    }
}
