# Phase 1 Contract: Public API Surface

**Feature**: `003-openapi-envelope-schemas` | **Date**: 2026-07-29

What consumers of the published packages can see. Everything else in this feature is internal to
`Simple.JsonApi.OpenApi` and changes no compiled-against surface.

---

## 1. `Simple.JsonApi` (core) — one addition

The only core change FR-023 permits: making the existing sideload declaration legible to other
tooling, as `002` FR-019 already committed to.

**Added** — a public accessor reporting what a declared sideload shape names. Two facts per declared
member: the wire resource type name, and the concrete element type holding it.

**Contract**:

| Guarantee | Detail |
| --- | --- |
| Additive | No existing member changes signature or behaviour. Not a breaking change. |
| No new dependency | Reflection over types already reachable; Principle III holds, core keeps zero package references. |
| Both TFMs | Available on net8.0 and net10.0, like the rest of the core package. |
| Single source | Reads the same cached `IncludedShape` the converter uses (`libs/JsonApiLite/Serialization/IncludedShape.cs:30-37`); no second reflection pass, no second set of rules. |
| Stable order | Declaration order, fixed by metadata token as `IncludedShape.cs:43-52` already does. |
| Empty is legal | A shape declaring no members, and `AnyIncluded`, both yield an empty sequence. |

**Not exposed**: `IncludedShape` and `IncludedMember` themselves, and in particular
`IncludedMember.Property` and `IncludedMember.ListType` (`IncludedShape.cs:19-22`) — serialization
mechanics that would become a contract the moment they were published.

**XML docs required** (Principle V): the accessor is public API and must say what it is *for* —
reporting a document's declared sideloadable types to tooling that describes the document — not
restate its name.

---

## 2. `Simple.JsonApi.OpenApi` — no public surface change

`ProducesJsonApi<TDocument>` and `AcceptsJsonApi<TDocument>` keep their signatures. What changes is
which `TDocument` they accept and what they emit.

### Accepted document types — widened

| `TDocument` | Today | After |
| --- | --- | --- |
| `ResourceDocument<A>` | accepted | accepted |
| `ResourceDocument<A,R>` | accepted | accepted |
| `ResourceDocument<A,R,M>` | accepted | accepted |
| `ResourceDocument<A,R,M,I>` | **throws** | accepted |
| the four `ResourceCollectionDocument` forms | same as above | same as above |
| `ToOneLinkageDocument`, `ToManyLinkageDocument`, `ErrorDocument` | accepted | accepted |
| anything else | throws | **throws, unchanged** |

The failure for a genuinely unsupported type keeps its current message, naming the offending type
and the accepted forms (`libs/JsonApiLite.OpenApi/JsonApiBody.cs:98-101`) — FR-003. Widening is
achieved by resolving through the inheritance chain (R1), not by relaxing the check.

**This is the crash fix.** Verified before the change:

```
Unhandled exception. System.ArgumentException: 'JsonApiLite.ResourceDocument`4[...]' is not a
JSON:API document the annotation understands — expected ResourceDocument<>, ...
   at JsonApiLite.OpenApi.JsonApiBody.Describe(Type documentType)
   at Program.<Main>$(String[] args) in D:\git\JsonApiPoc\JsonApiPoc.Api\Program.cs:line 96
```

### Emitted response schema — extended

Request body schemas are unchanged in every respect (FR-020).

```jsonc
// GET /contacts — ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>
{
  "type": "object",
  "required": ["data"],                    // unchanged: no envelope member is ever required
  "properties": {
    "data": { "type": "array", "items": { /* unchanged */ } },

    "meta": {                              // new — walked from PageMeta (FR-004)
      "type": "object",
      "properties": {
        "total":     { "type": "integer" },
        "pageCount": { "type": "integer" }
      }
    },

    "links": {                             // new — collection kind, so pagination included (FR-008)
      "type": "object",
      "properties": {
        "self":  { "anyOf": [ { "type": "string", "format": "uri" }, { "$comment": "link object" } ] },
        "first": { "anyOf": [ /* … */ ] },
        "prev":  { "anyOf": [ /* … */ ] },
        "next":  { "anyOf": [ /* … */ ] },
        "last":  { "anyOf": [ /* … */ ] }
      }
    }
  }
}
```

```jsonc
// GET /contacts/{id} — ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>
{
  "type": "object",
  "required": ["data"],
  "properties": {
    "data": { /* unchanged */ },

    "meta": { "type": "object" },          // TMeta is Meta — unconstrained, never invented (FR-007)

    "links": {                             // single-resource kind: no pagination
      "type": "object",
      "properties": { "self": { "anyOf": [ /* … */ ] } }
    },

    "included": {                          // new — from ContactIncluded's declaration (FR-011)
      "type": "array",
      "items": {
        "anyOf": [
          { /* resource object, type const "companies" */ },
          { /* resource object, type const "tags"      */ }
        ]
      }
    }
  }
}
```

One flat array, whatever the declaration — FR-012, and the spec: "In a compound document, all
included resources **MUST** be represented as an array of resource objects in a top-level `included`
member."

### Link member sets, by document kind

| Document kind | `self` | `related` | `first`/`prev`/`next`/`last` |
| --- | --- | --- | --- |
| Single resource | ✓ | | |
| Resource collection | ✓ | | ✓ |
| To-one linkage | ✓ | ✓ | |
| To-many linkage | ✓ | ✓ | ✓ |
| Error | ✓ | | |

**`describedby` is absent from every row, deliberately.** `libs/JsonApiLite/Documents/Links.cs:7-16`
has no such member, so no endpoint built on this library can send one, and FR-008a forbids
describing a link member the library cannot produce. Ratified in the spec's Clarifications, session
2026-07-29; see research.md R3 for the rejected alternative.

### Compatibility

| Consumer | Effect |
| --- | --- |
| Annotates `ResourceDocument<A,R>` and reads `data` | None. `data` is byte-identical; new members sit alongside it. |
| Annotates a document declaring `TIncluded` | Application starts instead of throwing. |
| Generates a client from the document | Gains `meta`, `links`, `included` members. Additive; no member is removed or retyped. |
| Validates responses against the description | Still passes. Nothing new is `required`, and `additionalProperties` is still never set, so undescribed members remain permitted (FR-016). |

---

## 3. Not in this contract

- Any change to what an endpoint sends. Serialization is untouched (FR-019); the existing wire tests
  must pass unmodified (SC-006).
- The `jsonapi` version member — out of scope, per the originating issue.
- Query parameters as inputs — tracked separately on the roadmap.
- Request body envelopes — explicitly excluded by FR-020.
