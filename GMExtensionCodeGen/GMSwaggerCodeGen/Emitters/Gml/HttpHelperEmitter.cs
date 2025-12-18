using CodeGenCore.Writers;
using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.CStyle;
using CodeGenCore.Writers.Lang.Gml;
using GMSwaggerCodeGen.Helpers;
using GMSwaggerCodeGen.Ir;

namespace GMSwaggerCodeGen.Emitters.Gml
{
    internal class HttpHelperEmitter
    {
        public static void Emit(IrWebCompilation ir, ICodeWriter w, GmlNaming n) 
        {
            EmitOptionsHelper(w, ir, n);
            w.Line();
            EmitUtilsHelper(w, ir, n);
            w.Line();
            EmitRequestHelper(w, ir, n);
            w.Line();
        }

        private static void EmitOptionsHelper(ICodeWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines($$"""           
                /// @returns {String}
                function {{n.Priv}}options_get_rest_url() {
                	static _url = extension_get_option_value("{{n.StructPrefix}}", "server_rest_url");
                	return _url;
                }
                
                /// @returns {Bool}
                function {{n.Priv}}options_is_debug() {
                	static _enabled = bool(extension_get_option_value("{{n.StructPrefix}}", "debug_logging"));
                	return _enabled;
                }
                """);
            w.Line();
        }

        private static void EmitUtilsHelper(ICodeWriter w, IrWebCompilation ir, GmlNaming n)
        {
            var authSchemeLiterals = string.Join(", ", ir.AuthSchemes.Select(s => $"\"{s.Name}\""));

            w.JsDoc(b =>
            {
                b.Tag("func", $"{n.Priv}get_singleton")
                .Summary($"Returns the {n.StructPrefix} core singleton (the single instance of 'obj{n.Priv}core').")
                .Param(new ParamDoc("_where", "String", "Usually the '_GMFUNCTION_' macro – used only for clearer error messages."))
                .Tag("ignore");
            })
            .Function($"{n.Priv}get_singleton", ["_where"], body =>
            {
                body.Comment("Generator function")
                .Assign("instance", expr => expr.Call("instance_create_depth", ["0", "0", "0", $"obj{n.Priv}core"]), VariableScope.Static)
                .Keyword("with", "instance", withBody =>
                {
                    withBody.Return("self");
                })
                .Call("show_error", [$$"""$"{_where} :: Failed to get the {obj{{n.Priv}}core} singleton instance." """, "true"]).Line(";");
            })
            .Line();

            // Auth Tokens
            w.JsDoc(b =>
            {
                b.Tag("func", $"{n.Priv}request_auth_set_token")
                .Summary($$"""
                    Stores or updates an authentication token under the given ID.
                    This is typically used when acquiring tokens from login endpoints,
                    OAuth flows, or any credential-granting operation.
                    The token is stored inside the 'auth_token' map of
                    'obj{{n.Priv}}core', which is accessed via the singleton.
                    """)
                .Param(new ParamDoc("_token_id", "String", $"One of the following ids: {authSchemeLiterals}."))
                .Param(new ParamDoc("_token", "String", "The actual token string (e.g. JWT, API key, etc.)"))
                .Returns("Undefined");
            })
            .Function($"{n.Priv}request_auth_set_token", ["_token_id", "_token"], body =>
            {
                body.Assign("_instance", expr => expr.Call($"{n.Priv}get_singleton", "_GMFUNCTION_"), VariableScope.Local)
                .Assign(lhs: lhs => lhs.Access("_instance.auth_tokens", AccessorKind.Struct, "_token_id"), "_token", VariableScope.None);
            })
            .Line();

            w.JsDoc(b =>
            {
                b.Tag("func", $"{n.Priv}request_auth_get_token")
                .Summary($$"""
                    Retrieves a previously stored token by ID.
                    If the ID does not exist in the token map, 'undefined' is returned.
                    This is typically used by the internal request system when applying
                    security credentials to headers, query parameters, or request bodies.
                    """)
                .Param(new ParamDoc("_token_id", "String", $"One of the following ids: {authSchemeLiterals}."))
                .Returns("String", "The token string, or undefined if not found.");
            })
            .Function($"{n.Priv}request_auth_get_token", ["_token_id"], body =>
            {
                body.Assign("_instance", expr => expr.Call($"{n.Priv}get_singleton", "_GMFUNCTION_"), VariableScope.Local)
                .Return(expr => expr.Access("_instance.auth_tokens", AccessorKind.Struct, "_token_id"));
            })
            .Line();

            // Body Converters
            w.JsDoc(b =>
            {
                b.Tag("func", $"{n.Priv}request_body_set_converter")
                .Summary($$"""
                    Convert a body struct/value to the wire format that matches the content-type header. Falls back to passthrough.
                    Registers (or replaces) a converter function for the given 'Content-Type'.
                    """)
                .Param(new ParamDoc("_content_type", "String", "Content type value to match (case-sensitive)"))
                .Param(new ParamDoc("_function", "Function", "A function that takes '(value, where)' and returns a string|buffer|any."));
            })
            .Function($"{n.Priv}request_body_set_converter", ["_content_type", "_function"], body =>
            {
                body.Assign("_instance", expr => expr.Call($"{n.Priv}get_singleton", "_GMFUNCTION_"), VariableScope.Local)
                .Assign(lhs: lhs => lhs.Access("_instance.type_converters", AccessorKind.Struct, "_content_type"), "_function");
            })
            .Line();

            w.JsDoc(b =>
            {
                b.Tag("func", $"{n.Priv}request_body_get_converter")
                .Summary($$"""
                    Retrieves the converter assigned to the specified 'Content-Type'.
                    Useful for testing or chaining to the existing implementation.
                    """)
                .Param(new ParamDoc("_content_type", "String", "Content type value to match (case-sensitive)"))
                .Returns("Function", "The converter function previously assigned (or default one).");
            })
            .Function($"{n.Priv}request_body_get_converter", ["_content_type"], body =>
            {
                body.Assign("_instance", expr => expr.Call($"{n.Priv}get_singleton", "_GMFUNCTION_"), VariableScope.Local)
                .Return(expr => expr.Access("_instance.type_converters", AccessorKind.Struct, "_content_type"));
            })
            .Line();

            // Response Hooks
            w.JsDoc(b =>
            {
                b.Tag("func", $"{n.Priv}request_response_set_hook")
                .Summary("Sets a default request reponse hook for a given http status.")
                .Param(new ParamDoc("_code", "Real", "The http response status to attach the hook too."))
                .Param(new ParamDoc("_hook", "Function", "The interseption function that will be executed."));
            })
            .Function($"{n.Priv}request_response_set_hook", ["_code", "_hook"], body =>
            {
                body.Assign("_instance", expr => expr.Call($"{n.Priv}get_singleton", "_GMFUNCTION_"), VariableScope.Local)
                .Assign(lhs: lhs => lhs.Access("_instance.response_hooks", AccessorKind.Map, "_code"), "_hook");
            })
            .Line();

            w.JsDoc(b =>
            {
                b.Tag("func", $"{n.Priv}request_body_get_hook")
                .Summary("Gets the default request reponse hook for a given http status.")
                .Param(new ParamDoc("_code", "Real", "The http response status to retreive the hook function from."))
                .Returns("Function", "The hook function previously assigned (or default one).");
            })
            .Function($"{n.Priv}request_body_get_hook", ["_code"], body =>
            {
                body.Assign("_instance", expr => expr.Call($"{n.Priv}get_singleton", "_GMFUNCTION_"), VariableScope.Local)
                .Return(expr => expr.Access("_instance.response_hooks", AccessorKind.Map, "_code"));
            })
            .Line();
        }

