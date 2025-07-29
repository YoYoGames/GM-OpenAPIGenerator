using CodeGenCore.Helpers;
using CodeGenCore.Writers;
using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.CStyle;
using GMSwaggerCodeGen.Helpers;
using GMSwaggerCodeGen.Ir;

namespace GMSwaggerCodeGen.Emitters.Gml
{
    internal class HttpControllerEmitter
    {
        public static void Emit(IrWebCompilation ir, string dir, GmlNaming n)
        {
            var createEvent = CodeWriter.From(new IndentedStringBuilder());
            createEvent.Section("Create Event (auto-generated, DO NOT EDIT)").Line();
            EmitCreateEvent(createEvent, ir, n);
            createEvent.Line();
            File.WriteAllText(Path.Combine(dir, "controller_create.gml"), createEvent.ToString());

            var httpEvent = CodeWriter.From(new IndentedStringBuilder());
            httpEvent.Section("Http Event (auto-generated, DO NOT EDIT)").Line();
            EmitHttpEvent(httpEvent, ir, n);
            httpEvent.Line();
            File.WriteAllText(Path.Combine(dir, "constroller_http.gml"), httpEvent.ToString());

            var cleanUpEvent = CodeWriter.From(new IndentedStringBuilder());
            cleanUpEvent.Section("Clean Up Event (auto-generated, DO NOT EDIT)").Line();
            EmitCleanUpEvent(cleanUpEvent, ir, n);
            cleanUpEvent.Line();
            File.WriteAllText(Path.Combine(dir, "controller_cleanup.gml"), cleanUpEvent.ToString());
        }

        private static void EmitCleanUpEvent(ICodeWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines("""
                ds_map_destroy(requests);
                ds_map_destroy(response_hooks);
                """);
        }

        private static void EmitHttpEvent(ICodeWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines($$"""
                var _async_id = async_load[? "id"];
                var _request = requests[? _async_id];

                // Early exit if there is no registered request with given id
                if (_request == undefined) {
                	exit;
                }

                var _status = async_load[? "status"];

                // Early exit if the _request has not finished yet
                if (_status == 1) exit;

                if ({{n.Mac}}DEBUG) {
                	var _encoded_async_load = json_encode(async_load);
                	show_debug_message("HTTP: " + _encoded_async_load)
                }

                var _code = async_load[? "http_status"];
                var _data = async_load[? "result"];

                // Make sure we check for respose hooks for the given http code
                var _hook = response_hooks[? _code];
                if (is_callable(_hook) && _hook(_code, _data, _request) == true) {
                	// Hook returned true-ish stop propagation
                    ds_map_delete(requests, _async_id);
                	return; 
                }

                // Get callback for the request
                var _callback = _request.get_callback();
                if (is_callable(_callback)) {
                	try {
                		_data = json_parse(_data);
                	}
                	catch(_ex) { /* ignore it */ };
                	_callback(_code, _data, _request);
                }

                // Request will be handled (remove from map)
                ds_map_delete(requests, _async_id);
                """);
        }

        private static void EmitCreateEvent(ICodeWriter w, IrWebCompilation ir, GmlNaming n)
        {
            w.Lines($$"""
                // These are the builtin converters from some of the content types.
                // More can be added or replaced using the `request_body_set_converter` function.
                /// @ignore
                type_converters = { };
                type_converters[$ "*/*"] = function(_i) { return _i; };
                type_converters[$ "application/json"] = function(_i) { return json_stringify(_i); };
                type_converters[$ "application/x-www-form-urlencoded"] = function(_i) { return _i; };
                type_converters[$ "text/plain"] = function(_i) { return string(_i) };

                // Where all auth-tokens are stored
                auth_tokens = {};

                // Store in-progress requests and also registered response hooks.
                // These serve as lookup tables (ds_map are used due to the nature of the indices)
                requests = ds_map_create();
                response_hooks = ds_map_create();

                /**
                 * @param {Real} _request_id
                 * @param {Struct.{{n.StructPrefix}}HttpRequest} _request_data
                 */
                function register_request(_request_id, _request_data) {
                	requests[? _request_id] = _request_data;
                }
                """);
        }
    }
}
