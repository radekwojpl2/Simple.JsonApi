# Feature Specification: Typed Included Resources

**Feature Branch**: `002-typed-included-resources`
**Created**: 2026-07-27
**Status**: Draft
**Input**: User description: "Read this for specification https://github.com/radekwojpl2/Simple.JsonApi/issues/9"

## User Scenarios & Testing *(mandatory)*

The library's premise is that a JSON:API document is strongly typed: the author states what their
resource's attributes and relationships look like, and every reader of that document reaches for
members rather than digging through untyped data. The sideload member is the one place where that
premise stops. An author who receives a document with related resources sideloaded gets back the
shared base shape common to all resources, so reading anything specific to a sideloaded resource
means testing its runtime type first and unwrapping it. Everything the author already declared
about that resource is unavailable at the point of use.

The people affected are the developers who consume documents this library produces or parses —
both the author of an endpoint that sideloads, and the author of a client that reads one.

### User Story 1 - Sideloaded resources are read by member, not by cast (Priority: P1)

A developer receives a document that sideloads related resources. They already know which resource
types their endpoint may sideload, and the library already knows how to turn each one into its
declared shape. This story lets the developer state the set of types once and then reach the
sideloaded resources' attributes directly, without first testing what each element turned out to be.

**Why this priority**: This is the whole of the complaint. Every other story here is either the
mirror image of it on the writing side or a robustness guarantee around it. It is also the half
that is nearly delivered already — the parsing side reconstructs each sideloaded resource in its
declared shape today and then hands it back as the shared base type, so what is missing is the
ability to say so at the point of use, not the ability to work it out.

**Independent Test**: Parse a document that sideloads two different resource types, having declared
both, and read an attribute from each without a runtime type test or unwrapping step. Verifiable
with no change to the writing side.

**Acceptance Scenarios**:

1. **Given** a document declaring the sideloadable resource types, **When** a response sideloading
   those types is parsed, **Then** the sideloaded resources are reachable per declared type and
   their attributes are directly readable.
2. **Given** such a parsed document, **When** a developer reads a sideloaded resource's attribute,
   **Then** no runtime type test and no unwrapping step is required, and a misspelled attribute
   name is caught before the program runs.
3. **Given** a document that declares sideloadable types but whose response sideloads nothing,
   **Then** the sideload member is reported as absent, distinguishably from present-but-empty.
4. **Given** a declaration naming a resource type, **When** a response sideloads that type, **Then**
   each sideloaded resource carries its own relationships and metadata as fully as a resource
   appearing as primary data does.

---

### User Story 2 - A compound document is assembled from declared parts (Priority: P2)

The mirror image on the writing side: a developer building a response that sideloads related
resources supplies them per declared type, and the document that goes out is unchanged from what the
same endpoint sends today.

**Why this priority**: It completes the round trip and is what makes the declaration worth stating
for a server author rather than only a client author. It ranks second because the reading side
delivers the value on its own — an author who sideloads today can keep assembling documents exactly
as they do now while clients already benefit from Story 1.

**Independent Test**: Assemble a document supplying two sideloaded resource types per declaration,
serialize it, and compare the output against the same document assembled the existing way. The two
must be indistinguishable.

**Acceptance Scenarios**:

1. **Given** a developer assembling a document with sideloaded resources supplied per declared type,
   **When** the document is serialized, **Then** the sideload member is a single flat list
   containing every supplied resource, indistinguishable from what the endpoint sends today.
2. **Given** a document assembled with some declared types supplied and others left unset, **When**
   it is serialized, **Then** only the supplied resources appear and the unset types contribute
   nothing — no empty entries, no placeholder.
3. **Given** a document assembled with no sideloaded resources at all, **When** it is serialized,
   **Then** the sideload member is omitted entirely rather than written as an empty list.
4. **Given** any document assembled per declaration, **When** it is serialized and parsed back,
   **Then** the result is equivalent to what was assembled.

---

### User Story 3 - A declaration is exhaustive (Priority: P3)

A document arrives sideloading a resource type the reader's declaration does not name — a server
that added a type, or a client written against an older view of the API. A declaration states what
the document may carry, so a type it does not name was not asked for: the resource is dropped, and
parsing does not fail merely because it arrived.

**Why this priority**: It defines what a declaration *means*, and only matters once the declaration
exists, so it cannot be delivered before Stories 1 and 2.

