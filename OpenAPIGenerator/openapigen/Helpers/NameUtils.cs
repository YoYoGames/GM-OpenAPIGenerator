using System.Text;
using System.Text.RegularExpressions;

namespace openapigen.Helpers
{
    public static partial class NameUtils
    {
        private static readonly Regex _var = MatchPathParameter();
        private static readonly Regex ident = MatchGmlIdentifier();

        // Case-insensitive exceptions
        public static readonly string[] Exceptions =
        {
            "OAuth2",
            "OAuth",
            "iPhone",
            "iOS",
            "eBay",
            "GitHub"
        };

        private static readonly Regex caseBoundary = MatchCaseBoundary();
        private static readonly Regex acronymBoundary = MatchAcronymBoundary();
        private static readonly Regex clean = Clean();

        public static string ToSnake(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // 1. Protect exceptions
            var protectedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string working = input;

            for (int i = 0; i < Exceptions.Length; i++)
            {
                string token = $"@@{i}@@";
                protectedMap[token] = $"_{Exceptions[i].ToLowerInvariant()}_";

                working = Regex.Replace(
                    working,
                    Regex.Escape(Exceptions[i]),
                    token,
                    RegexOptions.IgnoreCase);
            }

            // 2. Normal snake_case conversion
            working = acronymBoundary.Replace(working, "$1_$2");
            working = caseBoundary.Replace(working, "$1_$2");

            // 3. Restore exceptions
            foreach (var kv in protectedMap)
                working = working.Replace(kv.Key, kv.Value);

            // 4. Clean everything up
            working = clean.Replace(working, "_");

            return working.Trim('_').ToLowerInvariant();
        }



        /// <summary>
        /// True when <paramref name="s"/> can be written as a bare GML identifier. Callers that
        /// emit a name into code must fall back to accessor syntax — <c>self[$ "end"]</c> for a
        /// struct member, a quoted key in a struct literal — whenever this returns false.
        /// </summary>
        public static bool IsValidIdent(string s) => ident.IsMatch(s) && !Reserved.Contains(s);

        /// <summary>
        /// GML keywords and reserved identifiers. Compared case-insensitively: GML keywords are
        /// lowercase, but escaping a differently-cased match is harmless while missing one emits
        /// code that does not compile.
        /// </summary>
        public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            // Control flow
            "if","then","else","do","while","for","switch","case","default","break","continue",
            "return","exit","repeat","until","with",

            // Declarations
            "var","globalvar","static","enum","function","constructor","method","new","delete",
            "begin","end",

            // Exceptions
            "try","catch","finally","throw",

            // Word operators
            "and","or","not","xor","div","mod",

            // Scope / instance keywords
            "self","other","all","noone","global","local",

            // Literals and constants
            "true","false","undefined","infinity","pi","nan",

            // Argument access
            "argument","argument_count","argument0","argument1","argument2","argument3",
            "argument4","argument5","argument6","argument7","argument8","argument9",
            "argument10","argument11","argument12","argument13","argument14","argument15",

            // ---------------------------------------------------------------
            // Global built-in variables.
            //
            // A constructor assigns to `self`, so `field = value` normally creates a struct member.
            // These are the exception: the name resolves to the built-in global instead. Read-only
            // ones ("fps") are a hard compile error in the consumer's project; writable ones
            // ("health", "lives") silently write the global and never create the member, so reading
            // it back throws.
            //
            // INSTANCE built-ins are deliberately NOT here and must not be added. Inside a
            // constructor `self` is the struct, so `id`, `x`, `y`, `depth`, `speed`, `direction`,
            // `sprite_index`, `alarm`, `layer` and friends become ordinary struct members. `id`
            // alone appears in a large fraction of real-world schemas; reserving it would rewrite
            // all of them to `self[$ "id"]` for no benefit.
            //
            // Completeness cannot be proven from here — GameMaker adds built-ins between runtime
            // versions. Over-inclusion only costs readability (the member is emitted through the
            // accessor); under-inclusion emits code that is silently wrong or does not compile. So
            // this list errs toward inclusion, and new names should simply be appended.
            // ---------------------------------------------------------------

            // Game state
            "score","lives","health","debug_mode","error_last","error_occurred","iap_data",
            "gamemaker_pro","gamemaker_registered","secure_mode",

            // Rooms
            "room","room_speed","room_width","room_height","room_persistent","room_first","room_last",

            // Timing
            "fps","fps_real","delta_time","current_time",
            "current_year","current_month","current_day","current_weekday",
            "current_hour","current_minute","current_second",

            // Input
            "keyboard_key","keyboard_lastkey","keyboard_lastchar","keyboard_string",
            "mouse_button","mouse_lastbutton","mouse_x","mouse_y","cursor_sprite",

            // Display / drawing
            "application_surface","view_current","view_enabled","display_aa","webgl_enabled",
            "background_colour","background_color",
            "transition_kind","transition_steps",

            // Game / environment identity
            "game_id","game_display_name","game_project_name","game_save_id",
            "working_directory","program_directory","temp_directory",
            "os_type","os_device","os_version","os_browser","browser_width","browser_height",

            // Events and async
            "async_load","event_data","event_type","event_number","event_object","event_action",

            // Instances
            "instance_count","instance_id",

            // Pointers
            "pointer_null","pointer_invalid",
        };


        public static string ParamName(string raw) => "_" + ToSnake(raw);

        public static string CleanUrlPart(string pathTemplate)
        {
            var noVars = _var.Replace(pathTemplate, string.Empty).Trim('/');
            return ToSnake(noVars);
        }

        public static string EndpointFuncName(string operationId)
        {
            return ToSnake(operationId);
        }

        /// <summary>
        /// The GML function name an <c>operationId</c> is meant to produce, before any collision
        /// resolution. Empty when the operation declares none, or when it snake_cases to nothing.
        /// </summary>
        /// <remarks>
        /// Shared deliberately: the parser assigns names with this, and <c>NoDuplicateEndpointNamesRule</c>
        /// detects a collision by finding an endpoint whose name no longer matches it. Two copies of
        /// this logic would drift, and the rule would silently stop detecting anything.
        /// </remarks>
        public static string IntendedEndpointFuncName(string? operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                return string.Empty;

            var name = EndpointFuncName(operationId);
            if (name.Length == 0)
                return string.Empty;

            return char.IsLetter(name[0]) || name[0] == '_' ? name : "_" + name;
        }

        [GeneratedRegex(@"\{[^}]+\}", RegexOptions.Compiled)]
        private static partial Regex MatchPathParameter();
        [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled)]
        private static partial Regex MatchGmlIdentifier();
        [GeneratedRegex(@"([a-z0-9])([A-Z])", RegexOptions.Compiled)]
        private static partial Regex MatchCaseBoundary();
        [GeneratedRegex(@"([A-Z]{2,})([A-Z][a-z])", RegexOptions.Compiled)]
        private static partial Regex MatchAcronymBoundary();
        [GeneratedRegex(@"[^A-Za-z0-9]+", RegexOptions.Compiled)]
        private static partial Regex Clean();
    }
}
