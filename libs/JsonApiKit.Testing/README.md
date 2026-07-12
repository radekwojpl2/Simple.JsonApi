# JsonApiKit.Testing

Test-client helpers for integration-testing a [JSON:API](https://jsonapi.org/format/) HTTP API. Framework-agnostic and dependency-free: it drives plain `HttpClient` and parses with `System.Text.Json.Nodes`, so it works against any JSON:API server — not just ones built with [JsonApiKit](../JsonApiKit/README.md).

## What's in it

- **`JsonApiMember`** — spec document member names (`data`, `attributes`, `included`, `links.next`, …) plus JsonApiKit's conventional pagination meta (`total`, `pageCount`), so tests navigate documents without magic strings.
- **`JsonApiMediaTypes`** — `application/vnd.api+json` and `application/problem+json` for content-type assertions.
- **`JsonApiHttpClientExtensions`**
  - `GetDocumentAsync(url)` — GET + parse to `JsonNode`; a non-success response throws with the status *and the response body*, so failing tests show the server's actual error. Also asserts the `application/vnd.api+json` media type — a spec invariant of successful responses — so no test checks it by hand.
  - `GetProblemAsync(url, expectedStatus)` / `ReadProblemAsync(response, expectedStatus)` — for error paths: assert the status and the `application/problem+json` media type, verify a `traceId` is present and strip it (it is per-request random, not contract), and return the parsed problem body for a full match.
  - `FindIdAsync(collectionUrl, attribute, value)` — id of the first resource whose string attribute matches, for locating seeded data without hard-coding ids. Inspects one page only.
- **`JsonNodeMatchExtensions`**
  - `ShouldMatch(expected)` — asserts a `JsonNode` against an anonymous object serialized with web (camelCase) conventions. Objects match as *subsets*: members the server returns beyond the expected ones are ignored. Arrays match element by element; scalars must be equal. On mismatch it throws `JsonApiMatchException` with the JSON path of the first difference plus both payloads pretty-printed.
  - `ShouldMatchExactly(expected)` — the strict variant: any member the server returns that the expectation does not declare is a mismatch too (`$.data[0]: unexpected members: attributes`), so one assertion covers the entire payload. Object member order stays insignificant; array order and count stay exact. Accepts an anonymous object or a prebuilt `JsonNode`, so expected documents can come from a builder.

## Usage

```csharp
using JsonApiKit.Testing;
using static JsonApiKit.Testing.JsonApiMember; // or a using alias

var id = await client.FindIdAsync("/api/companies", "name", "Acme Manufacturing");
var document = await client.GetDocumentAsync($"/api/companies/{id}");

document[Data].ShouldMatch(new
{
    type = "companies",
    id,
    attributes = new { name = "Acme Manufacturing" },
    links = new { self = $"/api/companies/{id}" }
});

// Exact matching pins the whole payload — an undeclared member fails the test. Pair it
// with a builder that produces complete expected documents (see the integration tests'
// Expect golden model for an example).
document.ShouldMatchExactly(expectedDocument);
```

A failing match reports where and what diverged:

```text
JsonApiKit.Testing.JsonApiMatchException : JSON mismatch at $.attributes.name:
expected "Acme Manufacturing" but got "Acme Corp"

Expected (subset):
{ ... }

Actual:
{ ... }
```

The assertions throw their own `JsonApiMatchException` rather than depending on xunit/NUnit/MSTest — which is what keeps the package dependency-free and usable from any test framework.
