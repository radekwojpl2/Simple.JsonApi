---

description: "Task list for Typed Included Resources"
---

# Tasks: Typed Included Resources

**Input**: Design documents from `/specs/002-typed-included-resources/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md

**Tests**: Test tasks ARE included. Not a default choice — the spec requires them. Its Assumptions
state "Evidence is behavioural. Compiling is not evidence", FR-015 requires every existing wire-format
test to pass unmodified, and the constitution's *Build, Test and Verification Gates* make
`dotnet test` a precondition for calling the work complete.

**Organization**: Tasks are grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1, US2, US3)

## Path Conventions

Paths are repository-relative from `D:\git\JsonApiPoc`. The core library is `libs/JsonApiLite/`,
tests are `libs/tests/JsonApiLite.Tests/`, and the sample is `JsonApiPoc.Api/` (**not** in
`JsonApiLite.sln` — build it explicitly).

---

## Two findings that shape this list

Both established by compiling probes; see [research.md](research.md) D4/D5.

1. **Existing test call sites need no migration.** All eight `Included = […]` sites in the test
   project use collection-expression literals, which keep compiling because `AnyIncluded` carries
   `[CollectionBuilder]`. So FR-015's "every existing test passes unmodified" is achievable literally,
   and T009 makes it a hard checkpoint rather than an aspiration.

2. **Exactly one call site in the repository breaks**: `JsonApiPoc.Api/Program.cs:114`,
   `Included = included.Count > 0 ? included : null`, where `included` is a `List<Resource>`. It
   cannot be fixed until a package is published, because the sample consumes published packages. That
   is why the sample migration is a late, explicitly-blocked task (T029) rather than part of the main
   flow.

---

## Phase 1: Setup

**Purpose**: Clear the constitution gate and establish a known-good baseline before anything changes.

- [X] T001 Add a roadmap entry for typing the wire model's sideload member in `ROADMAP.md`, under *Next* or *Later*, citing issue #9. Required by the constitution's *Development Workflow* ("A feature proposal MUST cite ROADMAP.md"); the existing `included` entry covers OpenAPI schemas, not the wire model, so this feature currently has no entry to cite.
- [X] T002 Record a green baseline (76 tests per TFM, 152 total; all three gates green) by running `dotnet build JsonApiLite.sln -c Release`, `dotnet test JsonApiLite.sln -c Release` and `dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release`, saving the test counts for later comparison against T009 and T031.

**Checkpoint**: Gate cleared, baseline known.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The types every user story needs. Nothing in Phase 3+ can start until this is done.

**⚠️ CRITICAL**: No user story work can begin until this phase completes.

- [X] T003 [P] Create the `IIncluded` marker interface in `libs/JsonApiLite/Resources/IIncluded.cs`, with the single member `IReadOnlyList<Resource> Undeclared { get; }`, XML-documented as the only route to a sideloaded resource whose type the declaration does not name.
- [X] T004 [P] Create `AnyIncluded` and `AnyIncludedBuilder` in `libs/JsonApiLite/Resources/AnyIncluded.cs`, implementing `IIncluded` **and** `IReadOnlyList<Resource>`, carrying `[CollectionBuilder(typeof(AnyIncludedBuilder), nameof(AnyIncludedBuilder.Create))]`. Add a comment recording *why* it implements `IReadOnlyList<Resource>` — it is the reason existing call sites survive, and that is invisible from the code alone.
- [X] T005 Create the cached type-to-member map in `libs/JsonApiLite/Serialization/IncludedShape.cs`, built by reflecting over a closed `TIncluded`: each member of shape `IReadOnlyList<Resource<TAttributes, …>>` contributes one entry keyed by `TAttributes.ResourceType`. Cache per closed generic type so reflection runs once, never per document. Depends on T003.
- [X] T006 Reject a declaration whose two members claim the same wire type name, in `libs/JsonApiLite/Serialization/IncludedShape.cs`, throwing at map-construction time with a message naming the duplicated type and both members. Depends on T005.
- [X] T007 Add the arity-4 form `ResourceDocument<TAttributes, TRelationships, TMeta, TIncluded>` in `libs/JsonApiLite/Documents/ResourceDocument.cs`, changing `Included` to `TIncluded?`, constraining `where TIncluded : class, IIncluded`, and re-basing the arity-3 and arity-2 forms onto it with `AnyIncluded` as the default so their meanings are unchanged. Depends on T003, T004.
- [X] T008 Apply the same arity-4 change to `libs/JsonApiLite/Documents/ResourceCollectionDocument.cs`, mirroring T007 exactly. Depends on T003, T004.
- [X] T009 Confirm FR-015 and SC-004 hold by rebuilding `libs/tests/JsonApiLite.Tests` with **no edits to any test file** and comparing the test count against the T002 baseline. Any test file requiring an edit here is a design regression against research D4, not a test to fix. Depends on T007, T008.

**Checkpoint**: The types exist, existing tests still compile and pass untouched, and no serialization behaviour has changed yet.

> **Correction found during implementation.** This checkpoint is not reachable as ordered. `AnyIncluded`
> implements `IReadOnlyList<Resource>`, so System.Text.Json claims it with the built-in collection
> converter and throws on read: *"The collection type 'JsonApiLite.AnyIncluded' is abstract, an
> interface, or is read only, and could not be instantiated and populated."* Six existing tests failed
> on that alone, with the write path unaffected. The converter (T013/T014) is therefore a prerequisite
> of T009, not a consequence of it, and was implemented first. Research D4's actual claim —
> *source* compatibility — held exactly: zero test files edited, zero compile errors.

---

## Phase 3: User Story 1 — Sideloaded resources are read by member, not by cast (Priority: P1) 🎯 MVP

**Goal**: An author declares the types their document may sideload, then reads their attributes with
no cast and no runtime type test.

**Independent Test**: Parse a document sideloading two declared types and read an attribute from each
without a type test or unwrapping step. Verifiable with the write path untouched.

### Tests for User Story 1

> Write these first and watch them fail before implementing T013.

- [X] T010 [P] [US1] Add a declared sideload shape for the test fixtures in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` — a record with `Companies` and `Tags` members plus `Undeclared` — mirroring the existing `CompanyAttributes`/`TagAttributes` fixtures.
- [X] T011 [P] [US1] Add a test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting that a parsed document exposes sideloaded companies and tags per declared member, with attributes readable directly (spec acceptance scenarios 1 and 2).
- [X] T012 [P] [US1] Add tests in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` covering an absent sideload member versus a present-but-empty one (FR-007, scenario 3), and covering a sideloaded resource carrying its own relationships and meta (FR-006, scenario 4).

### Implementation for User Story 1

- [X] T013 [US1] Implement the read path in `libs/JsonApiLite/Serialization/IncludedConverter.cs`: for each array element peek `type`, resolve through the `IncludedShape` map, deserialize into the resolved concrete resource type and append it to that member. Use `if` blocks with early returns, matching `ResourceConverter.cs:27-42` — no ternary returns. Depends on T005.
- [X] T014 [US1] Register `IncludedConverter` for `IIncluded`-constrained members in `libs/JsonApiLite/Serialization/JsonApiSerializer.cs`, leaving the existing `ResourceTypeRegistry` path untouched for documents that declare nothing. Depends on T013.
- [X] T015 [US1] Confirm a declared document deserializes its sideloaded resources concretely **without** a `ResourceTypeRegistry` being supplied, adding the assertion to `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs`. This is the D2 consequence — the declaration is its own registry — and it is the thing most likely to regress silently.

**Checkpoint**: User Story 1 is fully functional. `document.Included?.Companies?[0].Attributes?.Name` compiles and returns the right value — the feature in one line.

---

## Phase 4: User Story 2 — A compound document is assembled from declared parts (Priority: P2)

**Goal**: An author supplies sideloaded resources per declared type, and the bytes on the wire are
unchanged from what the same endpoint sends today.

**Independent Test**: Assemble a document supplying two declared types, serialize it, and compare
against the same document assembled the existing way. The two must be byte-identical.

### Tests for User Story 2

- [X] T016 [P] [US2] Add a test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting that a document assembled per declaration serializes byte-identically to the same document assembled with `AnyIncluded` (FR-010, scenario 1).
- [X] T017 [P] [US2] Add a test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting that unset declared members contribute nothing — no empty entries, no placeholders (scenario 2).
- [X] T018 [P] [US2] Add a test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting that a document with nothing sideloaded omits the `included` member entirely rather than writing `[]` (FR-011, scenario 3).
- [X] T019 [P] [US2] Add a round-trip test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting serialize-then-parse yields an equivalent document (scenario 4).

### Implementation for User Story 2

- [X] T020 [US2] Implement the write path in `libs/JsonApiLite/Serialization/IncludedConverter.cs`: enumerate declared members in declaration order, then `Undeclared`, concatenating into one flat JSON array as the specification requires. Depends on T013.
- [X] T021 [US2] Make write ordering deterministic in `libs/JsonApiLite/Serialization/IncludedShape.cs` by fixing member order at map-construction time, with a comment noting the specification imposes no ordering within `included` and that determinism is required only so serialized-output comparisons are stable. Depends on T005, T020.
- [X] T022 [US2] Settle the open `notSure` from research D7 by checking whether any existing test asserts a specific ordering of `included` elements — start at `libs/tests/JsonApiLite.Tests/Documents/CompoundDocumentTests.cs:138` — and record the finding in `specs/002-typed-included-resources/research.md`, replacing the `notSure` marker with the answer.

**Checkpoint**: Stories 1 and 2 both work. The round trip is closed and the wire is provably unchanged.

---

## Phase 5: User Story 3 — A document carrying an undeclared resource type is not silently damaged (Priority: P3)

**Goal**: A sideloaded resource whose type no member names stays reachable and survives a round trip.

**Independent Test**: Declare one type, parse a document sideloading that type plus an undeclared one,
and confirm the undeclared resource is reachable and re-emitted unchanged.

### Tests for User Story 3

- [X] T023 [P] [US3] Add a test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting an undeclared sideloaded type lands in `Undeclared` rather than being dropped (FR-012, scenario 1).
- [X] T024 [P] [US3] Add a test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting an undeclared resource survives parse-then-write unchanged (FR-013, scenario 2).
- [X] T025 [P] [US3] Add a test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` asserting that parsing succeeds — an undeclared sideloaded type is not an error (FR-014, scenario 3).

