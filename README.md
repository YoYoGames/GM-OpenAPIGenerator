# GMSwaggerCodeGen — OpenAPI → GML code generator

**GMSwaggerCodeGen** reads an OpenAPI 3.x specification and generates
GameMaker Language (GML) code:

- ✅ Strongly-typed **schema structs** with field validation
- ✅ **Endpoint wrapper** functions that build URLs, serialize bodies,
  attach headers/query params, and trigger HTTP requests
- ✅ An **auth helper** to inject credentials based on security schemes
- ✅ Helpful **JSDoc** for functions and fields (including enum values)

This lets you call web APIs directly from GameMaker with one consistent,
type-checked GML layer.

> ⚠️ GML has no namespaces. The generator uses a **configurable prefix** for
> public functions, private helpers, macros, and struct names to avoid collisions.

---

## Table of contents

- [Features](#features)
- [What gets generated](#what-gets-generated)
- [Requirements](#requirements)
- [Command-line usage](#command-line-usage)
- [Configuration](#configuration)
- [Examples](#examples)
- [Supported OpenAPI features](#supported-openapi-features)
- [Limitations](#limitations)
- [Configuration & naming](#configuration--naming)
- [FAQ](#faq)
- [Contributing](#contributing)

---

## Features

- **OpenAPI 3.x** JSON or YAML input (format detected from the file extension)
- Generates up to eight files, each independently placed via `config.json`:
  - **Schemas**: GML constructors (structs) + a `_validate` function per schema
  - **Endpoints**: one wrapper per operation, named from `operationId`
  - **Helpers**: request struct, auth injection, cookie jar, URL encoding
  - **Controller events** and **Feather doc partials**
- **Parameter handling**
  - Required params first, optional after (spec `default` values are emitted)
  - Path params are inserted into the URL, percent-encoded
  - Query params collected into a struct; undefined entries are omitted
  - Header params collected into a struct and merged into the request headers
  - Cookie params are handled by the cookie jar, not exposed as arguments
- **Request bodies**
  - Supports `application/json`, `application/*+json`,
    `application/x-www-form-urlencoded`, `multipart/form-data`, `text/plain`, `*/*`
  - If multiple media types are supported, a `_content_type` argument is generated
- **Auth**
  - Reads `components.securitySchemes` and per-operation requirements
  - Endpoints include a `_security` array with the scheme **names** from your spec
  - Helper applies tokens from your game’s credential store
- **Validation**
  - Generated code checks types at runtime
  - Structs include a static `validate(_where)` that validates all fields (optional
    fields validated only when defined)
- **Docs**
  - JSDoc for functions/params and schema fields
  - Enum values included in parameter/field docs when present in the spec

---

## What gets generated

Eight files, each independently placeable and switchable via `config.json`:

| Output | Default file | Contents |
|---|---|---|
| `code.schemas` | `generated_schemas.gml` | Constructors + `_validate` per schema |
| `code.endPoints` | `generated_http.gml` | One wrapper function per operation |
| `code.helpers` | `generated_helpers.gml` | Request struct, auth, cookie jar, URL encoding |
| `controller.createEvent` | `controller_create.gml` | Controller object Create event |
| `controller.cleanupEvent` | `controller_cleanup.gml` | Controller object Clean Up event |
| `controller.httpAsyncEvent` | `controller_http.gml` | Controller object Async HTTP event |
| `docs.schemas` | `schemas_codegen.js` | Feather doc partials for structs |
| `docs.functions` | `function_codegen.js` | Feather doc partials for endpoints |

The three `controller.*` outputs are raw event bodies. Point them straight at the event files of
your controller object (`obj_<prefix>_core`), or paste their contents into the matching events.

---

The content includes:

- **Schema structs**  
  Each OpenAPI `#/components/schemas/*` object becomes:

```gml
function GmUser(_id, _level, _name = undefined, ...) constructor
{
    id = _id;
    level = _level;
    name = _name;
    ...
    static __uid = 1234567890;

    /// @func validate()
    /// @param {String} _where
    /// @ignore
    static validate = function (_where = _GMFUNCTION_) {
        _where = $"{_where} :: GmUser.validate";
        // type checks (optional only when defined)
        ...
        return true;
    };
}
```

- **Endpoint wrappers**
  One per OpenAPI operation. Example:

```gml
/// @func gm_save_data_list()
/// @param {Real} _offset
/// @param {Real} _count
/// @param {String} _user_id
/// @param {Function} _callback
function gm_save_data_list(_offset = undefined, _count = undefined, _user_id = undefined, _callback = undefined) {
    // argument validation
    ...
    // build URL
    var _url = $"{GM_SERVER_URL}/save_data";
    // query params
    var _params = { offset : _offset, count : _count, userId : _user_id };
    // auth schemes permitted for this endpoint
    var _security = ["auth_bearer", "session_secret"];
    // make request
    return _gm_create_request(_url, _params, "GET", undefined, undefined, _security, _callback, _GMFUNCTION_);
}
```

> [!NOTE]
> Where the prefixes `gm`, `Gm` and `GM` are based on the provided command line `--prefix` to avoid naming collisions.

## Requirements

* .NET SDK 9.0+ (build & run the tool)
* An OpenAPI 3.x document (JSON or YAML)


## Command-line usage

`openapigen` is driven entirely by a config file — there is no direct-arguments mode.

```bat
openapigen --config <path/to/config.json>   Generate from a config file
openapigen --init <folder>                  Create config.json + JSON schema in a folder
openapigen --help                           Show help

  -c, --config=VALUE   Path to JSON config file.
  -i, --init=VALUE     Initialize a new config + schema in the given folder.
  -h, --help           Show help.
```

Exit codes: `0` success, `1` bad arguments, `2` option parse error, `3` config not found,
`5` config/schema error, `6` spec parse or validation error, `30` emitter failure,
`98` init failure, `99` unhandled exception.

## Configuration

`openapigen --init .` writes a `config.json` next to an `openapigen.schema.json`, so any
JSON-schema-aware editor gives you completion and validation. Every generated file has its own
`outputFile`, resolved relative to `root`, which is itself relative to the config file. That lets
you write straight into a GameMaker project tree:

```json
{
  "$schema": "./openapigen.schema.json",
  "input": "./openapi.json",
  "root": "../MyGame",
  "prefix": "gm",
  "requireOperationId": true,
  "code": {
    "endPoints": { "enabled": true, "outputFile": "./scripts/gm_http/gm_http.gml" },
    "schemas":   { "enabled": true, "outputFile": "./scripts/gm_schemas/gm_schemas.gml" },
    "helpers":   { "enabled": true, "outputFile": "./scripts/gm_helpers/gm_helpers.gml" }
  },
  "controller": {
    "createEvent":    { "enabled": true, "outputFile": "./objects/obj_gm_core/Create_0.gml" },
    "cleanupEvent":   { "enabled": true, "outputFile": "./objects/obj_gm_core/CleanUp_0.gml" },
    "httpAsyncEvent": { "enabled": true, "outputFile": "./objects/obj_gm_core/Other_62.gml" }
  },
  "docs": {
    "schemas":   { "enabled": false, "outputFile": "./docs/schemas_codegen.js" },
    "functions": { "enabled": false, "outputFile": "./docs/function_codegen.js" }
  }
}
```

| Key | Meaning |
|---|---|
| `input` | OpenAPI 3.x spec, JSON or YAML (detected from the extension) |
| `root` | Base directory every `outputFile` resolves against |
| `prefix` | Namespace prefix for generated symbols |
| `requireOperationId` | Error when an operation has no `operationId` (default `true`) |
| `<section>.<output>.enabled` | Set `false` to skip that file |
| `<section>.<output>.outputFile` | Destination, relative to `root` |

### Why `operationId` is required

Generated function names are permanent public API, and `operationId` is the only stable,
author-controlled source for them — a name derived from the URL changes whenever the path is
refactored, silently breaking every caller, and derived names collide. The tool therefore reports
each operation missing one and stops. For a third-party spec you cannot edit, set
`"requireOperationId": false` to fall back to path-derived names.

## Examples

Bootstrap a project and generate:

```bash
openapigen --init ./api
# edit ./api/config.json, point "input" at your spec
openapigen --config ./api/config.json
```

## Supported OpenAPI features

* OpenAPI 3.x documents (JSON or YAML)
* Operations & Parameters
* Path / Query / Header / Cookie params
* Required vs optional (optional → default undefined and validated only when defined)
* Request bodies
* Multiple media types supported; generates _content_type argument when needed
* Schemas
  * #/components/schemas objects become GML constructors
* Scalar types: string, integer (int32/int64), number (float/double), boolean
* Arrays of any supported type
* “Objects with properties” → struct; “free-form objects” → Struct (map)
* Enums: enum literal list is preserved and appended to JSDoc
* Security
  * components.securitySchemes captured
  * Per-operation security requirements mapped to a string array of scheme names

## Limitations

* `oneOf` / `anyOf` validate by trial, but are not emitted as distinct GML union types
* `allOf` is not yet flattened into a merged constructor
* XML / Protobuf bodies are ignored
* Multipart file fields are base64-encoded; a server expecting raw binary parts needs a custom
  converter registered with `<prefix>_request_body_set_converter`

## Configuration & naming

Because GML has no namespaces, the generator uses four prefixes:

* Example: `'gm'`

|Purpose | Example |
|--------|---------|
|Public functions	| `gm_` |
|Private helpers |	`_gm_` |
|Struct names	| `Gm` |
|Macros/constants	| `GM_` |


Endpoint names are derived from the path and operationId and formatted 
in snake_case, e.g. elements_profile_image_update_put.

Parameters are sanitized to `_snake_case` and adjusted if they would clash with
GML keywords. For invalid identifiers (e.g. names containing invalid characters), 
the emitter uses bracket syntax `self[$ "prop"]`.

## FAQ

Q: How do I pass a request body with multiple supported content types?

A: The generator adds a _content_type parameter (defaulting to a valid value).
Pass your preferred content type string when calling the function.

---

Q: What does the validate(_where) method do?

A: It checks each field’s type. Optional fields are validated only when defined.
Nested structs call their own validate. _where is used to prefix error messages.

---

Q: Where do auth tokens come from?

A: The auth helper calls _…request_auth_get_token(headerName). Implement that in
your project to return the current credential string (e.g. “Bearer …”).

---

Q: I see Struct in docs. What is that in GML?

A: It means the field is an arbitrary key→value map (a GML struct), not a typed
constructor the generator created.

---

## Contributing

Issues and PRs are welcome!

Follow the code style of existing files

Add tests where practical

Keep emitters deterministic (stable output ordering)

Update the Wiki pages for new features