**Independent Test**: Declare one sideloadable type, parse a document sideloading that type plus a
second undeclared one, and confirm the declared resource arrives, the undeclared one does not, and
no error is raised.

**Acceptance Scenarios**:

1. **Given** a declaration naming a subset of the types a document sideloads, **When** that document
   is parsed, **Then** the declared resources arrive in their members and the undeclared ones are
   dropped.
2. **Given** such a document, **When** it is parsed and written back out, **Then** the dropped
   resources are absent from the output.
3. **Given** such a document, **When** it is parsed, **Then** parsing succeeds — an undeclared
   sideloaded type is not an error.
4. **Given** a document that declares no sideloadable types, **When** it is parsed, **Then** every
   sideloaded resource is kept — dropping is a consequence of declaring, not of the member itself.

---

### Edge Cases

- **A resource type is sideloaded that is also the primary data's type.** A document may sideload
  resources of the same type as its primary data. The declaration must be able to name it, and doing
  so must not collide with the primary data's own declaration.
- **The same resource appears twice.** The specification forbids it — "A compound document MUST NOT
  include more than one resource object for each `type` and `id` pair" — but a document violating it
  can still arrive. Reading must have a defined outcome rather than an accidental one.
- **A sideloaded resource carries no attributes member.** Permitted on the wire. Reading must
  distinguish this from an attributes member that is present and empty.
- **A declaration naming no types at all.** Must be either rejected at declaration time or behave
  exactly as no declaration does; it must not produce a document that can hold nothing.
