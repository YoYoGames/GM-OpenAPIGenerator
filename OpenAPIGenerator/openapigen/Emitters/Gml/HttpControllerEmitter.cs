using codegencore.Writers;
using codegencore.Writers.Lang;
using openapigen.Helpers;
using openapigen.Model;

namespace openapigen.Emitters.Gml
{
    internal static class HttpControllerEmitter
    {
        internal static void EmitCleanUpEvent(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines("""
                ds_map_destroy(requests);
                ds_map_destroy(response_hooks);
                """);
        }

        internal static void EmitHttpEvent(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines($$"""
                var __async_id__ = async_load[? "id"];
                var __request__ = requests[? __async_id__];

                if (is_undefined(__request__)) {
                	exit;
                }

                var __status__ = async_load[? "status"];

                // status 1 means "in progress" — wait for the terminal event.
                if (__status__ == 1) exit;

                if ({{n.Priv}}options_is_debug()) {
                	// async_load is a ds_map, which json_stringify cannot serialise.
                	show_debug_message("HTTP: " + json_encode(async_load));
                }

                var __code__ = async_load[? "http_status"];
                var __data__ = async_load[? "result"];

                // response_headers is a ds_map, not a struct.
                var __headers__ = async_load[? "response_headers"];

                if (!is_undefined(__headers__) && ds_exists(__headers__, ds_type_map)) {
                	var __set_cookie__ = string_trim(__headers__[? "Set-Cookie"] ?? "");
                	if (string_length(__set_cookie__) > 0) {
                		{{n.Priv}}cookie_capture(__set_cookie__);
                	}
                }

                try {
                	__data__ = json_parse(__data__);
                }
                catch (__ex__) { /* body is not JSON; hand it back untouched */ };

                var __hook__ = response_hooks[? __code__];
                if (is_callable(__hook__) && __hook__(__code__, __data__, __request__) == true) {
                	ds_map_delete(requests, __async_id__);
                	exit;
                }

                var __callback__ = __request__.get_callback();
                if (is_callable(__callback__)) {
                	__callback__(__code__, __data__, __request__);
                }

                ds_map_delete(requests, __async_id__);
                """);
        }

        internal static void EmitCreateEvent(GmlWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines($$"""
                /// @ignore
                type_converters = {};
                type_converters[$ "*/*"] = function(__body__) { return __body__; };
                type_converters[$ "application/json"] = function(__body__) {
                    // The replacer drops undefined fields so optional properties are omitted
                    // rather than serialised as null.
                    return json_stringify(__body__, false, function(__key__, __value__) {
                	    static __strip__ = function(__k__, __v__) {
                		    if (is_undefined(__v__)) return;
                		    self[$ __k__] = __v__;
                	    }
                	    if (is_struct(__value__)) {
                            with({}) {
                	            struct_foreach(__value__, __strip__);
                	            return self;
                            }
                	    }
                	    return __value__;
                    });
                };
                type_converters[$ "application/x-www-form-urlencoded"] = function(__body__) { return __body__; };
                type_converters[$ "text/plain"] = function(__body__) { return string(__body__); };
                type_converters[$ "multipart/form-data"] = function(__body__, __header__) {
                    var __boundary__ = "----Boundary" + string(current_time) + string(irandom(999999));
                    __header__[? "Content-Type"] = $"multipart/form-data; boundary={__boundary__}";
                    var __parts__ = "";
                    var __keys__ = struct_get_names(__body__);
                    for (var __j__ = 0; __j__ < array_length(__keys__); __j__++) {
                        var __k__ = __keys__[__j__];
                        var __v__ = __body__[$ __k__];
                        if (is_undefined(__v__)) continue;
                        __parts__ += $"--{__boundary__}\r\nContent-Disposition: form-data; name=\"{__k__}\"";
                        // is_handle guards buffer_exists, which throws on a string and reports true
                        // for any real matching a live buffer id — buffer ids start at 0.
                        if (is_handle(__v__) && buffer_exists(__v__)) {
                            // A buffer is binary: interpolating it would write "ref buffer".
                            __parts__ += $"; filename=\"{__k__}\"\r\nContent-Type: application/octet-stream\r\n";
                            __parts__ += "Content-Transfer-Encoding: base64\r\n\r\n";
                            __parts__ += buffer_base64_encode(__v__, 0, buffer_get_size(__v__)) + "\r\n";
                        } else {
                            __parts__ += $"\r\n\r\n{__v__}\r\n";
                        }
                    }
                    return __parts__ + $"--{__boundary__}--\r\n";
                };

                auth_tokens = {};
                cookie_jar = {};

                requests = ds_map_create();
                response_hooks = ds_map_create();
                """);
        }
    }
}
