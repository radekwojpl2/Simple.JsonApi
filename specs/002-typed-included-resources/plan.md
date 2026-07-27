# Implementation Plan: Typed Included Resources

**Branch**: `002-typed-included-resources` | **Date**: 2026-07-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-typed-included-resources/spec.md`

## Summary

A document's sideload member is the one place the library hands back an untyped value. This feature
makes it declarable: an author states which resource types their document may sideload, and reads
them by member rather than by cast.

The approach, established in [research.md](research.md) and verified by a compiled probe: add a
fourth type parameter `TIncluded` to the resource document forms, constrained by a new `IIncluded`
marker whose implementations are records with one member per sideloadable type. A converter flattens
those members into the single array the specification requires, and buckets them again on read by
peeking each element's `type`. The default type argument, `AnyIncluded`, itself implements
`IReadOnlyList<Resource>`, which is what keeps the breaking change narrow — existing reads, indexing,
`OfType`, `foreach` and collection-expression literals all keep compiling untouched.

## Technical Context

**Language/Version**: C# 12 / .NET — core library multi-targets `net8.0` and `net10.0`
**Primary Dependencies**: None. `libs/JsonApiLite` has zero package references and must keep them
(Constitution III). This feature uses `System.Text.Json` from the BCL only.
**Storage**: N/A
**Testing**: xUnit, `libs/tests/JsonApiLite.Tests`, run against both TFMs
**Target Platform**: Any .NET 8+ host; the core library is deliberately usable without a web framework
**Project Type**: Library (wire model) plus a companion OpenAPI package and a sample API
**Performance Goals**: No regression on serialize/deserialize. The type→member map is reflected once
per closed generic type and cached; no per-document reflection.
**Constraints**: Serialized output must be byte-identical to today (FR-010, FR-015). Must compile and
pass on `net8.0` as well as `net10.0` — `[CollectionBuilder]` and static abstract interface members
were both confirmed available on net8.0 by the probe.
**Scale/Scope**: 4 declaration sites change (`ResourceDocument.cs:19,42`,
`ResourceCollectionDocument.cs:11,31`), plus one new marker interface, one default implementation,
one converter, and the test and sample migrations.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates resolved against `.specify/memory/constitution.md` v1.0.0.

| Principle | Status | Evidence |
| --- | --- | --- |
| **I. The Specification Decides** | **PASS** | The design is shaped by the wire format, not around it. Checked https://jsonapi.org/format/ (*Compound Documents*): "In a compound document, all included resources **MUST** be represented as an array of resource objects in a top-level `included` member." The flattening converter exists to honour that `MUST`; a design storing per-type arrays on the wire would violate it and was never a candidate. |
| **II. Verify or Say You Did Not** | **PASS** | Every mechanical claim in research.md is backed by a compiled probe with its output quoted. The one unverified item carries the `notSure` token (D7, `included` element ordering) and names what would settle it. |
| **III. Core Package Takes No Dependencies** | **PASS** | Entirely inside `libs/JsonApiLite` using `System.Text.Json` only. No package reference added. Both TFMs retained — net8.0 was the probe's target precisely to prove this. |
| **IV. Model the Wire, Nothing Else** | **PASS with a required ROADMAP edit** | This models the wire and nothing else: no HTTP, no validation, no persistence, no server framework. It crosses none of the *Not planned* boundaries. But the Development Workflow section requires a feature proposal to cite ROADMAP.md, and **this feature is not on it** — see Complexity Tracking. |
| **V. House Style** | **PASS (enforced during implementation)** | No ternary returns; the converter's dispatch is written as `if` blocks with early returns, matching `ResourceConverter.cs:27-42`. Every new public member carries XML docs saying what it is *for*. Comments justify decisions — chiefly why `AnyIncluded` implements `IReadOnlyList<Resource>`, which is non-obvious and load-bearing. |

**Verification gates** (constitution, *Build, Test and Verification Gates*) — all three commands must
pass before this is proposed as complete, and `JsonApiPoc.Api` must be built explicitly because it is
not in the solution. It consumes **published** packages, so it will not see this change until a
package is published; per the constitution that gap must be stated rather than glossed over, and the
plan does so in Phase 2.4.

**Post-Phase-1 re-check**: PASS, unchanged. The Phase 1 design added no dependency, no framework
coupling and no new package. The one open item (ROADMAP.md) is recorded below rather than waived.

## Project Structure

### Documentation (this feature)

```text
specs/002-typed-included-resources/
├── plan.md              # This file
├── research.md          # Phase 0 — 7 decisions, each probe-verified
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── public-api.md    # Phase 1 — the public surface this feature adds and changes
├── checklists/
│   └── requirements.md  # From /speckit-specify
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
libs/JsonApiLite/                        # core wire model — zero dependencies, net8.0 + net10.0
├── Documents/
│   ├── ResourceDocument.cs              # CHANGED: TIncluded added, arities 1-3 preserved
│   └── ResourceCollectionDocument.cs    # CHANGED: same
├── Resources/
│   ├── IIncluded.cs                     # NEW: marker + Undeclared member
│   ├── AnyIncluded.cs                   # NEW: default; IReadOnlyList<Resource> + CollectionBuilder
│   └── Resource.cs                      # unchanged
└── Serialization/
    ├── IncludedConverter.cs             # NEW: flatten on write, bucket on read
    ├── IncludedShape.cs                 # NEW: cached type->member map per closed TIncluded
    └── ResourceConverter.cs             # unchanged