        private static void EmitRequestHelper(ICodeWriter w, IrWebCompilation ir, GmlNaming n)
        {
            // Constructor
            w.JsDoc(b =>
            {
                b.Summary("A wrapper around an http request index that allows retries.")
                .Param(new ParamDoc("_url", "String", "Url used in the request (without the parameters)"))
                .Param(new ParamDoc("_params", "Struct", "Url params that will be sent with the request."))
                .Param(new ParamDoc("_method", "String", "Method that will be used by the request."))
                .Param(new ParamDoc("_body", "Any", "The body of the request can be of any type."))
                .Param(new ParamDoc("_content_type", "String", "The content type used for formatting the body."))
                .Param(new ParamDoc("_security", "Array", "An array with all the security schemes that should be injected into the request."))
                .Param(new ParamDoc("_callback", "Function", "The callback function to be executed once the request finishes."))
                .Param(new ParamDoc("_where", "String", "The callee of this function (used for debugging purposes)."));
            })
            .Struct($"{n.StructPrefix}Request", ["_url", "_params", "_method", "_body", "_content_type", "_security", "_callback", "_where"], body =>
            {
                // Private variables
                body.Lines("""
                    /**
                     * create private scope
                     * @ignore
                     */
                    __ = {
                    	url: _url,
                    	params: _params,
                    	http_method: _method,
                        content_type: _content_type,
                    	body_type: 0, // string
                    	body: undefined,
                    	callback: _callback,
                    	security: _security,
                    	where: _where,
                    }
                    """)
                .Line();

                // Public variables
                body.Lines("""
                    attempts = 0;
                    """)
                .Line();

                // Public functions
                body.Lines($$"""
                    /**
                     * @func get_callback
                     * Returns the callback attatched to this request.
                     * @returns {Function}
                     */
                    static get_callback = function()
                    {
                    	return __.callback;
                    }

                    /**
                     * @func send
                     * Triggers the actual HTTP request.
                     * @returns {Real}
                     */
                    static send = function()
                    {
                    	var _id = -1;
                        var _self = self;
                    	with (__)
                        {
                            // make sure we have a struct cus we might need it for auth
                            var _params = params ?? {};
                    
                            // reconstruct header we might need it for auth	
                    		var _header = ds_map_create();

                            // inject security tokens lazily
                            var _count = array_length(security);
                            for (var _i = 0; _i < _count; _i++) {
                                _self._apply_auth(_header, _params, security[_i], where);
                    		}

                            // build final url from cached params
                            var _url = _self._build_url(url, _params);

                            if (!is_undefined(body))
                            {
                    	        _header[? "Content-Type"] = content_type;
                    	        switch (body_type) 
                    	        {
                    		        case 0: // string
                    			        var _string_body = body;
                    			        _id = http_request(_url, http_method, _header, _string_body);
                    			        break;
                    		        case 1: // buffer
                    			        var _buffer_body = buffer_base64_decode(body);
                    			        _id = http_request(_url, http_method, _header, _buffer_body);
                    			        buffer_delete(_buffer_body);
                    			        break;
                    	        }
                            }
                            else 
                            {
                    	        // Bodyless route (just use empty string)
                    	        _id = http_request(_url, http_method, _header, "");
                            }

                            var _instance = {{n.Priv}}get_singleton(where);
                    		_instance.requests[? _id] = _self;
                    
                            // free header
                            ds_map_destroy(_header);
                    	}
                    	attempts++;
                    	return _id;
                    }
                    
                    /**
                     * @func retry
                     * Alias to 'send' which will trigger the actual HTTP request.
                     * @returns {Real}
                     */
                    static retry = function() { return send(); }
                    """)
                .Line();

                // Pivate functions
                body.Lines($$"""

                    /**
                     * @func _process_body
                     * Processes the body of the http request.
                     * @param {Any} _body The body of the request can be of any type.
                     * @param {String} _content_type The content type used for formatting the body.
                     * @param {String} _where The callee of this function (used for debugging purposes).
                     * @returns {String|Id.Buffer}
                     * @ignore
                     */
                    static _process_body = function(_body, _content_type, _where)
                    {
                        var _body_converter = {{n.Priv}}request_body_get_converter(_content_type);
                        if (!is_callable(_body_converter)) 
                        {
                            show_error($"{_where} :: No converter for '{_content_type}'.", true);
                        }

                        _body = _body_converter(_body); // The result here needs to be either a string or a buffer
                        if (!is_string(_body) && (!is_handle(_body) || !string_starts_with(string(_body), "ref buffer")))
                        {
                            show_error($"{_where} :: The body process function failed to output either a string or a valid buffer.", true);
                        }

                        // feather ignore once GM1045
                        return _body;
                    }


                    /**
                     * @func _build_url
                     * Builds the full url from the base url and the cached params.
                     * @param {String} _url_base
                     * @param {Struct} _params
                     * @returns {String}
                     * @ignore
                     */
                    static _build_url = function(_url_base, _params = undefined)
                    {
                        // cache an internal array for storing params (this should reduce memory allocations)
                        static _result = [];
                    
                        // build params entry for array value
                        static params_build_array = function(_key, _array)
                        {
                    	    var _length = array_length(_array);
                    	    var _result = array_create(_length);
                    	    for (var _i = 0; _i < _length; ++_i) {
                    		    _result[_i]=$"{_key}={_array[_i]}"; 
                    	    }
                    	    return string_join_ext("&", _result);
                        } 
                        
                    	var _keys = struct_get_names(_params);
                    	var _length = array_length(_keys);
                    
                        // build full url from params (remove undefined entries)
                        var _j = 0;
                    	for (var _i = 0; _i < _length; _i++)
                    	{
                    		var _key = _keys[_i];
                    		var _value = struct_get(_params, _keys[_i]);
                    
                            if (is_undefined(_value)) continue;
                    
                    		_result[_j++] = is_array(_value) ? params_build_array(_key, _value) : $"{_key}={_value}";
                    	}
                    
                    	var _str = string_pos("?", _url_base) == 0 ? "?" : "";
                    	_str += string_join_ext("&", _result, 0, _j);
                    
                    	return _url_base + _str;
                    }
                    """)
                .Line();

                body.JsDoc(builder =>
                {
                    builder
                    .Line("@func _apply_auth")
                    .Summary("Injects credentials for the named security scheme.")
                    .Param(new ParamDoc("_header", "Id.DsMap", "Map that will contain the header metadata."))
                    .Param(new ParamDoc("_params", "Struct", "Url params that will be sent with the request."))
                    .Param(new ParamDoc("_scheme", "String", "The name of the secutiry scheme being processed."))
                    .Param(new ParamDoc("_where", "String", "The callee of this function (used for debugging purposes)."))
                    .Tag("ignore");
                })
                .Assign("_apply_auth", expr => expr.Method(["_header", "_params", "_scheme", "_where"], fnBody =>
                {
                    // When a token is missing print a message to the user
                    fnBody.Assign("missing", expr => expr.Method(["_where", "_token"], methodBody =>
                    {
                        methodBody.Call("show_debug_message", "$\"{_where} :: missing credential for {_token}, skipping this auth method.\"").Line(";");
                    }), VariableScope.Static);
                    fnBody.Line();

                    // Switch on the available schemes from the spec
                    fnBody.Switch("_scheme", sw =>
                    {
                        foreach (var s in ir.AuthSchemes)
                        {
                            var tokenLit = $"\"{s.Name}\"";
                            sw.Case(tokenLit, caseBody =>
                            {

                                caseBody.Assign($"_{s.Name}_token", expr => expr.Call($"{n.Priv}request_auth_get_token", tokenLit), VariableScope.Local);
                                caseBody.If($"is_undefined(_{s.Name}_token)", ifBody =>
                                {
                                    ifBody.Call("missing", tokenLit, "_where").Line(";");
                                    ifBody.Line("break;");
                                });

                                switch (s)
                                {
                                    case IrBasicScheme auth:
                                        caseBody.AppendLine($"_header[? \"Authorization\"] = $\"Simple {{base64_encode(_{s.Name}_token)}}\";");
                                        break;
                                    case IrBearerScheme bearerAuth:
                                        caseBody.AppendLine($"_header[? \"Authorization\"] = $\"Bearer {{_{s.Name}_token}}\";");
                                        break;
                                    case IrApiKeyScheme apiKeyScheme:
                                        switch (apiKeyScheme.In)
                                        {
                                            case IrLocation.Header:
                                                caseBody.AppendLine($"_header[? \"{apiKeyScheme.KeyName}\"] = _{s.Name}_token;");
                                                break;
                                            case IrLocation.Query:
                                                caseBody.AppendLine($"_params[$ \"{apiKeyScheme.KeyName}\"] = _{s.Name}_token;");
                                                break;
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            });
                        }
                        sw.Case("undefined", body => { }, true);
                        sw.Default(d => d.Line("show_debug_message($\"{_where} :: No auth rule for '{_scheme}'.\");"));
                    });
                }), VariableScope.Static)
                .Line();

                body.Lines($$"""
                    // Handle request body
                    if (!is_undefined(_body)) {
                        _body = _process_body(_body, _content_type, _where);
                        if (!is_string(_body)) 
                        {
                        	__.body_type = 1; // buffer
                        	__.body = buffer_base64_encode(_body, 0, -1); // we base64 encode the buffer so we can re-use it.
                        }
                    	else
                    	{
                    		__.body = _body;
                    	}
                    }
                    """);
            })
            .Line();

            // Creates a new request internal
            w.Lines($$"""
                /**
                 * A wrapper function that will create a new {{n.StructPrefix}}Request and immediately trigger it.
                 * @param {String} _url Url used in the request (without the parameters)
                 * @param {Struct|Undefined} _params Url params that will be sent with the request.
                 * @param {String} _method Method that will be used by the request.
                 * @param {Any} _body The body of the request can be either a string or a buffer.
                 * @param {String|Undefined} _content_type The content type used for formatting the body.
                 * @param {Array} _security An array with all the security schemes that should be injected into the request.
                 * @param {Function} _callback The callback function to be executed once the request finishes.
                 * @param {String} _where The callee of this function (used for debugging purposes).
                 * @returns {Real}
                 * @ignore
                 */
                function {{n.Priv}}create_request(_url, _params, _method, _body, _content_type, _security, _callback, _where)
                {
                	var _request = new {{n.StructPrefix}}Request(_url, _params, _method, _body, _content_type, _security, _callback, _where);
                	return _request.send();
                }
                """);
        }
    }
}
