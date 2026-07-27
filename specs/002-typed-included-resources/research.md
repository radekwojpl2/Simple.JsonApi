# Phase 0 Research: Typed Included Resources

**Feature**: `002-typed-included-resources` | **Date**: 2026-07-27

Every mechanical claim below was settled by compiling and running a probe against **net8.0** — the
constraining target framework, since the core library multi-targets net8.0 and net10.0 and a change
that works on only one is a failing change. The probe lives at
`scratchpad/probe/` (Program.cs, Break.cs, Remedy.cs); its full output is quoted in each decision.

---

## D1: What "strongly typed" must mean here

**Decision**: The declared shape is a record with one member per sideloadable resource type, and the
document's sideload member is typed as that record. Reaching a sideloaded resource is member access,
not a cast.

**Rationale**: The specification's FR-004 requires the attributes be readable "without a runtime type
test at the point of use". Verified by probe 3a, which is the feature in one line:

```csharp
string? companyName = doc3.Included?.Companies?[0].Attributes?.Name;
```

```
PASS  3a typed access with no cast
```

This also mirrors the pattern the library already uses for relationships — `IRelationships` is a
marker on a record of typed members, sitting over a name-keyed wire object. The sideload declaration
is the same idea over a flat wire array, so it introduces no new concept for an author to learn.

**Alternatives considered**:

- *Convenience helpers over the flat list* (`Included.OfType<…>()`). Rejected: it still requires the
  caller to name the concrete resource type at every read site, which is the cast in another spelling.
  Fails FR-004.
- *A declaration that names types without typing the member*. Rejected: it would satisfy the OpenAPI
  description work but leaves every read site unchanged. Fails FR-004.

Both are listed in the originating issue and both are excluded by the requirement rather than by
preference.

---

## D2: How the wire type name maps to a declared member

**Decision**: Build the map by reflecting over the declared record's members once per closed type and
caching it. Each member of shape `IReadOnlyList<Resource<TAttributes, …>>` contributes one entry,
keyed by `TAttributes.ResourceType`.

