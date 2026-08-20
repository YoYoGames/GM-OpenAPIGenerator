# Contributing

Thanks for taking a look. This is a code generator: it reads an OpenAPI 3.x document and emits GML.
Almost everything worth knowing follows from that one fact - the output is source code somebody else
has to read and debug, so it is held to the same standard as hand-written code.

## Building

```bash
cd OpenAPIGenerator
dotnet build -c Release
```

Requires the .NET 9 SDK. The binary lands at `openapigen/bin/Release/net9.0/openapigen`.

CI builds every pull request; pushes to `main` publish a self-contained build for Windows, Linux and
macOS to the `nightly` prerelease.

## Trying a change

There is no test project - writing one means supplying an OpenAPI document to generate from, and that
choice belongs to whoever writes it. In the meantime the practical regression check is a diff:

```bash
openapigen --config ./yourspec/config.json     # before your change
# ... make the change, rebuild ...
openapigen --config ./yourspec/config.json     # after
```

and compare the generated `.gml`. **Most changes should produce no diff at all** on a spec that does
not exercise them; a diff you did not expect is the finding. Use a real-world spec if you can - the
interesting bugs have consistently come from shapes a small hand-written spec does not contain
(nullable fields, `$ref` cycles, `+json` media types, cookie auth).

If you fix something, a small spec that reproduces it is the most useful thing you can attach.

## Layout

```
OpenAPIGenerator/
  codegencore/     language-agnostic IR and code writers (GmlWriter and friends)
  openapigen/
    Parsing/       OpenAPI document -> IR, and the validation rules
    Emitters/      IR -> GML
    Helpers/       naming, signatures, the shared validator emitter
    Models/Config/ the config.json model
```

## Conventions that matter

**Policy lives in the validation layer, never in the parser.** The parser is a transform: it turns a
document into IR and does not decide what is acceptable. Anything that should warn or stop the run is
an `IIrRule` in `Parsing/Validation/` with a diagnostic code. A rule that has to run *before* the
document is dereferenced is an `IDocumentRule` - see `IR_REF_001` for why that distinction exists.

**There is one validator emitter.** `ValueSchemaValidatorEmitter` produces every runtime type check,
for struct fields and endpoint arguments alike. If you need a different check, change it there rather
than adding a second predicate somewhere else - this codebase has had the same bug fixed twice in two
places before, and once more in a third that was missed.

**Generated GML must read as if a person wrote it.** Let `GmlWriter` handle indentation rather than
building strings with tabs, skip decorative separator comments, and comment the *why* when it is not
obvious. Generator-owned temporaries are `__name__`, so a spec-derived parameter can never shadow one.

**Prefer detection over a budget.** A depth limit or a retry count that papers over a malformed input
tends to hide the real problem; a cycle check or an explicit diagnostic does not.

## Known limitations

The README's Limitations section lists what the tool deliberately does not do, and several entries
carry the fix direction for anyone who wants to take one on. The integer round-trip limit and the
media-type parameter handling are both written up that way.

## Commits

Match the existing log: a short imperative subject, and a body only when the reasoning is not obvious
from the diff.
