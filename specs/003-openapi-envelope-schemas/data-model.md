# Phase 1 Data Model: OpenAPI Envelope Schemas

**Feature**: `003-openapi-envelope-schemas` | **Date**: 2026-07-29

This feature adds no wire-format types. The "data model" here is the description generator's
internal model of a document, plus the one public accessor the core package gains so that model can
be populated. Nothing below changes what any endpoint sends (FR-019).

---

## Entities

### `JsonApiBody.Description` — extended

`libs/JsonApiLite.OpenApi/JsonApiBody.cs:118-123`. What is read off a document type before the
request/response split. Gains two members.

| Member | Status | Type | Meaning |
| --- | --- | --- | --- |
| `Shape` | existing | `JsonApiShape` | Resource, Linkage or Errors |
| `Collection` | existing | `bool` | Whether primary data is an array |
| `ResourceType` | existing | `string?` | The wire type name of the primary resource |
| `Attributes` | existing | `Type?` | The primary resource's attributes type |
| `Relationships` | existing | `Type?` | The primary resource's relationships type |
| **`Meta`** | **new** | `Type?` | The document's declared metadata shape, or `null` when it is `Meta` or derives from it |
| **`Included`** | **new** | `Type?` | The document's declared sideload shape, or `null` when it is `AnyIncluded` |

**Population rule (R1)**: resolve the document type by walking its base-type chain to the
four-argument generic base of either family, then read arguments 0–3. A type with no such base and
no non-generic match keeps today's `ArgumentException` (FR-003).

**`Meta` normalisation rule (R2)**: `null` when the third argument is `Meta` or derives from `Meta`
— that type's wire form is owned by `MetaConverter`, so reflecting it would describe members that
are not sent. `null` means "describe an unconstrained object", never "omit the member" (FR-007).

**`Included` normalisation rule (R4)**: `null` when the fourth argument is `AnyIncluded`, which
declares nothing. `null` means "describe an array of unconstrained resource objects" (FR-013).

**Validation**: `Meta` and `Included` are only ever populated for `JsonApiShape.Resource`. Linkage
and error documents are non-generic, so they cannot carry either — which is FR-022 by construction
rather than by a check.

---

### `IncludedDeclaration` — new public accessor on the core package

The one core change FR-023 permits. It publishes what `IncludedShape`
(`libs/JsonApiLite/Serialization/IncludedShape.cs:30`, `internal`) already computes and caches,
without publishing how it computes it.

| Exposed | Type | Meaning |
| --- | --- | --- |
| Resource type name | `string` | The wire `type` this member claims, from the element's `IResourceType.ResourceType` |
| Element type | `Type` | The concrete resource type the member holds, e.g. `Resource<CompanyAttributes, CompanyRelationships>` |

**Deliberately not exposed**: `IncludedMember.Property` and `IncludedMember.ListType`
(`IncludedShape.cs:19-22`). Those are how the converter fills the member; publishing them would
commit the package to serialization mechanics.

**Ordering**: declaration order, as `IncludedShape` already fixes it by metadata token
(`IncludedShape.cs:43-52`). The description does not depend on order, but a stable order keeps the
emitted document byte-stable between runs, which the tests in R5 compare against.

**Empty case**: a shape declaring no members yields an empty sequence — indistinguishable from
`AnyIncluded`, which is what the "declaration naming no types at all" edge case requires.

---

### Envelope schema fragments — new, internal to the OpenAPI package

Built by `JsonApiSchemaBuilder`. None is a CLR type; each is an `OpenApiSchema` fragment attached to
the document object alongside `data`, which today is the whole envelope
(`JsonApiSchemaBuilder.cs:32`).

| Fragment | Applies to | Shape |
| --- | --- | --- |
| `meta` | resource documents (response only) | walked from the declared shape, or `{ "type": "object" }` |
| `links` | every document kind (response only) | object of link members, per the R3 table |
| `included` | resource documents (response only) | array whose `items` is an `anyOf` over declared resource schemas, or an unconstrained resource object |

**Link member schema**: `anyOf` of `{ "type": "string", "format": "uri" }` and
`{ "type": "object", "properties": { "href": …, "meta": … }, "required": ["href"] }`, per
`libs/JsonApiLite/Documents/Link.cs:9-11` and the spec's definition of a link.

**Required-ness**: no fragment appears in any `required` set (FR-015, FR-010). `data` and `errors`
remain the only required members, unchanged (`JsonApiSchemaBuilder.cs:32`, `:160-162`).

**Request/response split**: fragments are added only when the body is a `JsonApiResponseBody`
(FR-020). The distinction already exists at `JsonApiBody.cs:126-140` and is already available where
schemas are built, so this is a branch, not new plumbing.

---

## State transitions

None. Schema construction is a pure function of the document type and the serializer options; there
is no state to transition. The one cached structure, `IncludedShape`'s per-type cache
(`IncludedShape.cs:32`), is populated once per closed type and never invalidated — unchanged by this
feature.

---

## Relationships between entities

```
document type (e.g. ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>)
  │
  └─ walk base chain to 4-arg base ─────► Description
                                            ├─ Attributes    ──► attributes schema   (existing)
                                            ├─ Relationships ──► relationships schema (existing)
                                            ├─ Meta          ──► meta fragment        (new, R2)
                                            └─ Included ──► IncludedDeclaration ──► included fragment
                                                                (new, R4)     (per declared type)
                                                                    │
                                                                    └─ element type unwrapped to
                                                                       (TAttributes, TRelationships),
                                                                       reusing the resource builder
```

`Shape` and `Collection` together select the link member set (R3 table). They are already computed
and need no change.