### Implementation for User Story 3

- [X] T026 [US3] Route unresolved elements to `Undeclared` in `libs/JsonApiLite/Serialization/IncludedConverter.cs`, deserializing them as `Resource<JsonObject>` exactly as `ResourceConverter.cs:41` already does for the untyped path. Depends on T013.
- [X] T027 [US3] Include `Undeclared` in the write path in `libs/JsonApiLite/Serialization/IncludedConverter.cs` so round-tripping re-emits undeclared resources. Depends on T020, T026.
- [X] T028 [US3] Add an edge-case test in `libs/tests/JsonApiLite.Tests/Documents/TypedIncludedTests.cs` for a document sideloading the same resource type as its primary data, confirming the declaration can name it without colliding with the primary data's own declaration (spec Edge Cases).

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T029 Migrate the sample's one breaking call site at `JsonApiPoc.Api/Program.cs:114` from `Included = included.Count > 0 ? included : null` to `Included = included.Count > 0 ? [.. included] : null`. **No longer blocked.** The task assumed this had to wait for a publish, but the spread form compiles against the *published* package too — where `Included` is still `IReadOnlyList<Resource>?` — so it is forward- and backward-compatible. Applied and verified with `dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release`: 0 errors. Note `new AnyIncluded(included)` would **not** have worked here, since that type does not exist in the published package. The sample still exercises the old package at runtime; that gap remains until publish.
- [X] T030 Write the migration note required by FR-022 in `README.md` or a release note: `Included = x` becomes `Included = [.. x]`, or `new AnyIncluded(x)` where the copy is unwanted. State that every break is a compile error (`CS0266`/`CS0029`) and that no silent behaviour change is possible. List the surviving forms so readers do not migrate what does not need it.
- [X] T031 Run all three verification gates — `dotnet build JsonApiLite.sln -c Release`, `dotnet test JsonApiLite.sln -c Release`, `dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release` — confirming tests pass on **both** `net8.0` and `net10.0`. A change passing on one TFM only is a failing change.
- [X] T032 [P] Add XML docs to every new public member across `libs/JsonApiLite/Resources/IIncluded.cs` and `libs/JsonApiLite/Resources/AnyIncluded.cs`, saying what each member is *for* rather than restating its name.
- [X] T033 [P] Walk `specs/002-typed-included-resources/quickstart.md` end to end against the built library, correcting anything that does not behave as written.
- [ ] T034 Record the breaking change in the commit message so semantic-release computes the version. Never set a version by hand and never edit `CHANGELOG.md` — it is generated.
- [X] T035 Confirm the dictionary-relationships gap is acceptable, or record it as a follow-up issue: `ResourceDocument<TAttributes>` cannot declare sideloadable types because the arity slot is taken (research D3). Read strictly this is a partial gap against FR-001 and should be an explicit decision rather than a silent omission.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies. T001 is a genuine gate, not paperwork — the constitution makes the roadmap citation mandatory.
- **Foundational (Phase 2)**: Depends on Setup. **Blocks every user story.**
- **User Stories (Phases 3–5)**: All depend on Phase 2. US2 and US3 additionally depend on US1's converter skeleton (T013), so they are less independent here than the template's default shape assumes — see below.
- **Polish (Phase 6)**: Depends on the stories being delivered.

