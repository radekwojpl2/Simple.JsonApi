# CLAUDE.md

Guidance for working in this repository.

## What this is

`Simple.JsonApi` is a strongly typed [JSON:API](https://jsonapi.org/format/) wire model on
System.Text.Json. It models documents and nothing else — no HTTP, no validation pipeline, no
persistence. That restraint is the product, not an omission; see [ROADMAP.md](ROADMAP.md) for what
is deliberately absent before proposing a feature.

## The specification

JSON:API 1.1 is the contract. When behaviour is in question, the specification decides it — not
intuition about what a REST API "should" do, and not what another JSON:API library happens to do.

| | |
| --- | --- |
| Current spec (1.1) | https://jsonapi.org/format/ |
| Version 1.0 | https://jsonapi.org/format/1.0/ |
| Atomic operations extension | https://jsonapi.org/ext/atomic/ |
| Spec source, issues and rationale | https://github.com/json-api/json-api |

The sections that come up most: *Document Structure*, *Resource Objects*, *Resource Identifier
Objects*, *Relationships*, *Errors*, *Content Negotiation*.

Two habits worth keeping. Read the surrounding paragraph, not just the sentence that matches your
search — the exceptions to a `MUST` are usually one sentence away, as with `id` and `lid`. And keep
`MUST` / `SHOULD` / `MAY` straight: a `MAY` is not a defect when unimplemented, a `MUST` is.

## Checking things

Whenever you verify a claim against a source — the specification, framework documentation, a
package's actual behaviour, this codebase — report it in the reply. Always two things:

1. **Where it came from.** The URL plus the section, the command you ran, or the `file:line`.
2. **One or two sentences from it, quoted verbatim**, so the reader can judge the claim without
   opening the source themselves.

For example:

> Checked https://jsonapi.org/format/ (*Resource Objects*): "The `id` member is not required when
> the resource object originates at the client and represents a new resource to be created on the
> server." So omitting `id` on a create is conformant, not a bug.

Never paraphrase a normative statement and present it as verified. If a claim comes from memory and
you have not opened the source, say so plainly and mark it `notSure` — an assured-sounding wrong
answer about the spec costs far more than an admitted uncertainty.

The same applies to behaviour: when a claim can be settled by running something, run it and show
the output rather than reasoning about what probably happens.

### When you are not sure

**Do not invent.** A guessed method name, overload, spec clause, package version or behaviour is
worse than no answer at all, because it reads exactly like a checked one and the reader has no way
to tell them apart. Not knowing is a result — report it as one.

Say so explicitly and mark it with the literal token `notSure`, so it can be scanned for and
grepped:

> `notSure` — whether Swashbuckle's `SwaggerGen` honours `IOpenApiOperationTransformer`. I have not
> run it. A minimal app with `AddSwaggerGen` and one annotated endpoint would settle it.

Three things belong in that note, and the marker alone is not enough:

1. The token `notSure`, spelled exactly that way.
2. **What specifically** is unknown — a named method, clause or behaviour, not a topic. "I am not
   sure about OpenAPI" tells the reader nothing; "I have not checked whether `X` does `Y`" does.
3. **What would resolve it** — the page to read, the command to run, the probe to write.

Do not soften a guess into a claim with hedging words. "Should work", "typically", "I believe" all
read as knowledge to someone skimming. Either check it, or mark it `notSure`.

If a question cannot be answered without inventing something, say that the answer is unknown and
stop there. An honest dead end is a usable result; a plausible fabrication costs the reader the time
to discover it is wrong, and costs the codebase whatever was built on it in the meantime.

## Layout

| Path | What | Targets |
| --- | --- | --- |
| `libs/JsonApiLite` | The wire model. **Zero package references — keep it that way.** | net8.0, net10.0 |
| `libs/JsonApiLite.OpenApi` | OpenAPI schema generation. Needs ASP.NET Core. | net10.0 |
| `libs/tests/JsonApiLite.Tests` | Tests, run against both TFMs. | net8.0, net10.0 |
| `JsonApiPoc.Api` | Sample minimal API. Consumes the **published** packages. | net10.0 |

Package ids and assembly names differ, which is a recurring source of confusion: `Simple.JsonApi`
ships `JsonApiLite.dll`, and `Simple.JsonApi.OpenApi` ships `JsonApiLite.OpenApi.dll`. The nuget ids
`JsonApiLite` and `JsonApiKit` were taken.

`JsonApiPoc.Api` is **not** in `JsonApiLite.sln`, so a solution build does not compile it. Build it
explicitly after changing anything it consumes.

## Build and test

```
dotnet build JsonApiLite.sln -c Release
dotnet test  JsonApiLite.sln -c Release
dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release   # not covered by the solution
```

The sample serves its OpenAPI document at `/openapi/v1.json` in Development, with Swagger UI at
`/swagger` and Scalar at `/scalar` over that one document. Reading that document back is the way to
verify a schema change; do not assume a schema is right because it compiles.

## Conventions

- **Conventional Commits.** semantic-release computes the version on merge to `main`. Never set a
  version by hand and never edit `CHANGELOG.md` — it is generated.
- **The core package takes no dependencies.** Anything needing a framework goes in a separate
  package alongside it, as `Simple.JsonApi.OpenApi` does. This is why the library multi-targets
  net8.0 and its OpenAPI companion does not.
- **Errors are static problem details.** Use the `JsonApi.NotFound` / `Invalid` / `Malformed`
  helpers in the sample's `JsonApi.cs`. Do not propose configurable error formatters or an error
  abstraction; that was removed deliberately.
- **No ternary returns.** Explicit `if` blocks with early returns, not `return cond ? a : b`, and no
  nested `? :` in an initializer. A single-level ternary in an initializer is fine and is used.
- **Comments explain why, not what.** The existing comments are the house style — they justify a
  decision or record a constraint. A comment restating the code is worse than none.
- **Public API carries XML docs**, and they say what the member is for, not what its name already
  says.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
[specs/002-typed-included-resources/plan.md](specs/002-typed-included-resources/plan.md)
<!-- SPECKIT END -->