libs/tests/JsonApiLite.Tests/
├── Documents/CompoundDocumentTests.cs   # CHANGED: migration + new typed cases
└── Documents/TypedIncludedTests.cs      # NEW: the three user stories

JsonApiPoc.Api/                          # sample — consumes PUBLISHED packages (see Phase 2.4)
```

**Structure Decision**: No new project. The whole feature lands in the existing core library because
it is wire modelling and needs no framework — which is what Constitution III requires. The companion
OpenAPI package is untouched here; it becomes a *consumer* of the declaration later, under
`001-document-envelope-schemas` Story 3.

## Phase 2: Implementation Outline

Ordered so each user story is independently demonstrable, per the spec's priorities.

**2.1 — Foundation (blocks everything).** `IIncluded` with its `Undeclared` member; `AnyIncluded`
implementing `IReadOnlyList<Resource>` with `[CollectionBuilder]`; `IncludedShape` building and
caching the type→member map by reflection.

**2.2 — Story 1, read path (P1).** `IncludedConverter.Read`: peek each element's `type`, resolve
through the cached map, deserialize into the concrete resource type, bucket into the declared member;
anything unresolved goes to `Undeclared`. Add `TIncluded` to the four document declarations.

**2.3 — Story 2, write path (P2).** `IncludedConverter.Write`: concatenate declared members plus
`Undeclared` into one array, deterministic in declaration order. Assert byte-identical output against
the existing fixtures.

**2.4 — Story 3 and migration (P3).** Undeclared round-trip tests. Migrate
`libs/tests/JsonApiLite.Tests` (the three broken assignment forms → `[.. x]`). Write the migration
note required by FR-022. The sample cannot be migrated until a package is published — that gap is
stated, not glossed.

**2.5 — Release.** Record the break in the commit so semantic-release picks it up. Versions are never
set by hand and `CHANGELOG.md` is never edited (Constitution, *Development Workflow*).

## Complexity Tracking

> Filled because the Constitution Check has items requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Not on ROADMAP.md.** The Development Workflow section requires a proposal to cite the roadmap entry it advances. The roadmap's *Next* entry covers `links`, `meta` and `included` in **document schemas** (OpenAPI, i.e. spec 001) — not typing the wire model. | The gap is real and was reported as issue #9. The roadmap is explicitly "the maintainer's current read, not a commitment", and states "A reported use case moves an item faster than anything else here." | Not adding a roadmap entry: rejected because the constitution makes the citation mandatory, so shipping without one is an unremarked violation. **Action: add a *Next* or *Later* entry for this before implementation begins.** |
| **A breaking public API change.** `Included`'s type changes on all four document forms. | Chosen deliberately in the spec's clarification session: one way to express a document, rather than an old family and a new one (FR-021). | The additive alternative was rejected by the spec author with the trade-off stated. Precedent exists — ROADMAP.md already accepts a breaking change for `lid`: "The cost is a breaking change: `ResourceIdentifier` is a positional record over `Type` and `Id`". Mitigated by D4/D5: every break is a compile error with a one-token fix. |
| **The dictionary-relationships flavour cannot declare sideloadable types.** `ResourceDocument<TAttributes>` keeps the untyped form, because the arity slot it needs is taken. | C# has no default type arguments, and an arity-2 overload would collide with `ResourceDocument<TAttributes, TRelationships>`. | Reordering the type parameters would let it work but would silently reinterpret every existing arity-3 usage (research D3). Accepting a gap on the escape-hatch flavour is cheaper than a silent reinterpretation of correct code. Read strictly this is a partial gap against FR-001 and should be confirmed as acceptable. |
| **Reflection in the core library.** `IncludedShape` reflects over `TIncluded`'s members. | Needed to map a wire type name to a declared member without asking the author to write the dispatch by hand (FR-002). | A source generator was rejected as disproportionate and would be the repository's first. Reflection is cached per closed generic type, so it runs once, not per document; if it ever appears in a profile a generator can replace it behind the same public surface. |

### T035 — the dictionary-relationships gap, decided

**Accepted as a permanent gap, not a follow-up.** `ResourceDocument<TAttributes>` and
`ResourceCollectionDocument<TAttributes>` keep `IReadOnlyList<Resource>? Included` and cannot declare
sideloadable types. Read strictly this is a partial gap against FR-001.

Two things make it the right call rather than an omission. The arity slot a typed version needs is
already `ResourceDocument<TAttributes, TRelationships>`, and reordering the type parameters to free
one would silently reinterpret every existing arity-3 usage — the one failure mode this design
exists to avoid (research D3). And the flavour is explicitly the escape hatch for an author who does
not know their relationship names at compile time; such an author is not in a position to enumerate
their sideloadable types either.

The consequence is that these two forms are also the only ones whose `Included` member is unchanged
by this feature, so nothing assigning to them breaks. Reopening it would need a different arity
scheme, not an addition to this one.