**Rationale**: This satisfies FR-002 ("MUST reuse the existing means by which a resource type's name
and shape are already declared") without inventing a second place to write the name. The mechanism
already exists — `IResourceType` declares the name as a static abstract, at
`libs/JsonApiLite/Resources/IResourceType.cs:9-13`:

> "Implemented by an attributes record to declare, in one place, the JSON:API resource type name its
> resource serializes as. […] the string exists exactly once and a typo is a compile error."

Verified by probe 5, which builds the map from `ContactIncluded` alone:

```
PASS  5a reflection finds 2 declared resource types
PASS  5b map resolves 'companies'
PASS  5c map resolves 'tags'
```

**Consequence worth noting**: the declaration is self-sufficient. A declared document does **not**
need a `ResourceTypeRegistry` to deserialize its sideloaded resources concretely, because its own
members say which types to expect. The registry remains what it is today for undeclared documents.

**Alternatives considered**:

- *Require the author to write the dispatch by hand* via a static abstract factory on the marker
  interface. Rejected: it pushes a mechanical loop onto every author and is a place for the mapping
  to drift from the members.
- *A source generator*. Rejected as disproportionate, and it would be the first in the repository.
  Reflection cached per closed generic type is the smaller change; if it ever shows up in a profile,
  a generator can replace it without changing the public surface.

---

## D3: Preserving the existing generic arities

**Decision**: The full form becomes arity 4 — `ResourceDocument<TAttributes, TRelationships, TMeta,
TIncluded>` — and arities 1, 2 and 3 keep their **current** meanings via the subtype-default trick
the codebase already uses at `libs/JsonApiLite/Documents/ResourceDocument.cs:54-57`.

**Rationale**: The obvious alternative — inserting `TIncluded` before `TMeta` — would silently
reinterpret every existing arity-3 usage. Appending it instead means an existing
`ResourceDocument<ContactAttributes, ContactRelationships, PageMeta>` continues to mean exactly what
it means today. Verified by probe 2a:

```
PASS  2a arity-3 third parameter is still TMeta
```

**Known limitation, accepted**: declaring a sideload shape *without* also naming a meta shape
requires spelling the default meta explicitly —
`ResourceDocument<ContactAttributes, ContactRelationships, Meta, ContactIncluded>`. C# has no default
type arguments, and an arity-3 overload meaning "attributes, relationships, included" would collide
with the existing arity-3 form. This is the cost of not reinterpreting existing code, and it is the
cheaper of the two.

**Second known limitation**: the dictionary-relationships flavour `ResourceDocument<TAttributes>`
cannot gain a typed sideload member, because the arity-2 slot it would need is already
`ResourceDocument<TAttributes, TRelationships>`. That flavour keeps the untyped form. This is
defensible — it is explicitly the escape hatch for authors who do not know their relationship names
at compile time, and such an author is unlikely to know their sideloadable types either — but it is
a real gap against FR-001 read strictly, and is recorded in the plan's Complexity Tracking.

---

## D4: Keeping the undeclared path working — the key finding

**Decision**: The default type argument is `AnyIncluded`, a built-in implementation that **also
implements `IReadOnlyList<Resource>`** and carries `[CollectionBuilder]`.

**Rationale**: This is what turns a wide breaking change into a narrow one. Because `AnyIncluded` is
itself an `IReadOnlyList<Resource>`, the existing read and write patterns keep compiling untouched.
Verified by probe 1:

```
PASS  1a collection expression `Included = [a, b]` compiles
PASS  1b indexer  Included![0]
PASS  1c OfType<>()
PASS  1d assignable to IReadOnlyList<Resource>
PASS  1e foreach enumeration
```

Probe 1a matters most: `Included = [Company("7", "Acme"), Company("8", "Globex")]` is the dominant
form in the existing tests (`libs/tests/JsonApiLite.Tests/Documents/CompoundDocumentTests.cs:138`),
and `[CollectionBuilder]` — available on net8.0, as the probe's target framework proves — keeps it
compiling with no edit at all.

**Alternatives considered**:

- *A plain wrapper exposing `.Resources`*. Rejected: it would force `doc.Included` to become
  `doc.Included?.Resources` at every existing read site, breaking SC-004's "zero edits to the code
  that reads or assembles it".
- *A `NoIncluded` marker holding nothing*. Rejected outright: it would leave authors who cannot
  enumerate their sideloadable types with nowhere to put resources, violating FR-017.

---

## D5: The exact break surface

**Decision**: Accept three breaking assignment forms, all of which fail loudly at compile time and
all of which have a one-token mechanical fix.

**Rationale**: FR-022 requires the migration be mechanical, so the break had to be characterised
rather than estimated. Compiling each form in isolation gives the complete list. What **survives**
(from probe 1, above): collection-expression literals, `null`, indexing, `OfType`, `foreach`, and
assignment of `Included` *to* an `IReadOnlyList<Resource>`.

What **breaks** — assigning a pre-existing collection *variable*:

| Case | Form | Compiler error |
| --- | --- | --- |
| C | `IReadOnlyList<Resource>` variable | `error CS0266: Cannot implicitly convert type 'IReadOnlyList<Resource>' to 'AnyIncluded'` |
| D | `Resource[]` variable | `error CS0029: Cannot implicitly convert type 'Resource[]' to 'AnyIncluded'` |
| E | LINQ result (`List<Resource<…>>`) | `error CS0029: Cannot implicitly convert type 'List<Resource<CompanyAttributes, CompanyRelationships>>' to 'AnyIncluded'` |

Every one is a compile error, never a silent behaviour change — which is the property that makes the
break safe to ship. The remedy was verified rather than assumed, by compiling both forms:

```
remedy spread(IReadOnlyList) count = 1
remedy spread(array)         count = 1
remedy spread(LINQ List)     count = 1
remedy ctor                  count = 1
```

So the migration note is two lines: `Included = x` becomes `Included = [.. x]`, or
`Included = new AnyIncluded(x)` where the spread is unwanted. This satisfies FR-022 and is the
evidence behind SC-004.

---

## D6: Where undeclared sideloaded resources live

**Decision**: `IIncluded` carries an `Undeclared` member — `IReadOnlyList<Resource>` — which every
implementation must expose, including declared ones.

**Rationale**: This is forced by the two clarification answers taken together. Because a declared
document exposes no untyped view (FR-017), a sideloaded resource whose type no member names would
otherwise be unreachable, and Story 3 exists precisely to stop it being silently dropped. Verified
by probes 3b and 4a:

```
PASS  3b Undeclared present on declared doc
PASS  4a declared Included is NOT IReadOnlyList<Resource> (see CS8121 note)
```

Probe 4a is worth reading carefully. The first attempt wrote the check as a runtime pattern test and
the compiler rejected it outright:

```
error CS8121: An expression of type 'ContactIncluded' cannot be handled by a pattern of
type 'IReadOnlyList<Resource>'
```

That failure is a stronger result than the passing test that replaced it: FR-017 is enforced by the
compiler, not merely by convention. An author cannot accidentally read a declared document's sideload
member the untyped way, because the code will not compile.

---

## D7: Round-tripping and wire fidelity

**Decision**: The converter flattens every declared member plus `Undeclared` into one array on write,
and buckets on read by peeking each element's `type`.

**Rationale**: The wire shape is fixed by the specification. Checked https://jsonapi.org/format/
(*Compound Documents*):

> "In a compound document, all included resources **MUST** be represented as an array of resource
> objects in a top-level `included` member."

And (*Document Structure*):

> "`included`: an array of resource objects that are related to the primary data and/or each other
> ("included resources")."

So heterogeneity is required, not incidental, and FR-010's "serialize identically" is the constraint
that keeps this a compile-time change only.

**Open implementation question, not a blocker**: member ordering on write. The specification does not
require any order within `included`, so declaration order is a free choice — but the existing tests
compare serialized output, so whichever order is chosen must be deterministic. `notSure` — whether
any existing test asserts a specific ordering of `included` elements; `dotnet test` after the change
will settle it, and `CompoundDocumentTests.cs:138` is the first place to look.

---

## Summary of decisions

| # | Decision | Verified by |
| --- | --- | --- |
| D1 | Declared record of typed members; member access, no cast | probe 3a |
| D2 | Reflect the type→member map from `IResourceType`, cache per closed type | probe 5a–5c |
| D3 | Append `TIncluded` as arity 4; arities 1–3 keep current meanings | probe 2a |
| D4 | `AnyIncluded` implements `IReadOnlyList<Resource>` + `[CollectionBuilder]` | probe 1a–1e |
| D5 | Break is 3 assignment forms, all compile errors, fix is `[.. x]` | Break.cs / Remedy.cs |
| D6 | `Undeclared` on `IIncluded`; FR-017 enforced by the compiler | probe 3b, 4a, CS8121 |
| D7 | Converter flattens on write, buckets on read | jsonapi.org/format |

No `NEEDS CLARIFICATION` items remain from the Technical Context.
