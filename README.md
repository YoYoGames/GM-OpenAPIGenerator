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
- [Examples](#examples)
- [Supported OpenAPI features](#supported-openapi-features)
- [Limitations](#limitations)
- [Configuration & naming](#configuration--naming)
- [FAQ](#faq)
- [Contributing](#contributing)

---

## Features

- **OpenAPI 3.x** JSON or YAML input
- Generates a single `generated_http.gml` containing:
  - **Schemas**: GML constructors (structs) + `validate()` methods
  - **Endpoints**: one wrapper per operation
  - **Auth helper**: `_…request_apply_auth(...)` (separate file)
- **Parameter handling**
  - Required params first, optional after (defaulted to `undefined`)
  - Path params are inserted into the URL
  - Query params collected into a struct (undefined entries ignored downstream)
  - Header params emitted into a `ds_map`
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

By default the tool writes:

```
/out/
  generated_schemas.gml // with all the schema types
  generated_http.gml // with all the endpoints
  generated_helpers.gml // with all the auxiliar functions
```

and then a set of files that need to be placed inside a user created object (manager, `obj_<namespace>_core`)

```
controller_create.gml // copy into the create event
controller_http.gml // copy into the HTTP async event
controller_cleanup.gml // copy into the cleanup event
```

---

The content includes:

- **Schema structs**  
  Each OpenAPI `#/components/schemas/*` object becomes:

```gml
function <Namespace>User(_id, _level, _name = undefined, ...) constructor
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
        _where = $"{_where} :: <Namespace>User.validate";
        // type checks (optional only when defined)
        ...
        return true;
    };
}
```

- **Endpoint wrappers**
  One per OpenAPI operation. Example:

```gml
/// @func <namespace>_save_data_list()
/// @param {Real} _offset
/// @param {Real} _count
/// @param {String} _user_id
/// @param {Function} _callback
function <namespace>_save_data_list(_offset = undefined, _count = undefined, _user_id = undefined, _callback = undefined) {
    // argument validation
    ...
    // build URL
    var _url = $"{<NAMESPACE>_SERVER_URL}/save_data";
    // query params
    var _params = { offset : _offset, count : _count, userId : _user_id };
    // auth schemes permitted for this endpoint
    var _security = ["auth_bearer", "session_secret"];
    // make request
    return _<namespace>_create_request(_url, _params, "GET", undefined, undefined, _security, _callback, _GMFUNCTION_);
}
```

## Requirements

* .NET SDK 8.0+ (build & run the tool)
* An OpenAPI 3.x document (JSON or YAML)


## Command-line usage

```bat
GMSwaggerCodeGen --input <file|url> --output <dir> [options]

Required:
  -i, --input           Path or URL to OpenAPI 3.x spec (JSON or YAML)
  -o, --output          Output directory for generated GML files

Optional:
      --prefix          The namespace prefix to be used (default: gm)

General:
  -?, -h, --help        Show help
  -v, --version         Show version
```

The prefix options feed the internal GmlNaming used during generation.

## Examples

Generate from a local JSON file

```bash
./GMSwaggerCodeGen.exe -- \
  --input ./openapi.json \
  --output ./out \
  --prefix 'gm'
```

## Supported OpenAPI features

* OpenAPI 3.x documents (JSON)
* Operations & Parameters
* Path / Query / Header params
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

* oneOf / anyOf / discriminators are not yet emitted as unions in GML
* Inline object schemas without a component id are treated as free-form Struct
* XML/Protobuf bodies are ignored
* File uploads/downloads require custom handling (depending on your HTTP layer)

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