### User Story Dependencies

- **US1 (P1)**: Depends only on Phase 2. Fully independent. This is the MVP.
- **US2 (P2)**: Depends on T013 from US1, because read and write share one converter. It is independently *testable* (byte-identical output is its own assertion) but not independently *buildable* — an honest deviation from the template's assumption, and it follows from the wire format having a single flat array rather than from a modelling choice.
- **US3 (P3)**: Depends on T013 (read) and T020 (write). Independently testable; cannot precede the other two.

### Within Each User Story

- Tests are written before implementation and must fail first.
- Foundational types before the converter; the converter before registration.

### Parallel Opportunities

- T003 and T004 are different files with no interdependency.
- All test-authoring tasks within a story (T010–T012, T016–T019, T023–T025) are the same file but independent cases; parallel only if split across separate files first.
- T032 and T033 are independent of each other.

---

## Parallel Example: Phase 2 Foundational

```bash
# T003 and T004 touch different files and share no dependency:
Task: "Create IIncluded in libs/JsonApiLite/Resources/IIncluded.cs"
Task: "Create AnyIncluded + AnyIncludedBuilder in libs/JsonApiLite/Resources/AnyIncluded.cs"

# Then, once both land:
Task: "Create IncludedShape in libs/JsonApiLite/Serialization/IncludedShape.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup — clear the roadmap gate, record the baseline.
2. Phase 2 Foundational — **critical**, blocks everything.
3. Phase 3 User Story 1.
4. **STOP and VALIDATE**: parse a two-type compound document and read both attributes with no cast.
5. That alone is worth shipping: it is the whole of the complaint in issue #9.

### Incremental Delivery

1. Setup + Foundational → existing tests still green, untouched (T009).
2. + US1 → typed reads work → **MVP**.
3. + US2 → round trip closed, wire provably unchanged.
4. + US3 → undeclared types safe.
5. + Polish → migration note, sample, release.

### Notes

- **T009 is the most valuable checkpoint in the list.** If any existing test needs editing there, the
  `AnyIncluded : IReadOnlyList<Resource>` design has not delivered what research D4 claims, and that
  is worth stopping for rather than editing tests to fit.
- **T029 stays blocked until publish.** Do not report the sample as verified against this change
  before a package exists; the constitution requires that gap be stated rather than glossed over.
- Commit after each task or logical group, with Conventional Commits.
