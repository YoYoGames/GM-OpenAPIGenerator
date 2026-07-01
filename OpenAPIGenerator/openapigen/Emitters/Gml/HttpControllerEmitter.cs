using codegencore.Writers;
using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;

namespace openapigen.Emitters.Gml
{
    internal static class HttpControllerEmitter
    {
        public static void Emit(IrWebCompilation ir, string dir, GmlNaming n)
        {
            var sw = new StringWriter();
            var createEvent = new GmlWriter(CodeWriter.From(sw));
            createEvent.Section("Create Event (auto-generated, DO NOT EDIT)").Line();
            EmitCreateEvent(createEvent, ir, n);
            createEvent.Line();
            File.WriteAllText(Path.Combine(dir, "controller_create.gml"), sw.ToString());

            sw = new StringWriter();
            var httpEvent = new GmlWriter(CodeWriter.From(sw));
            httpEvent.Section("Http Event (auto-generated, DO NOT EDIT)").Line();
            EmitHttpEvent(httpEvent, ir, n);
            httpEvent.Line();
            File.WriteAllText(Path.Combine(dir, "controller_http.gml"), sw.ToString());

            sw = new StringWriter();
            var cleanUpEvent = new GmlWriter(CodeWriter.From(sw));
            cleanUpEvent.Section("Clean Up Event (auto-generated, DO NOT EDIT)").Line();
            EmitCleanUpEvent(cleanUpEvent, ir, n);
            cleanUpEvent.Line();
            File.WriteAllText(Path.Combine(dir, "controller_cleanup.gml"), sw.ToString());
        }

        private static void EmitCleanUpEvent(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines("""
                ds_map_destroy(requests);
                ds_map_destroy(response_hooks);
                """);
        }

        private static void EmitHttpEvent(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines($$"""
                var _async_id = async_load[? "id"];
                var _request = requests[? _async_id];

                if (_request == undefined) {
                	exit;
                }

                var _status = async_load[? "status"];

                if (_status == 1) exit;

                if ({{n.Priv}}options_is_debug()) {
                	var _encoded_async_load = json_encode(async_load);
                	show_debug_message("HTTP: " + _encoded_async_load)
                }

                var _code = async_load[? "http_status"];
                var _data = async_load[? "result"];
                var _headers = async_load[? "responseHeaders"];

                if (!is_undefined(_headers)) {
                	var _set_cookie = string_trim(_headers[$ "Set-Cookie"] ?? "");
                	if (string_length(_set_cookie) > 0) {
                		{{n.Priv}}cookie_capture(_set_cookie);
                	}
                }

                try {
                	_data = json_parse(_data);
                }
                catch(_ex) { /* ignore */ };

                var _hook = response_hooks[? _code];
                if (is_callable(_hook) && _hook(_code, _data, _request) == true) {
                    ds_map_delete(requests, _async_id);
                	return;
                }

                var _callback = _request.get_callback();
                if (is_callable(_callback)) {
                	_callback(_code, _data, _request);
                }

                ds_map_delete(requests, _async_id);
                """);
        }

        private static void EmitCreateEvent(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines($$"""
                /// @ignore
                type_converters = {};
                type_converters[$ "*/*"] = function(_i) { return _i; };
                type_converters[$ "application/json"] = function(_i) {
                    return json_stringify(_i, false, function(_key, _value) {
                	    static __strip = function(_key, _value) {
                		    if (is_undefined(_value)) return;
                		    self[$ _key] = _value;
                	    }
                	    if (is_struct(_value)) {
                            with({}) {
                	            struct_foreach(_value, __strip);
                	            return self;
                            }
                	    }
                	    return _value;
                    });
                };
                type_converters[$ "application/x-www-form-urlencoded"] = function(_i) { return _i; };
                type_converters[$ "text/plain"] = function(_i) { return string(_i); };
                type_converters[$ "multipart/form-data"] = function(_i, _header) {
                    var _boundary = "----Boundary" + string(current_time);
                    _header[? "Content-Type"] = $"multipart/form-data; boundary={_boundary}";
                    var _parts = "";
                    var _keys = struct_get_names(_i);
                    for (var _j = 0; _j < array_length(_keys); _j++) {
                        var _k = _keys[_j];
                        var _v = _i[$ _k];
                        _parts += $"--{_boundary}\r\nContent-Disposition: form-data; name=\"{_k}\"\r\n\r\n{_v}\r\n";
                    }
                    return _parts + $"--{_boundary}--\r\n";
                };

                auth_tokens = {};
                cookie_jar = {};

                requests = ds_map_create();
                response_hooks = ds_map_create();
                """);
        }
    }
}
