# Phase 0 Research: OpenAPI Envelope Schemas

**Feature**: `003-openapi-envelope-schemas` | **Date**: 2026-07-29

Every claim below is either cited to `file:line` in this repository, quoted from
https://jsonapi.org/format/, or shown as command output. Anything unchecked carries `notSure`.

---

## R1 — How the description generator should accept a document that declares its sideload shape

**Decision**: Resolve a document type by walking its base-type chain to the four-argument generic
base, and read all four type arguments from that base. Do not extend the arity lists.

**Rationale**: The document families are an inheritance chain, not independent arities. Verified at
`libs/JsonApiLite/Documents/ResourceDocument.cs:63-75`:

```csharp
public record ResourceDocument<TAttributes, TRelationships, TMeta>
    : ResourceDocument<TAttributes, TRelationships, TMeta, AnyIncluded>
...
public sealed record ResourceDocument<TAttributes, TRelationships>
    : ResourceDocument<TAttributes, TRelationships, Meta>
```

`ResourceCollectionDocument` is built identically
(`libs/JsonApiLite/Documents/ResourceCollectionDocument.cs:48-59`). So the four-argument form is the
root of both chains, and every convenience form reaches it by inheritance with its defaults already
substituted — `ResourceDocument<A,R>` resolves to `(A, R, Meta, AnyIncluded)` with no special
casing. One code path covers all three arities present today and any convenience subtype added
later, which the current design cannot do: `libs/JsonApiLite.OpenApi/JsonApiBody.cs:48-60` names
three closed arities per family, so `002` shipping a fourth broke it.

**Alternatives considered**:

- *Add `ResourceDocument<,,,>` and `ResourceCollectionDocument<,,,>` to the existing sets.* Smallest
  possible diff, and it would fix the crash. Rejected because it repeats the mistake: the next
  convenience subtype breaks it again, and the set must then be kept in sync with a type hierarchy
  it does not model.
- *Match by type name.* Rejected for the reason already recorded in the code
  (`JsonApiBody.cs:46-47`): "Matched against the open generic types themselves rather than by type
  name, so a rename is a compile error here instead of a document that silently loses its schemas."

**Constraint retained**: FR-003 requires an unsupported type still fail loudly. The chain walk must
terminate in the existing `throw new ArgumentException` (`JsonApiBody.cs:98-101`) when no
four-argument base is found — not fall through to an empty schema.

---

## R2 — How to describe a declared metadata shape, and when not to

**Decision**: Read the third type argument. If it is `Meta` or derives from `Meta`, describe an
unconstrained object. Otherwise run the existing `Schema()` walker over it.

**Rationale**: The derivation test is load-bearing, not defensive. `Meta` carries its wire form in a
single `JsonObject` behind a converter — `libs/JsonApiLite/Documents/Meta.cs:13-18`:

```csharp
[JsonConverter(typeof(MetaConverter))]
public record Meta : IMeta
{
    public JsonObject Members { get; init; } = [];
```

Walking that type describes a member named `members`, which is not on the wire. The same trap
applies to `Meta<TMeta>` (`Meta.cs:36-46`), which satisfies the `TMeta : class, IMeta` constraint
and would be described as `{ members, value }` — both wrong. A shape that does *not* derive from
`Meta` is a plain record with no converter, so reflecting it describes exactly what is serialized.
The sample declares `public sealed record PageMeta(int Total, int PageCount) : IMeta`
(`JsonApiPoc.Api/Contracts.cs:45`) — no derivation, walked; and
`ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>`
(`JsonApiPoc.Api/Program.cs:127`) — `TMeta` is `Meta`, unconstrained. Both FR-004 and FR-007 fall
out of the one rule.

The walker needs no new capability: `Schema(Type, HashSet<Type>)`
(`libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs:168-218`) already handles scalars, enums,
dictionaries, sequences and nested objects, and already breaks self-reference with its `seen` set —
which is FR-006 in full.

**Alternatives considered**:

- *Describe `Meta` as absent rather than unconstrained.* Rejected per the spec's own framing: "The
  spec reserves no member names — 'any members MAY be specified'" (`Meta.cs:7-8`). Omitting the
  member would tell a caller it does not exist when it may.
- *Test `TMeta == typeof(Meta)` by equality.* Rejected: it misses `Meta<TMeta>`, which is the exact
  case that would emit a wrong description rather than a merely unhelpful one.

---

## R3 — Which link members can actually be described

