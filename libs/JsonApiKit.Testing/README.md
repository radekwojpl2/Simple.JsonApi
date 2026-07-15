# JsonApiKit.Testing

Test-client helpers for integration-testing a [JSON:API](https://jsonapi.org/format/) HTTP API. Framework-agnostic and dependency-free: it drives plain `HttpClient` and parses with `System.Text.Json.Nodes`, so it works against any JSON:API server — not just ones built with [JsonApiKit](../JsonApiKit/README.md).

## What's in it

### `Protocol/` — spec vocabulary

- **`JsonApiMember`** — spec document member names (`data`, `attributes`, `included`, `links.next`, …) plus JsonApiKit's conventional pagination meta (`total`, `pageCount`), so tests navigate documents without magic strings.
- **`JsonApiMediaTypes`** — `application/vnd.api+json` and `application/problem+json` for content-type assertions.

### `Documents/` — request bodies and expected documents

- **`JsonApiDocuments`** — builders for both sides of the wire contract. Ids are strings, as the spec defines them; a suite for a server with numeric keys typically wraps these with converting overloads.
  - Write bodies: `Post`, `Patch`, and `Linkage` (a to-one relationship update, or explicit `data: null` to clear).
  - Deliberately non-conformant bodies for the spec's rejection tests: `PostWithoutType` (400), `PostWithArrayData` (400), `PostWithClientGeneratedId` (403), `PatchWithDatalessRelationship` (400).
  - Full expected response documents for `ShouldMatchExactly`: `Single`, `Page` (with complete pagination links and `total`/`pageCount` meta), `Related`, `Linkage`, and `Problem` (RFC 7807 body with ASP.NET's default type URIs).
- **`ResourceExpectation`** — one expected resource object: type/id, attributes and relationships in declaration order, `links.self`, with nulls omitted mirroring JsonApiKit's serialization rules. `Fields(...)` trims it to a sparse fieldset. Wrap it in named per-resource builders that take the entities the test arranged, so expectations form a golden model independent of the production mapping code.

### `Http/` — driving the server

- **`JsonApiHttpClientExtensions`**
  - `GetDocumentAsync(url)` — GET + parse to `JsonNode`; a non-success response throws with the status *and the response body*, so failing tests show the server's actual error. Also asserts the `application/vnd.api+json` media type — a spec invariant of successful responses — so no test checks it by hand.
  - `PostJsonApiAsync(url, document)` / `PatchJsonApiAsync(url, document)` — send an anonymous object serialized with web (camelCase) conventions as `application/vnd.api+json` with no media type parameters (a charset parameter would rightly draw a 415 from a spec-strict server).
  - `GetProblemAsync(url, expectedStatus)` / `ReadProblemAsync(response, expectedStatus)` — for error paths: assert the status and the `application/problem+json` media type, verify a `traceId` is present and strip it (it is per-request random, not contract), and return the parsed problem body for a full match. `GetProblemAsync` covers GET error paths; `ReadProblemAsync` takes the `HttpResponseMessage` a failed POST/PATCH/DELETE returned.
  - `FindIdAsync(collectionUrl, attribute, value)` — id of the first resource whose string attribute matches, for locating seeded data without hard-coding ids. Inspects one page only.
  - `ShouldBeSuccessfulWrite()` — asserts the spec's successful-write contract on an `HttpResponseMessage`: 200 OK with a response document, or 204 No Content.

### `Assertions/` — asserting on documents

- **`JsonNodeAssertions`**
  - `ShouldMatch(expected)` — asserts a `JsonNode` against an anonymous object serialized with web (camelCase) conventions. Objects match as *subsets*: members the server returns beyond the expected ones are ignored. Arrays match element by element; scalars must be equal. On mismatch it throws `JsonApiMatchException` with the JSON path of the first difference plus both payloads pretty-printed.
  - `ShouldMatchExactly(expected)` — the strict variant: any member the server returns that the expectation does not declare is a mismatch too (`$.data[0]: unexpected members: attributes`), so one assertion covers the entire payload. Object member order stays insignificant; array order and count stay exact. Accepts an anonymous object or a prebuilt `JsonNode`, so expected documents can come from a builder.
- **`JsonApiDocumentAssertions`** — spec-level assertions for conformance tests, also throwing `JsonApiMatchException`:
  - `ShouldBeSortedBy(field, descending)` — the data array is ordered by an attribute, numerically for JSON numbers and ordinally for strings.
  - `ShouldHaveAvailableLink(key)` / `ShouldHaveUnavailableLink(key)` — pagination link availability, honoring the spec's rule that an unavailable link may be either omitted or explicitly null.

## Usage

All examples assume:

```csharp
using JsonApiKit.Testing;
using static JsonApiKit.Testing.JsonApiMember; // or a using alias
```

### Fetching and matching

```csharp
var id = await client.FindIdAsync("/api/companies", "name", "Acme Manufacturing");
var document = await client.GetDocumentAsync($"/api/companies/{id}");

document[Data].ShouldMatch(new
{
    type = "companies",
    id,
    attributes = new { name = "Acme Manufacturing" },
    links = new { self = $"/api/companies/{id}" }
});
```

### Creating a resource

`JsonApiDocuments.Post` builds the resource document — attributes as an anonymous object, relationships as `(name, targetType, targetId)` tuples. `PostJsonApiAsync` returns the raw `HttpResponseMessage`, so the test can assert the parts of a 201 the spec prescribes — status, `Location` header, and the created document:

```csharp
var response = await client.PostJsonApiAsync("/api/contacts", JsonApiDocuments.Post("contacts",
    new { firstName = "Beata", lastName = "Nowak" },
    ("company", "companies", companyId)));

Assert.Equal(HttpStatusCode.Created, response.StatusCode);
var location = response.Headers.Location!.ToString();

var created = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
created[Data].ShouldMatch(new
{
    type = "contacts",
    attributes = new { firstName = "Beata", lastName = "Nowak" }
});

// The Location header must point at the new resource.
var fetched = await client.GetDocumentAsync(location);
fetched[Data].ShouldMatch(new { id = created[Data]![Id]!.GetValue<string>() });
```

### Updating a resource

Omitted attributes and relationships keep their current values; a relationship tuple with a null id clears it:

```csharp
var response = await client.PatchJsonApiAsync($"/api/contacts/{id}",
    JsonApiDocuments.Patch("contacts", id, new { email = "beata.nowak@example.com" }));

// The spec's successful-write contract: 200 OK with a response document, or 204 No Content.
response.ShouldBeSuccessfulWrite();
```

### Error paths

`GetProblemAsync` asserts the status and problem-details media type, strips the random `traceId`, and returns the body, so an exact match can pin the whole error contract:

```csharp
var problem = await client.GetProblemAsync("/api/contacts/99999", 404);

problem.ShouldMatchExactly(new
{
    type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    title = "Not found",
    status = 404,
    detail = "Contact '99999' does not exist."
});
```

For write error paths, feed the response a POST/PATCH/DELETE returned to `ReadProblemAsync`:

```csharp
var response = await client.PostJsonApiAsync("/api/contacts",
    JsonApiDocuments.Post("contacts", new { firstName = "" }));

var problem = await response.ReadProblemAsync(422);
problem.ShouldMatch(new { title = "Validation failed" });
```

### Walking pagination

The `JsonApiMember` constants cover the pagination links and JsonApiKit's `total`/`pageCount` meta:

```csharp
var page = await client.GetDocumentAsync("/api/contacts?page[size]=10");

page[Meta].ShouldMatch(new { total = 27, pageCount = 3 });

while (page[Links]?[Next]?.GetValue<string>() is { } next)
{
    page = await client.GetDocumentAsync(next);
}

page.ShouldHaveUnavailableLink(Next); // last page
```

### Spec-conformance assertions

`JsonApiDocumentAssertions` covers rules from the spec's fetching sections. Sorting, per ["the sort order for each sort field MUST be ascending unless it is prefixed with a minus"](https://jsonapi.org/format/#fetching-sorting) — numbers compare numerically, strings ordinally:

```csharp
var ascending = await client.GetDocumentAsync("/api/deals?sort=amount");
ascending.ShouldBeSortedBy("amount");

var descending = await client.GetDocumentAsync("/api/deals?sort=-amount");
descending.ShouldBeSortedBy("amount", descending: true);
```

Pagination link availability, per ["keys MUST either be omitted or have a null value to indicate that a particular link is unavailable"](https://jsonapi.org/format/#fetching-pagination) — `ShouldHaveUnavailableLink` accepts both forms:

```csharp
var first = await client.GetDocumentAsync("/api/deals?page[size]=1&page[number]=1");
first.ShouldHaveUnavailableLink(Prev); // omitted or explicit null both pass
first.ShouldHaveAvailableLink(Next);
first.ShouldHaveAvailableLink(Last);
```

And the write contract on deletions, per ["the server MUST return either a 200 OK status code and response document or a 204 No Content status code"](https://jsonapi.org/format/#crud-deleting-responses):

```csharp
var deleted = await client.DeleteAsync($"/api/deals/{id}");
deleted.ShouldBeSuccessfulWrite();
```

### Exact matching with document builders

Exact matching pins the whole payload — an undeclared member fails the test. `JsonApiDocuments` and `ResourceExpectation` build the complete expected documents to pair with it:

```csharp
ResourceExpectation Company(string id, string name) =>
    new ResourceExpectation("companies", id, $"/api/companies/{id}")
        .Attr("name", name)
        .RelatedOnlyRel("contacts");

var document = await client.GetDocumentAsync($"/api/companies/{id}");
document.ShouldMatchExactly(JsonApiDocuments.Single(Company(id, "Acme Manufacturing")));

// Collection documents come with full pagination links and total/pageCount meta.
var page = await client.GetDocumentAsync("/api/companies?page[number]=1&page[size]=2");
page.ShouldMatchExactly(JsonApiDocuments.Page("/api/companies", query: null, number: 1, size: 2,
    total: 7, [Company(id, "Acme Manufacturing"), Company(otherId, "Borealis Ltd")]));
```

In a real suite the per-resource builders (like `Company` above) take the entities the test arranged and live in test infrastructure — a golden model that re-encodes the wire contract independently of the production mapping code, so a mapping bug cannot hide inside the expectation.

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