- **Documents that cannot sideload.** Linkage and error documents have no resource primary data, so
  the sideload member does not apply to them. [Spec: `included` is "an array of resource objects
  that are related to the primary data"] Nothing in this feature may add the member to them.
- **An author who declares nothing.** The overwhelming majority of existing code. Its document
  declaration changes, but nothing else does: the code reading or assembling that document must
  behave identically, and the author must not be forced to enumerate sideloadable types they do not
  know.
- **A sideloaded resource whose relationships point outside the document.** Full linkage is the
  sender's obligation, not the reader's. Reading must not attempt to verify it and must not fail
  when it does not hold.

## Clarifications

### Session 2026-07-27

- Q: Is the typed declaration added alongside the existing document forms, or does it replace them? →
  A: It replaces them — the existing document forms change. One way to express a document, accepted
  as a breaking change for consumers of the published preview packages.
- Q: Once a document declares its sideloadable types, may its sideload member still be read the
  existing untyped way? → A: No — declaring commits the author to the typed view. A declared
  document exposes one way to read its sideload member.

### Session 2026-07-28

- Q: Should a declared document carry an `Undeclared` member holding sideloaded resources whose type
  no member names? → A: No — drop them. The member was ceremony on every declaration: an author who
  has stated which types their document may carry has, by that act, said they want nothing else.
  Story 3 and FR-012/FR-013 were written the opposite way and are revised above.
  - **Accepted cost**: a document that is read and written again loses the resources its declaration
    did not name, with nothing signalling it. Pinned by a test named for the cost rather than left to
    be discovered.
  - **Why this is tolerable**: full linkage makes the sender responsible for what appears in
    `included`, so a server assembling a document knows its own types, and a client parsing one
    rarely re-emits what it did not ask for. An author who cannot enumerate their types keeps the
    untyped form, which still holds everything (FR-014a).
  - **Rejected alternative**: moving the member onto an inherited base record, which would have kept
    the data and removed the boilerplate. Rejected because it keeps a concept the author has no use
    for; the simpler model was preferred over the safer one deliberately.

## Requirements *(mandatory)*

### Functional Requirements

**Declaring (Story 1)**

- **FR-001**: A developer MUST be able to declare, on a document, the set of resource types that
  document's sideload member may carry.
- **FR-002**: The declaration MUST name each resource type exactly once, and MUST reuse the existing
  means by which a resource type's name and shape are already declared, so that a declaration cannot
  drift from the resource it names.
- **FR-003**: Declaring sideloadable types MUST be optional. A document that declares none MUST
  remain expressible and MUST behave as it does today.

**Reading (Story 1)**

- **FR-004**: When a document declares its sideloadable types, sideloaded resources MUST be
  reachable per declared type, with each resource's attributes and relationships available in their
  declared shapes and without a runtime type test at the point of use.
- **FR-005**: An error in a declared type's name or in an attribute reached through it MUST be
  detectable before the program runs, not at the moment the document is read.
- **FR-006**: A sideloaded resource MUST expose everything a resource carries — identity, type,
  attributes, relationships, links and resource-level metadata — to the same degree as a resource
  appearing as primary data.
- **FR-007**: An absent sideload member MUST be distinguishable from one that is present and empty.

**Writing (Story 2)**

- **FR-008**: A developer MUST be able to supply sideloaded resources per declared type when
  assembling a document.
- **FR-009**: The serialized output MUST place every supplied resource into a single flat list, as
  the specification requires: "In a compound document, all included resources MUST be represented as
  an array of resource objects in a top-level `included` member."
- **FR-010**: A document assembled per declaration MUST serialize identically to the same document
  assembled the existing way. This feature changes what a developer writes, never what goes on the
  wire.
- **FR-011**: A document with no sideloaded resources MUST omit the sideload member rather than
  write it empty.

**Undeclared types (Story 3)**

- **FR-012**: A sideloaded resource whose type no declaration names MUST be dropped when the document
  is read. A declaration is exhaustive: it states the resource types the document may carry, so one
  it does not name was not asked for. [Revised 2026-07-28 — this requirement previously demanded the
  opposite, that such a resource be preserved in a named member on every declared document. See
  Clarifications.]
- **FR-013**: A dropped resource MUST NOT appear when the document is written back out.
- **FR-014**: Encountering an undeclared sideloaded type MUST NOT be treated as an error.
- **FR-014a**: A document that declares no sideloadable types MUST keep every sideloaded resource it
  receives. Dropping MUST be a consequence of declaring, never of the sideload member itself.

**Applying to all three**

- **FR-015**: This feature MUST NOT change the serialized form of any document. Every existing test
  covering the wire format MUST pass unmodified.
- **FR-016**: Changing the existing document forms is accepted as a breaking change for consumers of
  the published packages. The break MUST be confined to how a document type is declared: a document
  that names no sideloadable types MUST behave identically to today once its declaration is updated,
  and MUST require no change to the code that reads or assembles it beyond that one declaration.
- **FR-017**: A document that names no sideloadable types MUST continue to expose its sideload
  member in the existing untyped form, since a document's sideloadable set is not always known where
  the document is declared. A document that does name them MUST expose the typed view only; the
  untyped view MUST NOT also be available on it.
- **FR-018**: This feature MUST NOT introduce a dependency into the core wire-model package, which
  takes no dependencies.
- **FR-019**: The declaration MUST be readable by other tooling in this project, so that the API
  description work tracked separately can report the declared types without a second declaration
  being invented for it.
- **FR-020**: Nothing in this feature may add a sideload member to documents that cannot carry one.
- **FR-021**: The typed declaration MUST be delivered by changing the existing document forms rather
  than by adding a parallel family alongside them. There MUST be exactly one way to express a
  document that sideloads, and no second document family expressing the same thing.
- **FR-022**: The migration for a document that names no sideloadable types MUST be mechanical — a
  uniform edit an author can apply without deciding anything per call site — and MUST be documented
  as a migration note stating what changed and what to write instead.
- **FR-023**: The break MUST be published as a breaking change through the project's existing
  release process, so that consumers of the published packages are told rather than surprised.

### Key Entities

- **Sideload member**: The document member holding resources related to the primary data. Per the
  specification, "an array of resource objects that are related to the primary data and/or each
  other". A single flat list on the wire, deliberately holding several resource types at once.
- **Sideloadable type declaration**: An author's statement of which resource types a given document's
  sideload member may carry. Optional; absent on every document that exists today.
- **Sideloaded resource**: One entry in the sideload member — a full resource object, not merely an
  identifier, carrying everything a resource appearing as primary data carries.
- **Undeclared sideloaded resource**: A sideloaded resource whose type the reader's declaration does
  not name. Dropped on read, and absent when the document is written back.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Reading an attribute from a sideloaded resource requires zero runtime type tests and
  zero unwrapping steps, down from one of each per resource type today.
- **SC-002**: A misspelled attribute or resource type in code that reads a sideloaded resource is
  reported before the program runs, in 100% of cases where the type was declared.
- **SC-003**: Zero changes to serialized output: every existing test covering the wire format passes
  unmodified, and a document assembled the new way is byte-identical to the same document assembled
  the existing way.
- **SC-004**: Migrating a document that names no sideloadable types takes exactly one edit to its
  declaration and zero edits to the code that reads or assembles it, with no behavioural change.
- **SC-005**: 100% of sideloaded resources whose type the declaration names survive a parse-then-write
  round trip, and 100% of those it does not name are absent from the output — the drop is total and
  predictable, never partial.
- **SC-006**: A developer can declare their sideloadable types in one place, and no second
  declaration is needed for the same information anywhere else in the project.
- **SC-007**: The number of resource types a single document may declare as sideloadable is not
  capped at one; a document sideloading several types is expressible.
- **SC-008**: Exactly one document family expresses a document that sideloads — a reader looking for
  how to declare one finds a single answer, not a choice between an old and a new way.
- **SC-009**: A consumer upgrading across this change can identify what broke and what to write
  instead from the release notes alone, without reading the library's source or its commit history.

## Assumptions

- **"Strongly typed" means reachable without a cast.** The originating issue lists three possible
  designs and deliberately leaves the choice open. This specification takes the request at its word:
  only an outcome where the developer reaches a sideloaded resource's attributes without a runtime
  type test satisfies it. The two cheaper designs in the issue — convenience helpers over the flat
  list, and a declaration that names types without typing the member — are therefore assumed
  insufficient, and the choice of mechanism is left to planning rather than fixed here.
- **The reading side is largely built.** Sideloaded resources are already reconstructed in their
  declared shapes when the resource type mapping is supplied, and only the static view of them is
  lost. Verified at `libs/JsonApiLite/Serialization/ResourceConverter.cs:35-41`. The gap is assumed
  to be expressing what is already known, not discovering it.
- **The wire format is settled and correct.** It is pinned by existing tests, and this feature is
  assumed to be a change to what a developer writes in their own code, never to what an endpoint
  sends. Any proposal that alters serialized output is out of scope by definition.
- **Heterogeneity is a requirement, not an obstacle.** The specification defines the sideload member
  as holding resources related to the primary data "and/or each other", so a design that permits only
  one resource type would be non-conformant regardless of how convenient it was.
- **Declaring which types may be sideloaded stays optional; the document forms themselves do not.**
  No author is assumed to be forced to enumerate their sideloadable types — the untyped form remains
  permanently available for documents that name none, and is not deprecated by this feature. What is
  not optional is the change to the document forms, which every consumer takes whether or not they
  declare anything. The cost of a breaking change was accepted deliberately, in exchange for there
  being one way to express a document rather than an old way and a new one.
- **A pre-1.0 preview is the cheapest moment for this break.** The sample already consumes published
  preview packages, so the consumer population is assumed small enough that a single well-documented
  break costs less than carrying two parallel document families indefinitely. If that assumption is
  wrong — if there are consumers who cannot migrate — the additive option reopens, and this is the
  decision to revisit first.
- **The API description work is a consumer of this, not part of it.** Describing the sideload member
  in the published API description is tracked separately, and this feature is assumed to supply the
  declaration that work will read rather than to deliver the description itself.
- **Evidence is behavioural.** Compiling is not evidence. Each story is assumed to be demonstrated by
  reading a document back and by comparing serialized output, as the project's existing tests do.

## Dependencies

- **This feature requires changing the core wire-model package**, which the in-flight
  `001-document-envelope-schemas` specification forbids at FR-016 ("This feature MUST NOT require a
  change to the core wire-model package"). That constraint was written for a description-only
  feature and does not bind this one, but the two specifications must be reconciled rather than left
  contradicting each other, since both concern the same document member.
- **The API description work (`001-document-envelope-schemas`, Story 3) consumes the declaration this
  feature defines.** Delivering that story before this one would mean inventing a second declaration
  surface for the same information; the ordering should be settled deliberately.
- **The sample application consumes published packages rather than local source**, so demonstrating
  an outcome against the sample requires either a published preview package or a temporary local
  reference. This must be accounted for when evidence is gathered. It also means the sample is
  itself a consumer of the breaking change and must be migrated as part of delivering it.
- **The release carries a breaking change**, so the version bump and the migration note are part of
  this feature's delivery rather than follow-up work. The project computes versions from commit
  history rather than by hand, so the break must be recorded in the commit that makes it.