**Decision**: Describe the members `Links` actually carries, per document kind. **`describedby`
cannot be described, because the library cannot send it.** Ratified in the spec on 2026-07-29 —
FR-008 revised, FR-008a added, FR-021 revised. No longer an open question.

**Rationale**: `libs/JsonApiLite/Documents/Links.cs:7-16` is the complete set:

```csharp
public sealed record Links
{
    public Link? Self { get; init; }
    public Link? Related { get; init; }
    public Link? About { get; init; }
    public Link? First { get; init; }
    public Link? Prev { get; init; }
    public Link? Next { get; init; }
    public Link? Last { get; init; }
}
```

There is no `DescribedBy` member. Describing `describedby` would describe a member no endpoint built
on this library can produce, which FR-019 forbids in spirit and SC-005 ("every response validates
against the description") cannot detect, since an absent optional member never fails validation. The
requirement was inherited from the superseded `001`, written without checking `Links`. The spec now
states the rule generally at **FR-008a**: "The description MUST NOT describe a link member the
library cannot produce."

`about` is present and is the error-object link — the spec places it on an error object rather than
the document. The library uses one `Links` record everywhere, so `about` is reachable on a document
too; it is not described at document level because no sample endpoint sends it there. `notSure` —
whether any consumer relies on document-level `about`; nothing in this repository sets it, checked
by searching for `About` across `JsonApiPoc.Api` and `libs`, which returns only the declaration and
its XML doc.

**The per-kind sets, as they will be implemented**:

| Document kind | Link members described |
| --- | --- |
| Single resource document | `self` |
| Resource collection document | `self`, `first`, `prev`, `next`, `last` |
| To-one linkage document | `self`, `related` |
| To-many linkage document | `self`, `related`, `first`, `prev`, `next`, `last` |
| Error document | `self` |

Pagination is confined to collection-primary-data kinds per the spec: "Pagination links **MUST**
appear in the links object that corresponds to a collection." `related` is confined to linkage per
the spec: "**related**: a related resource link when primary data represents a relationship."

**Each member's schema**: `anyOf` of a string with `format: uri` and an object `{ href, meta }`,
matching `libs/JsonApiLite/Documents/Link.cs:9-11` (a `record Link(string Href)` with `Meta? Meta`,
behind `LinkConverter`) and the spec: a link is "a string whose value is a URI-reference pointing to
the link's target, a link object or `null` if the link does not exist." Written by hand, not
reflected, for the reason already recorded at `JsonApiSchemaBuilder.cs:135-136`.

**The spec has been amended** (2026-07-29). Options were: add `DescribedBy` to core `Links` (rejected
— FR-023 confines core changes to sideload legibility, and this would change the wire model for a
member nothing asked for), or describe only what can be sent (chosen). Recorded in the spec's
Clarifications rather than left as a planning note.

---

## R4 — How the description generator reads the declared sideloadable types

**Decision**: Add a small public accessor to the core package that reports, for a declared sideload
shape, the resource type names it claims and the element type each claims them into. The OpenAPI
package consumes that and unwraps `Resource<,>` to reach the attributes and relationships types it
already knows how to describe.

**Rationale**: The map already exists and is authoritative, but is internal —
`libs/JsonApiLite/Serialization/IncludedShape.cs:30` is `internal sealed class IncludedShape`, and
`IncludedMember` at `:9` likewise. `002` FR-019 already committed to this being readable: "The
declaration MUST be readable by other tooling in this project, so that the API description work
tracked separately can report the declared types without a second declaration being invented for
it." Exposing an accessor over the existing cache means one implementation of "what does this
declaration name", which is what FR-014 asks for. It adds no package reference, so Principle III
holds.

The accessor must expose only what the description needs — the wire type name and the element type.
It must not expose `IncludedMember.Property` or `ListType`, which are serialization mechanics.

**Alternatives considered**:

- *Make `IncludedShape` and `IncludedMember` public as they stand.* Rejected: it publishes
  `PropertyInfo` and `ListType`, committing the package to serialization internals it would then
  have to keep stable.
- *`InternalsVisibleTo("JsonApiLite.OpenApi")`.* Zero public surface and no drift, and tempting.
  Rejected because the two packages are published separately: a consumer pairing mismatched versions
  gets a runtime `MethodAccessException` instead of a compile error, and the friend assembly makes
  every core internal a de-facto contract.
- *Re-reflect independently in the OpenAPI package.* Rejected: it would duplicate the member-
  selection and naming rules at `IncludedShape.cs:49-61`, and the two copies would drift. It also
  reads against FR-014.

**What the OpenAPI side then does**: for each declared entry, unwrap the element type
(`Resource<TAttributes, TRelationships>`, `libs/JsonApiLite/Resources/Resource.cs:123`) to its two
type arguments and reuse the existing `Resource(...)` builder path
(`JsonApiSchemaBuilder.cs:35-66`). The described member is one array whose `items` is an `anyOf`
over the declared resource schemas — one flat list, per FR-012 and the spec: "In a compound
document, all included resources **MUST** be represented as an array of resource objects in a
top-level `included` member."

**Undeclared case**: `TIncluded` resolves to `AnyIncluded`, which declares no members
(`libs/JsonApiLite/Resources/AnyIncluded.cs:7-8`: "This is the default `TIncluded` on every resource
document form, so it is what `Included` means when an author declares nothing"). An empty declared
set is described as an array of unconstrained resource objects, satisfying FR-013 and the "declares
no types at all" edge case with the same code path.

---

## R5 — Where the evidence for each story comes from

**Decision**: Add a test project `libs/tests/JsonApiLite.OpenApi.Tests` (net10.0 only) that asserts
against the emitted schema JSON, and keep the sample's `/openapi/v1.json` as the end-to-end gate.

**Rationale**: There is no test coverage for the OpenAPI package at all today. Verified — the only
test project references only the core library:

```
libs/tests/JsonApiLite.Tests/JsonApiLite.Tests.csproj:
  <ProjectReference Include="..\..\JsonApiLite\JsonApiLite.csproj" />
```

and no test source mentions OpenApi (`grep -rl "OpenApi" libs/tests/ --include=*.cs` returns
nothing). Every requirement in this feature is a statement about emitted schema content, so without
such a project the only evidence available is reading the sample's published document by hand — which
the constitution forbids relying on alone ("A schema MUST NOT be called correct because it
compiles") and which cannot cover the document kinds the sample does not expose.

net10.0 only, matching the package under test, for the reason recorded in
`libs/JsonApiLite.OpenApi/JsonApiLite.OpenApi.csproj`: "There is nothing to multi-target back to
net8.0." This is the first test project in the repository that does not multi-target; the constitution's
"Tests run against both net8.0 and net10.0" applies to the core library's tests and cannot apply
here.

**The sample gap must be stated, not glossed.** `JsonApiPoc.Api/JsonApiPoc.Api.csproj:13-14`
references published packages (`Simple.JsonApi` and `Simple.JsonApi.OpenApi`, both
`1.1.1-preview.10.44`), so local changes are invisible to it until published. Verifying SC-001 to
SC-005 against the sample therefore requires temporarily repointing it at the local projects,
recording the output, and reverting. The constitution requires exactly this disclosure: "It consumes
the **published** packages, so a local change is not reflected there until published — that gap MUST
be stated rather than glossed over when reporting results."

**Alternatives considered**:

- *Verify only through the sample.* Rejected: cannot reach linkage or error document kinds
  independently, and requires a publish cycle per iteration.
- *Add OpenAPI tests to the existing `JsonApiLite.Tests` project.* Rejected: that project targets
  net8.0 as well, and the OpenAPI package cannot be referenced from a net8.0 target.

---

## Open items carried into the plan

Both blocking items were resolved on 2026-07-29 and are recorded here for provenance.

1. ~~**FR-008 and FR-021 name `describedby`, which the library cannot send**~~ (R3). **Resolved**:
   the spec drops it. FR-008 revised, FR-008a added stating the general rule, FR-021 revised; the
   reasoning and the rejected alternative are in the spec's Clarifications, session 2026-07-29.
2. ~~**`001-document-envelope-schemas` is still live** and its FR-016 forbids the core change R4
   requires.~~ **Resolved**: `001` is marked superseded by `003`, with the contradiction and the
   reason for retaining rather than deleting it recorded in its header.
3. **The working tree carries unfinished `002` migration.** Still open.
   `JsonApiPoc.Api/Contracts.cs:59` still
   declares the `Undeclared` member that commit `ddbb34b` removed from the core, and
   `JsonApiLite.sln` has been modified to include `JsonApiPoc.Api`, which contradicts both
   `CLAUDE.md` ("`JsonApiPoc.Api` is **not** in `JsonApiLite.sln`") and the constitution's
   verification gates. Neither is this feature's work, but both affect whether the sample can serve
   as evidence.
