# Phase 1 Data Model: Typed Included Resources

**Feature**: `002-typed-included-resources` | **Date**: 2026-07-27

The "data model" here is the wire model — the types that say what a JSON:API document is. Shapes
below were compiled and exercised by the probe described in [research.md](research.md).

---

## `IIncluded` (new)

The marker constraining a document's `TIncluded`. Empty of behaviour except for one member, which
exists because the spec's two clarification answers together leave undeclared resources with nowhere
else to go (FR-012).

| Member | Type | Purpose |
| --- | --- | --- |
| `Undeclared` | `IReadOnlyList<Resource>` | Sideloaded resources whose `type` no declared member names. Present on **every** implementation, including one that names every type its author expects. |

**Validation rules**

- An implementation's declared members must each be `IReadOnlyList<Resource<TAttributes, …>>`, where
  `TAttributes` implements `IResourceType`. Members not of that shape are ignored when the map is
  built (verified: probe 5a counts exactly the two qualifying members on `ContactIncluded`).
- Two members must not declare the same wire type name. Detected when the map is built, because the
  map is keyed by that name — the shape cache is the natural place to reject it.
- `Undeclared` defaults to empty, never null, so a reader never has to null-check it.

**Why it sits on the interface rather than on each implementation**: a reader holding only
`IIncluded` must still be able to reach resources the declaration did not anticipate. Putting it on
the interface makes that reachability a property of the contract rather than of each author's
diligence.

---

## `AnyIncluded` (new — the default type argument)

The implementation used when an author declares nothing. It is the single most load-bearing piece of
the design, because it is what makes the breaking change narrow.

| Member | Type | Purpose |
| --- | --- | --- |
| `Undeclared` | `IReadOnlyList<Resource>` | Every resource. Nothing is "declared" here, so all of it is undeclared by definition. |
| `this[int]` | `Resource` | From `IReadOnlyList<Resource>`. |
| `Count` | `int` | From `IReadOnlyList<Resource>`. |
| `GetEnumerator()` | `IEnumerator<Resource>` | From `IReadOnlyList<Resource>`. |

**Declared as**: `[CollectionBuilder(typeof(AnyIncludedBuilder), nameof(AnyIncludedBuilder.Create))]`
over `IIncluded, IReadOnlyList<Resource>`.

**Why it implements `IReadOnlyList<Resource>`** — the non-obvious decision, and the one most worth a
comment in the source. Because `AnyIncluded` *is* a list of resources, today's call sites keep
working with no edit: indexing, `OfType`, `foreach`, and assignment of `Included` into an
`IReadOnlyList<Resource>` all continue to compile. Without this, every existing read site would need
`.Resources` appended, breaking SC-004.

**Why `[CollectionBuilder]`**: it keeps `Included = [a, b]` compiling, which is the dominant form in
the existing tests (`CompoundDocumentTests.cs:138`). Confirmed available on net8.0 — the probe
targets net8.0 and probe 1a passes.

---

## `Resource` / `Resource<…>` (unchanged)

No change. Sideloaded resources are ordinary resource objects; a declared member's element type is
just `Resource<TAttributes, TRelationships>`. This is what FR-006 requires — a sideloaded resource
carries everything a primary-data resource carries — and it comes for free by reusing the type.

---

## Document types (changed)

Both families gain a fourth type parameter. Arities 1–3 keep their **current** meanings, so the
third parameter is still `TMeta` (probe 2a).

| Arity | Form | `Included` type | Status |
| --- | --- | --- | --- |
| 1 | `ResourceDocument<TAttributes>` | `AnyIncluded?` | Changed type, same behaviour. Cannot declare — see plan's Complexity Tracking. |
| 2 | `ResourceDocument<TAttributes, TRelationships>` | `AnyIncluded?` | Changed type, same behaviour |
| 3 | `ResourceDocument<TAttributes, TRelationships, TMeta>` | `AnyIncluded?` | Changed type, same behaviour |
| 4 | `ResourceDocument<TAttributes, TRelationships, TMeta, TIncluded>` | `TIncluded?` | **New** |

`ResourceCollectionDocument` mirrors this exactly. `ToOneLinkageDocument`, `ToManyLinkageDocument`
and `ErrorDocument` are untouched — they have no resource primary data, so per FR-020 they must not
gain a sideload member. Checked https://jsonapi.org/format/ (*Document Structure*): "`included`: an
array of resource objects that are related to the primary data"; those documents have no such data.

**Constraint added**: `where TIncluded : class, IIncluded`.

---

## `IncludedShape` (new, internal)

The cached map from wire type name to declared member, built once per closed `TIncluded`.

| Field | Type | Purpose |
| --- | --- | --- |
| *(key)* | `string` | The wire type name, from `TAttributes.ResourceType` |
| *(value)* | member accessor + concrete `Resource<…>` type | Where to put a resource of that type, and what to deserialize it as |

**Why a cache**: reflection per document would be a performance regression on a hot path. Per closed
generic type it runs once for the life of the process. Verified reachable by probe 5, which builds
the map from `ContactIncluded` and resolves both `"companies"` and `"tags"`.

---

## State transitions

A document's sideload member has three observable states, and the spec requires all three be
distinguishable (FR-007, FR-011).

| State | In C# | On the wire |
| --- | --- | --- |
| Absent | `Included is null` | member omitted entirely |
| Present, empty | `Included` non-null, every member empty | `"included": []` |
| Present, populated | `Included` non-null, members populated | `"included": [ … ]` |

The absent → omitted mapping is what FR-011 requires; a document with nothing sideloaded must omit
the member rather than write `[]`.

---

## Read and write paths

**Write** (`IncludedConverter.Write`): enumerate declared members in declaration order, then
`Undeclared`, concatenating into one JSON array. Deterministic ordering matters only because existing
tests compare serialized output — the specification imposes no order within `included`.

**Read** (`IncludedConverter.Read`): for each element, peek `type`; resolve through the cached map; if
resolved, deserialize into the concrete resource type and append to that member; if not, deserialize
as `Resource<JsonObject>` and append to `Undeclared`. This mirrors the dispatch `ResourceConverter`
already performs at `libs/JsonApiLite/Serialization/ResourceConverter.cs:35-41`, with the declaration
supplying the mapping instead of a separately-configured registry.

**Consequence**: a declared document needs no `ResourceTypeRegistry` to deserialize its sideloaded
resources concretely — its own members are the registry. The registry keeps its present role for
undeclared documents.
