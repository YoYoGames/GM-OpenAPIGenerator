# GM-OpenAPIGenerator — OpenAPI → GML code generator

**GM-OpenAPIGenerator** reads an OpenAPI 3.x specification (JSON or YAML) and generates a complete
GameMaker Language (GML) HTTP client layer:

- ✅ Strongly-typed **schema structs** with a runtime **validator** per schema
- ✅ **Endpoint wrappers** that build URLs, serialise bodies, attach headers/query params,
  inject credentials, and fire the request
- ✅ An **auth layer** driven by `components.securitySchemes`
- ✅ A transparent **cookie jar** and per-status-code **response hooks**
- ✅ **Feather/JSDoc** documentation for every generated function and struct member

This lets you call web APIs directly from GameMaker through one consistent, type-checked GML layer.

> ⚠️ GML has no namespaces. The generator uses a **configurable prefix** for public functions,
> private helpers, macros and struct names to avoid collisions.

📖 **Full documentation lives in the [Wiki](https://github.com/YoYoGames/GM-OpenAPIGenerator/wiki).**

---

## Table of contents

- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Command-line usage](#command-line-usage)
- [Configuration](#configuration)
- [What gets generated](#what-gets-generated)
- [Validation](#validation)
- [Naming](#naming)
- [Supported OpenAPI features](#supported-openapi-features)
- [Limitations](#limitations)
- [FAQ](#faq)
- [Contributing](#contributing)

---

## Requirements

- .NET SDK 9.0+ (build and run the tool)
- An OpenAPI 3.x document (JSON or YAML)
- GameMaker 2024+ for the generated GML

## Quick start

```bat
git clone https://github.com/YoYoGames/GM-OpenAPIGenerator.git
cd GM-OpenAPIGenerator\OpenAPIGenerator
dotnet build -c Release
```

The binary lands at `openapigen\bin\Release\net9.0\openapigen.exe`.

```bat
openapigen --init ./myapi
# edit myapi/config.json — set "input" to your spec path
openapigen --config ./myapi/config.json
```

Then copy the generated files into your GameMaker project and paste the three controller snippets
into `obj_gm_core`'s Create, Async HTTP and Clean Up events. See
[Getting Started](https://github.com/YoYoGames/GM-OpenAPIGenerator/wiki/getting_started).

## Command-line usage

`openapigen` is driven entirely by a config file — there is no direct-arguments mode, so every run
is reproducible and reviewable.

```
openapigen --config <path/to/config.json>   Generate from a config file
openapigen --init <folder> [--force]        Create config.json + JSON schema in a folder
openapigen --help                           Show help

  -c, --config=VALUE   Path to JSON config file.
  -i, --init=VALUE     Initialize a new config + schema in the given folder.
  -f, --force          With --init, overwrite an existing config.json.
  -h, --help           Show help.
```

`--init` **refuses to overwrite an existing `config.json`** and exits `98`; pass `--force` when
replacing it is what you meant. The schema file beside it is derived output and is refreshed either
way, so re-running `--init` is how you pick up a schema change after upgrading the tool.

> `-i` is `--init`. It is not `--input` — that mode, along with `--output`, `--prefix` and `--docs`,
> was removed in favour of the config file.

Exit codes: `0` success, `1` bad arguments, `2` option parse error, `3` config not found,
`5` config/path error (including two outputs resolving to the same file), `6` spec parse or
validation error, `30` emitter failure, `98` `--init` failure or refusal, `99` unhandled exception.

## Configuration

`openapigen --init .` writes a `config.json` next to an `openapigen.schema.json`, so any
JSON-schema-aware editor gives completion and validation. Every generated file has its own
`outputFile`, resolved against `root`, which is itself resolved against the config file's own
directory. That lets you write straight into a GameMaker project tree:

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
| `input` | OpenAPI 3.x spec, JSON or YAML (detected from the extension), relative to this config |
| `root` | Base directory every `outputFile` resolves against |
| `prefix` | Namespace prefix for generated symbols |
| `requireOperationId` | Error when an operation has no `operationId` (default `true`) |
| `<section>.<output>.enabled` | Set `false` to skip that file |
| `<section>.<output>.outputFile` | Destination, relative to `root` |

Three rules apply to the resolved paths:

- **Two enabled outputs may not resolve to the same file** — an error (exit `5`), because emitters
  run in a fixed order and one output would silently overwrite the other.
- **An output resolving outside `root` warns but proceeds** — it can be deliberate.
- **`~` expands only at the start of a path**; elsewhere it is an ordinary filename character.

Comments (`//`) and trailing commas are accepted in `config.json`, but comments do not survive the
rewrite that patches the `$schema` key.

### Why `operationId` is required

Generated function names are permanent public API, and `operationId` is the only stable,
author-controlled source for them — a name derived from the URL changes whenever the path is
refactored, silently breaking every caller, and derived names collide (measured: 4 collisions in 261
operations on a real spec). The tool therefore reports each operation missing one and stops. For a
third-party spec you cannot edit, set `"requireOperationId": false` to fall back to path-derived
names.

## What gets generated

Eight files, each independently placeable and switchable via `config.json`. The two `docs.*` outputs
are **off by default**.

| Output | Default file | Contents |
|---|---|---|
| `code.schemas` | `generated_schemas.gml` | Constructors + a standalone `_validate` per schema |
| `code.endPoints` | `generated_http.gml` | One wrapper function per operation |
| `code.helpers` | `generated_helpers.gml` | `GmRequest`, auth store, converters, hooks, cookie jar, URL encoding |
| `controller.createEvent` | `controller_create.gml` | Controller object Create event |
| `controller.cleanupEvent` | `controller_cleanup.gml` | Controller object Clean Up event |
| `controller.httpAsyncEvent` | `controller_http.gml` | Controller object Async HTTP event |
| `docs.schemas` | `schemas_codegen.js` | `gm-ext` doc partials for structs |
| `docs.functions` | `function_codegen.js` | `gm-ext` doc partials for endpoints |

The three `controller.*` outputs are raw event bodies, so they can be written straight over the
event files of your controller object (`obj_<prefix>_core`). `Other_62.gml` is the Async HTTP event.

### Schema structs

Each `#/components/schemas` object becomes a constructor. The **member keeps the name the spec
used**; only the constructor *argument* is snake_cased:

```gml
/**
 * @func GmApplicationConfiguration(_name, _type, _parent, _id = undefined, _description = undefined)
 * @param {String} _name The application-configuration specific unique ID.
 * @param {String} _type The fully-qualified Java type of ApplicationConfiguration.
 * @param {Struct.GmApplication} _parent
 * @param {String} [_id] The database assigned ID for the application configuration.
 * @param {String} [_description]
 */
function GmApplicationConfiguration(_name, _type, _parent, _id = undefined, _description = undefined) constructor
{
    name = _name;
    type = _type;
    parent = _parent;
    id = _id;
    description = _description;
}
```

Each schema also gets a **standalone validator function** — not a method on the struct:

```gml
function GmApplicationConfiguration_validate(__inst__, __where__ = _GMFUNCTION_)
{
    __where__ = $"{__where__} :: GmApplicationConfiguration_validate";

    if (!is_struct(__inst__)) throw $"{__where__} :: expected Struct.GmApplicationConfiguration";

    if (!is_string(__inst__[$ "name"])) throw $"{__where__} :: 'name' expected String";
    if (!is_string(__inst__[$ "type"])) throw $"{__where__} :: 'type' expected String";
    GmApplication_validate(__inst__[$ "parent"], $"{__where__} :: 'parent'");
    if (!is_undefined(__inst__[$ "id"]))
    {
        if (!is_string(__inst__[$ "id"])) throw $"{__where__} :: 'id' expected String";
    }
    if (!is_undefined(__inst__[$ "description"]))
    {
        if (!is_string(__inst__[$ "description"])) throw $"{__where__} :: 'description' expected String";
    }
}
```

Required fields are always checked, optional ones only when defined, and a nested schema is
validated by its own validator with the field name folded into the location string.

> **Validators `throw` a plain string, not an exception struct.** In a `catch` block use
> `is_struct(_e) ? _e.message : string(_e)` — reading `_e.message` directly will itself fail.

### Endpoint wrappers

One public function per operation. Required parameters come first, then optional ones (using the
spec's `default` when it provides one), then `_body`, then `_content_type` where several media types
are allowed, then `_callback`:

```gml
/**
 * @func gm_get_advanced_inventory_items(_offset = 0, _count = 20, _user_id = undefined, _search = undefined, _callback = undefined)
 * Searches all inventory items in the system and returns the metadata for all matches against the given search filter.
 * @param {Real} [_offset]
 * @param {Real} [_count]
 * @param {String} [_user_id]
 * @param {String} [_search]
 * @param {Function} [_callback] Callback with signature (status, data, request).
 */
function gm_get_advanced_inventory_items(_offset = 0, _count = 20, _user_id = undefined, _search = undefined, _callback = undefined)
{
    var __base_url__ = _gm_options_get_rest_url();

    // argument validation
    var __where__ = _GMFUNCTION_;

    // ... per-argument type checks ...

    // build url path
    var __url__ = $"{__base_url__}/inventory/advanced";

    // create query params struct
    var __params__ = { offset : _offset, count : _count, userId : _user_id, search : _search };

    var __security__ = [ "auth_bearer", "session_secret" ];

    return _gm_create_request(__url__, __params__, "GET", undefined, undefined, undefined, __security__, undefined, _callback, _GMFUNCTION_);
}
```

Call it with a callback of `(status, data, request)`:

```gml
gm_get_advanced_inventory_items(0, 20, undefined, "sword", function(_status, _data, _request) {
    show_debug_message($"status={_status} data={json_stringify(_data)}");
});
```

### Auth, cookies and hooks

The helpers file generates the credential store — you do not implement it:

```gml
gm_request_auth_set_token("auth_bearer", "eyJhbGci...");   // key is the scheme name from the spec
gm_request_body_set_converter("application/xml", fn);      // custom body serialiser
gm_request_response_set_hook(401, fn);                     // per-status-code interceptor
gm_cookie_set / gm_cookie_get / gm_cookie_delete / gm_cookie_clear
```

`Set-Cookie` response headers are captured into a jar automatically and re-injected on every
subsequent request, so `in: cookie` parameters are deliberately **not** exposed as function
arguments.

The base URL and debug flag are read from GameMaker Extension options named after the struct prefix:

```gml
extension_get_option_value("Gm", "server_rest_url")
extension_get_option_value("Gm", "debug_logging")
```

## Validation

The parsed spec is checked before anything is generated. Every diagnostic is reported; any error
stops the run.

| Code | Severity | Meaning |
|---|---|---|
| `IR_OP_001` | Error¹ | An operation has no `operationId` |
| `IR_PATH_001` | Error | A path parameter is not present in its path template |
| `IR_SYM_001` | Error¹ | Two operations ask for the same GML function name |
| `IR_SYM_002` | Error² | Two schemas share a name |

¹ Downgraded to a warning when `"requireOperationId": false`.
² Unreachable by construction; kept as an assertion.

Problems that do not prevent generation — a response missing its required `description`, for
example — are reported as warnings and the run continues.

## Naming

| Token | Example (`gm`) | Used for |
|---|---|---|
| `{prefix}_` | `gm_` | Public functions |
| `_{prefix}_` | `_gm_` | Private helpers |
| `{Prefix}` | `Gm` | Struct constructor names |
| `{PREFIX}_` | `GM_` | Macros and constants |

An endpoint's name is `{prefix}_{snake(operationId)}` — **the tag is not involved**. Tags are only
used by the path-derived fallback that applies when `requireOperationId` is `false`.

Struct members keep the spec's own casing; a member that is not a legal bare GML identifier — which
includes every GML reserved word and every **global** built-in variable (`fps`, `health`, `lives`,
…) — is emitted through `self[$ "name"]`. Full rules in
[Naming Conventions](https://github.com/YoYoGames/GM-OpenAPIGenerator/wiki/naming_conventions).

## Supported OpenAPI features

- OpenAPI 3.x documents (JSON or YAML)
- Operations and parameters — path / query / header / cookie
- Required vs optional (optional defaults to `undefined` and is validated only when defined)
- Request bodies: `application/json`, `application/x-www-form-urlencoded`, `multipart/form-data`,
  `text/plain`, `*/*`, and **any `application/…+json` subtype** (`merge-patch+json`, `hal+json`,
  `problem+json`, …) — these serialise as JSON but keep their own media type on the request, since a
  server dispatches on it. A `_content_type` argument is generated when several media types are allowed
- Schemas: `#/components/schemas` objects become GML constructors; inline schemas are named from
  their owner
- Scalars (`string`, `integer`, `number`, `boolean`), arrays, free-form objects, enums
- Security: `components.securitySchemes` plus per-operation requirements — Basic, Bearer, API key
  (header / query / cookie), OAuth 2 and OpenID Connect

## Limitations

- `oneOf` / `anyOf` validate by trial but are not emitted as distinct GML union types
- `allOf` is not flattened into a merged constructor
- **Media types are matched literally, so parameters are not understood.**
  `application/json; charset=utf-8` is not recognised as JSON and its body is dropped with a warning.
  Declare the bare type (`application/json`) in the spec. This is deliberate: stripping parameters
  generally would also strip `multipart/form-data; boundary=…`, where the parameter is meaningful
- OAuth 2 has no flow scaffolding — the stored token is injected as a Bearer credential
- `input` must be a local file; URLs are not supported
- XML / Protobuf bodies are ignored
- Multipart binary fields are base64-encoded with `Content-Transfer-Encoding: base64` rather than
  written as raw bytes; a server expecting raw parts needs a custom converter registered through
  `<prefix>_request_body_set_converter`

## FAQ

**How do I pass a request body with multiple supported content types?**
The generator adds a `_content_type` parameter defaulting to the first media type the spec lists.
Pass your preferred content-type string when calling the function.

**What does the validator do?**
`GmX_validate(__inst__, __where__)` is a standalone function that checks each field's type. Optional
fields are validated only when defined, nested structs call their own validator, and `__where__`
prefixes the error message so a deep failure still says which field it was. It `throw`s a plain
string.

**Where do auth tokens come from?**
You store them: `gm_request_auth_set_token("<scheme name>", "<token>")`, keyed by the scheme name in
`components.securitySchemes`. The generated helper retrieves and injects the credential on every
request that declares that scheme. A missing token logs a debug message and is skipped.

**I see `Struct` in the docs. What is that in GML?**
An arbitrary key→value map (a plain GML struct), not a typed constructor the generator created.

**Do I need to place the controller object in a room?**
No. `_gm_get_singleton()` creates it on first use via `instance_create_depth`. Placing one yourself
as well gives you two controllers, each with its own request and hook maps.

## Contributing

Issues and PRs are welcome.

- Follow the code style of the existing files
- Add tests where practical
- Keep emitters deterministic (stable output ordering)
- Update the Wiki pages for new features

## Licence

See [LICENSE](LICENSE).
