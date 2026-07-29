# Roadmap

What is planned, what is not, and why.

_Last reviewed: 2026-07-27._

Ordering rather than dates, and no version numbers: releases are cut by semantic-release from
Conventional Commits when a branch merges, so the next number is not something this file can
promise. [CHANGELOG.md](CHANGELOG.md) records what shipped; this is the only forward-looking
document.

The ordering is the maintainer's current read, not a commitment. A reported use case moves an item
faster than anything else here — see [Requests](#requests).

## Works today

Stated first, because these are the questions that get asked and all of them already have an
answer.

- **Minimal APIs.** `AcceptsJsonApi`, `ProducesJsonApi` and `ProducesJsonApiError` are
  `RouteHandlerBuilder` extensions. [`JsonApiPoc.Api`](JsonApiPoc.Api) is the worked example.
- **FastEndpoints.** The same extensions, called through `Options()` inside an endpoint's
  `Configure()`. Verified against FastEndpoints 8.2.0.
- **Swagger UI and Scalar.** Both are readers over the generated document, and the sample serves
  both from the one `/openapi/v1.json`. Neither needs anything from this library: they render
  OpenAPI, and the document is already correct by the time it reaches them.

One constraint runs under all three. The schemas are produced by an `IOpenApiOperationTransformer`,
which only ASP.NET Core's built-in generator runs — so the document has to come from
`AddOpenApi()`. Swashbuckle and Scalar are fine as *readers* of that document; what does not work
is a different *generator*. That is the next item.

## Done

**`links`, `meta` and `included` in document schemas.** Response schemas now describe all three: a
declared `TMeta` is walked into named, typed members; the link members are described per document
kind, with pagination only where the primary data is a collection; and `included` is constrained to
the resource types the document declares. Request schemas are unchanged. Reported as
[#8](https://github.com/radekwojpl2/Simple.JsonApi/issues/8).

`describedby` is deliberately not described. The specification defines it for a document, but `Links`
has no such member, so no endpoint built on this library can send one — and describing a member that
can never appear is a claim nothing would falsify, since every link member is optional.

**A typed `included`.** Declaring the types a document may sideload — one member per type, as
`IRelationships` already does for relationships — made the sideload member reachable by member
instead of by cast, and the declaration doubles as the input the OpenAPI package reads to describe
`included`. Reported as [#9](https://github.com/radekwojpl2/Simple.JsonApi/issues/9).

The cost was a breaking change: `Included` changed type on all four resource document forms. It was
narrow — collection-expression literals, indexing, `OfType`, `foreach` and `null` all kept
compiling; only assigning a pre-existing collection *variable* broke, and every break was a compile
error with a one-token fix.

## Next

**Schemas under Swashbuckle and NSwag.** An app whose document comes from Swashbuckle's
`SwaggerGen` or from NSwag gets the operations but not the bodies, because neither runs the
transformer — which is also what stands between the library and `FastEndpoints.Swagger`. The likely
shape is a small package per generator, hooking in after the generator has built its own schema.
Doing it would additionally bring net8.0 support to the OpenAPI package, which is otherwise
blocked. Analysis and evidence in
[#7](https://github.com/radekwojpl2/Simple.JsonApi/issues/7).

**Query parameter helpers.** `include`, `fields`, `filter`, `sort` and `page` are spec-defined and
entirely absent — no parsing, no building. Pagination surfaces only as `Links` the caller populates.

Parsing them into typed values belongs in the core package: it is string handling, with no HTTP in
it. Binding those values as a handler parameter does not, because it needs ASP.NET Core — so it
follows the same split as the OpenAPI package, and it has to cover **minimal APIs and
FastEndpoints** equally, as the annotations already do. Neither should be the one that gets the
worked example while the other gets a paragraph. The OpenAPI package would then describe the
parameters an endpoint binds, so they appear in the document instead of being undocumented strings.

*Applying* the parsed values to a data source stays out of scope (see below).

## Later

**`lid`.** The clearest conformance gap against the base specification. JSON:API 1.1 lets a
resource being created carry a client-chosen `lid` in place of an `id`, identifying it "locally
within the document", and a resource identifier for a resource that does not exist yet "MUST"
carry that `lid` rather than an `id`. `Resource` and `ResourceIdentifier` model `id` only, so
neither can be expressed. One operation names the resource, a later one points at it:

```json
{ "atomic:operations": [
  { "op": "add", "data": { "type": "companies", "lid": "co-1",
                           "attributes": { "name": "Acme" } } },

  { "op": "add", "data": { "type": "contacts",
      "relationships": {
        "company": { "data": { "type": "companies", "lid": "co-1" } } } } }
]}
```

Not urgent, because a `lid` reaches only as far as the document it appears in and the base
specification gives a create request nowhere to put the resource being referred to: the request
"MUST include a single resource object as primary data", and `included` is a response member. The
forward reference above therefore only pays off under atomic operations, below. Modelling it is
worth doing on conformance grounds regardless — the types should be able to say what the
specification says.

The cost is a breaking change: `ResourceIdentifier` is a positional record over `Type` and `Id`,
while the specification makes it *type plus exactly one of id or lid* — a constraint the type
system will not carry, so it lands in the converters.

**Extension and profile negotiation.** `ext` and `profile` round-trip inside the `jsonapi` object,
but nothing negotiates them or enforces their rules.

## Maybe

The cost is understood and the demand is not. Nothing here is refused — an issue describing a use
case that runs into one is the thing most likely to move it up.

**Atomic operations.** The extension is unimplemented. `JsonApiObject.Ext` can carry the URI, but
nothing models the operations document. It is an extension rather than base specification, so a
library that models the wire faithfully can decline it indefinitely without being wrong — and it is
a document format of its own, not a variation on the resource documents, so it is the largest piece
of work on this page. Batching updates and deletes over existing resources would stand on its own;
creating several linked resources in one request is what needs `lid`, above.

## Not planned

These are the boundaries that keep the library small. They are as much a part of the roadmap as the
additions, and a request to cross one is likely to be declined.

- **HTTP.** Content negotiation, status codes, the `Location` header on a 201, and the `406`/`415`
  responses belong to the caller. `JsonApiMediaType.Value` is the only concession. The sample's
  `JsonApi.cs` shows how thin that seam is — it is a file, not a framework.
- **Validation.** Documents are not checked against the specification beyond what parsing requires.
  A relationship carrying none of `data`/`links`/`meta` is rejected and a declared arity mismatch is
  rejected; member requirements otherwise are not enforced.
- **Persistence, ORM or resource-graph integration.** No repositories, no `IQueryable` translation,
  no EF Core. Libraries that map a database to JSON:API endpoints already exist and are a different
  product; this one models the wire and hands you the objects.
- **A server framework.** No controllers, no conventions, no routing. The annotations describe
  endpoints you wrote; they do not generate them.

## Requests

Open an issue. A use case that the wire model cannot express is the most useful thing to report —
more so than a feature name, because the gap is usually narrower than the specification section it
sits in.
