---

description: "Task list for OpenAPI Envelope Schemas"
---

# Tasks: OpenAPI Envelope Schemas

**Input**: Design documents from `/specs/003-openapi-envelope-schemas/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md)

**Tests**: Included, and not optional here. The spec's Assumptions state "Evidence is behavioural.
Compiling is not evidence", the constitution requires a schema change be verified "by reading the
generated document back", and research R5 established that the OpenAPI package has **zero** test
coverage today. Every story's acceptance is a claim about emitted schema content, so the tests are
the deliverable, not a garnish.

**Organization**: Grouped by user story. Each story is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths in every task

## Path Conventions

Paths are as laid out in plan.md's *Source Code* tree:

- Core wire model: `libs/JsonApiLite/` — net8.0 + net10.0, **zero package references**
- Description package: `libs/JsonApiLite.OpenApi/` — net10.0
- Tests: `libs/tests/`
- Sample: `JsonApiPoc.Api/` — consumes **published** packages, not local source

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create somewhere to put evidence. Nothing here changes behaviour.

- [X] T001 Create `libs/tests/JsonApiLite.OpenApi.Tests/JsonApiLite.OpenApi.Tests.csproj` targeting
      `net10.0` only, mirroring `libs/tests/JsonApiLite.Tests/JsonApiLite.Tests.csproj` for package
      versions (xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4,
      coverlet.collector 6.0.4), with `IsPackable=false`, `<Using Include="Xunit" />`, and
      `ProjectReference`s to `libs/JsonApiLite.OpenApi/JsonApiLite.OpenApi.csproj` and
      `libs/JsonApiLite/JsonApiLite.csproj`. Single-target, not multi-target: the package under test
      is net10.0-only (see plan.md, *One gate that needs stating plainly*).
- [X] T002 Add `InternalsVisibleTo("JsonApiLite.OpenApi.Tests")` to
      `libs/JsonApiLite.OpenApi/JsonApiLite.OpenApi.csproj`. `JsonApiBody` and
      `JsonApiSchemaBuilder` are `internal` (`JsonApiBody.cs:22`, `JsonApiSchemaBuilder.cs:13`) and
      there is no existing `InternalsVisibleTo` in the repository. This is a same-repository test
      assembly, so the objection recorded in research R4 against `InternalsVisibleTo` — mismatched
      versions of separately published packages — does not apply; add a comment saying so, because
      the two decisions otherwise look contradictory.
- [X] T003 Register the new project in `JsonApiLite.sln` so `dotnet test JsonApiLite.sln -c Release`
      runs it. Add only the test project — do **not** carry over the uncommitted edit that adds
      `JsonApiPoc.Api` to the solution, which contradicts `CLAUDE.md`.
- [X] T004 [P] Create fixture contracts in `libs/tests/JsonApiLite.OpenApi.Tests/TestContracts.cs`:
      attributes records implementing `IResourceType`/`IAttributes`, relationships records, a
      declared meta record (`: IMeta`, not deriving from `Meta`), a self-referencing meta record, a
      meta record with a nested object/list/enum, and a declared sideload shape (`: IIncluded`) naming
      two resource types. Mirror the sample's shapes in `JsonApiPoc.Api/Contracts.cs` so the fixtures
      and the sample cannot drift apart in what they exercise.
- [X] T005 [P] Create `libs/tests/JsonApiLite.OpenApi.Tests/SchemaFixture.cs` — a helper that builds
      a `JsonApiBody` for a given document type and status/request role, runs `JsonApiSchemaBuilder`
      over it, and serializes the resulting `OpenApiSchema` to a `JsonNode` so tests assert against
      emitted JSON rather than against object graphs. Assertions on JSON are what the constitution's
      "read the generated document back" means at unit level.
- [X] T006 Add `libs/tests/JsonApiLite.OpenApi.Tests/EnvelopeBaselineTests.cs` pinning **today's**
      behaviour: a resource document, a collection document, a to-one and to-many linkage document
      and an error document each emit exactly the members they emit now. This is the characterization
      net that proves later phases changed only what they meant to (FR-018).

**Checkpoint**: `dotnet test JsonApiLite.sln -c Release` runs the new project and T006 passes
against unmodified source. If T006 fails here, the fixture is wrong, not the library.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The envelope assembly seam every envelope member hangs off, and the response-only gate.

**⚠️ Blocks US2, US3 and US4.** It does **not** block US1 — US1 changes document *type resolution*,
which is a different file and a different concern. US1 and Phase 2 may proceed in parallel.

- [X] T007 Replace the single-line envelope in `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs:32`
      (`return Object(new() { ["data"] = data }, ["data"]);`) with a composition point that starts
      from `{ data }` and conditionally adds envelope members. It must emit byte-identical output
      until a later phase adds a member — T006 is the proof.
- [X] T008 Gate envelope members on `body is JsonApiResponseBody` in
      `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs`. The request/response split already exists
      (`JsonApiBody.cs:126-140`), so this is a branch, not new plumbing (FR-020).
- [X] T009 [P] Add `libs/tests/JsonApiLite.OpenApi.Tests/RequestSchemaTests.cs` asserting a request
      body schema carries `data` and nothing else, for every document form — including one whose type
      declares a metadata shape and a sideload shape. Guards the "Request bodies" edge case, which is
      the one a later phase is most likely to break by accident.
- [X] T010 [P] Add `libs/tests/JsonApiLite.OpenApi.Tests/RequiredMembersTests.cs` asserting that
      every `required` array anywhere in an emitted document contains only `data`, `errors`,
      `type`/`id`, or `href`. Written now so it fails the moment any later phase marks an envelope
      member required (FR-010, FR-015).

**Checkpoint**: Envelope seam exists, emits nothing, and is fenced by tests that will catch the two
most likely regressions before they reach the sample.

---

## Phase 3: User Story 1 — An endpoint that declares its sideloadable types can be described (P1) 🎯 MVP

**Goal**: The annotation accepts a document declaring its sideload shape, and the sample starts.

**Independent Test**: Annotate an endpoint whose response is
`ResourceDocument<A, R, M, I>`, start the application, read the published description. It starts, and
that endpoint's `data` is identical to the same document declaring no sideload shape.

### Tests for User Story 1 ⚠️

> Write these first. T011 must fail with the exact `ArgumentException` before T015 lands.

- [X] T011 [P] [US1] Add `libs/tests/JsonApiLite.OpenApi.Tests/DocumentResolutionTests.cs` asserting
      that `ResourceDocument<A,R,M,I>` and `ResourceCollectionDocument<A,R,M,I>` are accepted. Pin the
      current failure first, so the test demonstrates the reported crash rather than merely the fix.
- [X] T012 [P] [US1] In the same file, assert the emitted `data` for `ResourceDocument<A,R,M,I>` is
      **identical** to that for `ResourceDocument<A,R>` — FR-002, "described identically". Compare
      serialized JSON, not object references.
- [X] T013 [P] [US1] In the same file, assert every arity accepted today is still accepted:
      `ResourceDocument<A>`, `<A,R>`, `<A,R,M>`, the four `ResourceCollectionDocument` forms,
      `ToOneLinkageDocument`, `ToManyLinkageDocument`, `ErrorDocument`.
- [X] T014 [P] [US1] In the same file, assert an unsupported type (e.g. `string`) still throws
      `ArgumentException` naming the offending type and the accepted forms (FR-003). Widening must not
      be achieved by deleting the check.

### Implementation for User Story 1

- [X] T015 [US1] Rework `Describe(Type)` in `libs/JsonApiLite.OpenApi/JsonApiBody.cs:62-102` to
      resolve a document type by walking its base-type chain to the four-argument generic base of
      either family, then read arguments 0–3. Delete the `SingleDocuments`/`CollectionDocuments`
      arity sets at `JsonApiBody.cs:48-60` — they are what `002` broke, and per research R1 the chain
      walk supersedes them. Keep the non-generic matches for linkage and error documents, and keep the
      `throw` at `:98-101` as the fall-through.
- [X] T016 [US1] Extend `JsonApiBody.Description` (`libs/JsonApiLite.OpenApi/JsonApiBody.cs:118-123`)
      with `Meta` and `Included` type members, and populate them from arguments 2 and 3 in T015.
      Normalisation is deferred to US2 and US4; here they are carried, unused.
- [X] T017 [US1] Update the XML doc on `JsonApiBody` (`JsonApiBody.cs:18-21`) to describe resolution
      by inheritance rather than by arity. The existing comment at `:46-47` justifying open-generic
      matching is now stale — replace it with one saying why the chain walk is used, per Principle V
      ("comments explain why, not what").

**Checkpoint**: US1 complete. `dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release` and, with
the temporary local reference from T037, the sample starts — SC-001.

---

## Phase 4: User Story 2 — A paged list endpoint publishes its page counts (P2)

**Goal**: A declared metadata shape reaches the published description as named, typed members.

**Independent Test**: Read the description for a collection endpoint declaring a metadata shape; its
envelope describes each member with its type. Verifiable with no link or sideload member present.

**Depends on**: Phase 2 (envelope seam) and US1 (`Description.Meta` is populated there).

### Tests for User Story 2 ⚠️

- [X] T018 [P] [US2] Add `libs/tests/JsonApiLite.OpenApi.Tests/MetaSchemaTests.cs` asserting a
      declared meta record is walked into named, typed members (FR-004).
- [X] T019 [P] [US2] In the same file, assert a document whose `TMeta` is `Meta` emits
      `{ "type": "object" }` with **no** properties — and specifically no `members` property. This is
      the research R2 trap: `Meta` is a `JsonObject` behind `MetaConverter` (`Meta.cs:13-18`), so
      walking it would describe something never on the wire.
- [X] T020 [P] [US2] In the same file, assert the same for `Meta<TMeta>`, which satisfies the
      `IMeta` constraint and would otherwise be described as `{ members, value }`. Equality against
      `typeof(Meta)` is not sufficient — the guard must test derivation.
- [X] T021 [P] [US2] In the same file, assert a declared shape containing a nested object, a list and
      an enum is described to the same depth and with the same naming/enum conventions as attributes
      (FR-005, FR-006), and that a self-referencing shape terminates.

### Implementation for User Story 2

- [X] T022 [US2] In `libs/JsonApiLite.OpenApi/JsonApiBody.cs`, normalise `Description.Meta` to `null`
      when the third type argument is `Meta` or derives from it; otherwise carry the declared type
      (research R2). Comment the derivation test with *why* — the converter owns the wire form.
- [X] T023 [US2] In `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs`, emit the `meta` member on
      response envelopes: run the existing `Schema(Type, HashSet<Type>)` walker
      (`JsonApiSchemaBuilder.cs:168-218`) over a declared shape, or emit an unconstrained
      `{ "type": "object" }` when `Description.Meta` is `null`. Never omit the member and never invent
      member names (FR-007). Reuse the walker as-is; it already handles nesting, enums and
      self-reference.

**Checkpoint**: US1 and US2 both work. The sample's `/contacts` publishes `total` and `pageCount`;
`/contacts/{id}` publishes an unconstrained `meta`.

---

## Phase 5: User Story 3 — A paged list endpoint is visibly paged (P3)

**Goal**: The link members appear, varying by document kind.

**Independent Test**: Read the description for a collection endpoint; `links` describes `self`,
`first`, `prev`, `next`, `last`, each accepting a URL or a `{ href, meta }` object.

**Depends on**: Phase 2 only. **Independent of US1 and US2** — link sets are chosen from `Shape` and
`Collection`, which are already computed and unchanged by this feature.

### Tests for User Story 3 ⚠️

- [X] T024 [P] [US3] Add `libs/tests/JsonApiLite.OpenApi.Tests/LinksSchemaTests.cs` asserting the
      per-kind table from research R3: single resource → `self`; resource collection → `self`,
      `first`, `prev`, `next`, `last`; to-one linkage → `self`, `related`; to-many linkage → `self`,
      `related`, plus pagination; error → `self`.
- [X] T025 [P] [US3] In the same file, assert each link member is an `anyOf` of a string with
      `format: uri` and an object with `href` (required) and `meta` — matching `Link.cs:9-11` and the
      spec's "a string whose value is a URI-reference pointing to the link's target, a link object or
      `null` if the link does not exist." (FR-009).
- [X] T026 [P] [US3] In the same file, assert **no** emitted document anywhere contains a
      `describedby` member. `Links.cs:7-16` has no such member, and FR-008a forbids describing a link
      member the library cannot produce. This test is the enforcement of the 2026-07-29 clarification.
- [X] T027 [P] [US3] In the same file, assert pagination links appear **only** on collection-primary
      kinds, per the spec: "Pagination links **MUST** appear in the links object that corresponds to a
      collection."

### Implementation for User Story 3

- [X] T028 [US3] Add a hand-written `Links(JsonApiShape, bool collection)` builder to
      `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs`, alongside the existing hand-written
      `ErrorDocument()` (`:137-163`). Written out rather than reflected for the reason already
      recorded at `:135-136` — `Links` and `Meta` carry their own converters, so reflecting them would
      describe CLR fields instead of the wire. Select members with explicit `if` blocks, not nested
      ternaries (Principle V).
- [X] T029 [US3] Emit the `links` member on response envelopes from the seam added in T007, and
      confirm nothing is added to any `required` set (T010 is the guard).

**Checkpoint**: US1–US3 work. A consumer can see that `/contacts` pages — SC-003.

---

## Phase 6: User Story 4 — Sideloaded resources are described from the declaration (P4)

**Goal**: `included` is described from the sideload shape the author already declared.

**Independent Test**: Read the description for an endpoint declaring two sideloadable types; the
envelope describes one array whose entries are constrained to those types.

**Depends on**: Phase 2 and US1 (`Description.Included` is populated there). Independent of US2/US3.

### Tests for User Story 4 ⚠️

- [X] T030 [P] [US4] Add `libs/tests/JsonApiLite.OpenApi.Tests/IncludedSchemaTests.cs` asserting a
      declared sideload shape produces **one** array whose `items` is an `anyOf` over the declared
      resource schemas, each with its `type` pinned to the right constant (FR-011, FR-012). One flat
      array, per the spec: "In a compound document, all included resources **MUST** be represented as
      an array of resource objects in a top-level `included` member."
- [X] T031 [P] [US4] In the same file, assert a document resolving to `AnyIncluded` describes an
      unconstrained resource array and claims no specific type (FR-013), and that a declared shape
      naming **no** types behaves identically — the "declaration naming no types at all" edge case.
- [X] T032 [P] [US4] In the same file, assert linkage and error documents never describe `included`
      (FR-022), and that a sideloadable type equal to the primary data's type does not collide with
      the primary data's own schema.
- [X] T033 [P] [US4] Add `libs/tests/JsonApiLite.Tests/Serialization/IncludedDeclarationTests.cs`
      covering the new core accessor directly: declared order is stable, resource type names come
      from `IResourceType`, an empty shape and `AnyIncluded` both yield nothing. Runs on **both**
      TFMs, since it tests the core package.

### Implementation for User Story 4

- [X] T034 [US4] Add the public accessor to the core package in
      `libs/JsonApiLite/Serialization/IncludedShape.cs` (or a new file alongside it), reporting per
      declared member: the wire resource type name and the element type. It must read the existing
      cached `IncludedShape` (`IncludedShape.cs:30-37`) rather than re-reflecting, so there is one
      implementation of what a declaration names (FR-014). Do **not** expose `IncludedMember.Property`
      or `ListType` (`IncludedShape.cs:19-22`) — serialization mechanics. Carry XML docs saying what
      it is *for* (Principle V). Adds no package reference; Principle III holds.
- [X] T035 [US4] In `libs/JsonApiLite.OpenApi/JsonApiBody.cs`, normalise `Description.Included` to
      `null` when the fourth type argument is `AnyIncluded`, which declares nothing
      (`AnyIncluded.cs:7-8`).
- [X] T036 [US4] In `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs`, emit `included` on resource
      response envelopes: for each declared entry, unwrap the element type
      (`Resource<TAttributes, TRelationships>`, `Resource.cs:123`) to its two type arguments and reuse
      the existing `Resource(...)` path (`:35-66`); combine with `anyOf`. When `Description.Included`
      is `null`, describe an unconstrained resource array.

**Checkpoint**: All four stories functional and independently verifiable at unit level.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T037 Verify against the sample. Temporarily replace the two `PackageReference` lines in
      `JsonApiPoc.Api/JsonApiPoc.Api.csproj:13-14` with `ProjectReference`s to `libs/JsonApiLite` and
      `libs/JsonApiLite.OpenApi`, run the app in Development, and capture `/openapi/v1.json`.
      **Revert the swap before committing.** State which reference mode produced any reported output —
      the constitution requires that gap be stated, not glossed.
- [X] T038 Walk [quickstart.md](quickstart.md) end to end against the captured document, confirming
      every story's expected output and both contrast cases (unconstrained `meta`, unconstrained
      `included`).
- [X] T039 Validate a real sample response against the published description (FR-017, SC-005).
      Because `additionalProperties` is never set, a passing validation alone does not prove the
      envelope is described — also compare the response's members against the schema's `properties`.
- [X] T040 Run the full constitution gate and report the output verbatim:
      `dotnet build JsonApiLite.sln -c Release`, `dotnet test JsonApiLite.sln -c Release`,
      `dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release`. Confirm every pre-existing
      wire-format test in `libs/tests/JsonApiLite.Tests` passes **unmodified** (SC-006, FR-019) — if
      one needed editing, the change exceeded this feature's scope.
- [X] T041 [P] Update `README.md` to document the envelope members now described and the new core
      accessor, and check `ROADMAP.md` for an entry to mark advanced.
- [X] T042 Confirm the core package still has zero package references and both TFMs build
      (`libs/JsonApiLite/JsonApiLite.csproj`) — Principle III, verified rather than assumed.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: needs Phase 1. Blocks **US2, US3, US4** — not US1.
- **US1 (Phase 3)**: needs Phase 1 only. May run in parallel with Phase 2.
- **US2 (Phase 4)**: needs Phase 2 **and** US1 (T016 populates `Description.Meta`).
- **US3 (Phase 5)**: needs Phase 2 only. Independent of US1, US2, US4.
- **US4 (Phase 6)**: needs Phase 2 **and** US1 (T016 populates `Description.Included`).
- **Polish (Phase 7)**: needs every story intended for the release. T037 needs US1 at minimum — the
  sample cannot start without it.

### Story dependency graph

```
Phase 1 ──┬─────────────► US1 (P1) ──┬──► US2 (P2)
          │                          └──► US4 (P4)
          └──► Phase 2 ──────────────┴──► US3 (P3)   [US3 needs Phase 2 only]
