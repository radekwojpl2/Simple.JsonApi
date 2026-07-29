# Quickstart: Verifying OpenAPI Envelope Schemas

**Feature**: `003-openapi-envelope-schemas` | **Date**: 2026-07-29

How to see each story working. Compiling is not evidence (Principle II); every step here ends in
output you read.

---

## Before you start: the state of the tree

Two things are true right now and will confuse verification if you do not know them.

**The sample does not start.** Reproduce it first, so you know what "fixed" looks like:

```powershell
dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release
dotnet run   --project JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release --no-build
```

```
Unhandled exception. System.ArgumentException: 'JsonApiLite.ResourceDocument`4[...ContactAttributes,
...ContactRelationships,JsonApiLite.Meta,...ContactIncluded]' is not a JSON:API document the
annotation understands — expected ResourceDocument<>, ResourceCollectionDocument<>, ...
   at JsonApiLite.OpenApi.JsonApiBody.Describe(Type documentType)
   at Program.<Main>$(String[] args) in D:\git\JsonApiPoc\JsonApiPoc.Api\Program.cs:line 96
```

**The sample consumes published packages, not your local build.**
`JsonApiPoc.Api/JsonApiPoc.Api.csproj:13-14` pins `Simple.JsonApi` and `Simple.JsonApi.OpenApi` at
`1.1.1-preview.10.44`. Nothing you change locally reaches the sample until it is published. To
verify against the sample, temporarily swap those two `PackageReference` lines for
`ProjectReference`s to `libs/JsonApiLite` and `libs/JsonApiLite.OpenApi`, and **revert before
committing**. Say which mode produced any output you report — the constitution requires that gap be
stated rather than glossed over.

---

## The gates, every time

```powershell
dotnet build JsonApiLite.sln -c Release
dotnet test  JsonApiLite.sln -c Release
dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release   # not covered by the solution
```

The third is separate because `JsonApiPoc.Api` is not in the solution. Note that the working tree
currently has an uncommitted edit adding it — if a solution build starts compiling the sample, that
is why, and it contradicts `CLAUDE.md`.

---

## Story 1 — the annotation accepts a declared sideload shape

**Unit evidence** (`libs/tests/JsonApiLite.OpenApi.Tests`, new in this feature):

```powershell
dotnet test libs/tests/JsonApiLite.OpenApi.Tests -c Release
```

Assert that building a schema for `ResourceDocument<A,R,M,I>` succeeds and produces a `data` member
identical to the one for `ResourceDocument<A,R>` — FR-002, "described identically". Assert that an
unsupported type still throws with the message naming the accepted forms — FR-003.

**End-to-end evidence**: repoint the sample at the local projects, then:

```powershell
dotnet run --project JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release
curl http://localhost:<port>/openapi/v1.json
```

It starts, and the document is served. That is SC-001.

---

## Story 2 — the declared metadata shape is published

```powershell
curl -s http://localhost:<port>/openapi/v1.json | ConvertFrom-Json |
  ForEach-Object { $_.paths.'/contacts'.get.responses.'200'.content.'application/vnd.api+json'.schema.properties.meta }
```

Expected — `PageMeta` walked into named, typed members (FR-004):

```jsonc
{ "type": "object", "properties": { "total": { "type": "integer" }, "pageCount": { "type": "integer" } } }
```

Then check the contrast case. `GET /contacts/{id}` declares `TMeta` as `Meta`
(`JsonApiPoc.Api/Program.cs:127`), so its `meta` must be `{ "type": "object" }` with **no**
properties — unconstrained, never invented (FR-007). If you see a `members` property there, the
derivation guard from research.md R2 is missing.

---

## Story 3 — the paged endpoint is visibly paged

On `/contacts` (a collection), `links` must carry `self`, `first`, `prev`, `next`, `last`, each an
`anyOf` of a URI string and a `{ href, meta }` object (FR-009). On `/contacts/{id}` (a single
resource), only `self` — no pagination, per the spec's "Pagination links **MUST** appear in the
links object that corresponds to a collection."

Nothing may appear in a `required` array (FR-010, FR-015):

```powershell
curl -s http://localhost:<port>/openapi/v1.json | Select-String -Pattern '"required"'
```

Every hit should be `["data"]`, `["errors"]`, `["type","id"]` or `["href"]` — never a link, `meta`
or `included`.

**Expect `describedby` to be missing, and do not add it.** `Links` has no such member
(`libs/JsonApiLite/Documents/Links.cs:7-16`), so it cannot be described. FR-008a makes this a
requirement: "The description MUST NOT describe a link member the library cannot produce." A
`describedby` in the emitted document is a defect, not an improvement.

---

## Story 4 — sideloaded resources are described from the declaration

`GET /contacts/{id}` declares `ContactIncluded`, which names `companies` and `tags`
(`JsonApiPoc.Api/Contracts.cs:51-55`). Its `included` must be one array whose `items` is an `anyOf`
over both resource schemas, each with its `type` pinned to the right constant (FR-011, FR-012):

```powershell
curl -s http://localhost:<port>/openapi/v1.json | ConvertFrom-Json |
  ForEach-Object { $_.paths.'/contacts/{id}'.get.responses.'200'.content.'application/vnd.api+json'.schema.properties.included }
```

Contrast case: any endpoint declaring no sideload shape resolves `TIncluded` to `AnyIncluded` and
must describe an unconstrained resource array — no `companies`, no `tags`, no claimed type (FR-013).

---

## The end-to-end check that covers everything

Validate a real response against the published description (SC-005, FR-017):

```powershell
curl -s -H "Accept: application/vnd.api+json" "http://localhost:<port>/contacts?include=company" > response.json
```

Every member the response carries must be described, and the response must still validate. Because
`additionalProperties` is never set, an undescribed member does not fail validation — so a passing
validation alone does not prove the envelope is described. Compare the response's members against
the schema's `properties` by eye as well.

---

## What must not change

```powershell
dotnet test JsonApiLite.sln -c Release
```

Every existing wire-format test must pass **unmodified** (SC-006, FR-019). If a serialization test
needed editing, the change went further than this feature allows — it describes documents, it does
not change them.