```

### Within each story

- Tests first, and confirmed failing, before implementation. T011 in particular must reproduce the
  reported `ArgumentException` before T015 lands.
- Type resolution (`JsonApiBody.cs`) before schema emission (`JsonApiSchemaBuilder.cs`).
- Core accessor (T034) before the OpenAPI side consumes it (T036).

### Parallel opportunities

- T004, T005 in Phase 1 (different new files).
- T009, T010 in Phase 2 (different new files).
- All four US1 tests (T011–T014) — same file, so one author, but independent of Phase 2 entirely.
- **US3 is the big one**: it needs only Phase 2, so it can run start-to-finish alongside US1.
- US2 and US4 can run in parallel with each other once US1 lands — they touch the same two files, so
  coordinate, but neither depends on the other.

---

## Parallel Example: after Phase 1

```bash
# Two independent tracks:
Track A: Phase 2 (T007-T010) → US3 (T024-T029)          # links, needs no type-argument work
Track B: US1 (T011-T017)                                 # document resolution, unblocks the sample

# Then, once both land:
Track A: US2 (T018-T023)
Track B: US4 (T030-T036)
```

---

## Implementation Strategy

### MVP: US1 alone

1. Phase 1 (T001–T006) — somewhere to put evidence.
2. Phase 3 (T011–T017) — the chain walk.
3. **Stop and validate**: the sample starts. That is SC-001, and it is the difference between a
   broken sample and a working one — worth shipping on its own, ahead of any envelope member.

Note this inverts the originating issue's own ordering, which called the `meta` fix "worth doing
alone". That was written before `002` shipped the four-argument form; an undescribed envelope on a
running app beats a well-described one on an app that will not start.

### Incremental delivery

1. Setup → US1 → **ship** (sample starts).
2. Phase 2 → US2 → ship (page counts published — the issue's original headline).
3. US3 → ship (paging is visible).
4. US4 → ship (sideloaded types published — closes the issue).

Each step is independently valuable and independently verifiable.

---

## Notes

- **The sample cannot verify local changes.** It consumes published packages
  (`JsonApiPoc.Api/JsonApiPoc.Api.csproj:13-14`, `1.1.1-preview.10.44`). Every sample-based task
  (T037–T039) requires the temporary reference swap, and every reported result must say which mode
  produced it.
- **Two working-tree items are not this feature's work but block its evidence**: `Contracts.cs:59`
  still declares the `Undeclared` member that commit `ddbb34b` removed from the core, and
  `JsonApiLite.sln` has an uncommitted edit adding `JsonApiPoc.Api`. Settle both before T037.
- Conventional Commits are mandatory; T034 adds public API to the core package, so its commit message
  determines the version semantic-release computes. Additive — a `feat:`, not a `feat!:`.
- Commit after each task or logical group. Stop at any checkpoint to validate a story independently.
