# Feature Specification: OpenAPI Envelope Schemas

**Feature Branch**: `003-openapi-envelope-schemas`
**Created**: 2026-07-29
**Status**: Draft
**Input**: User description: "here is a specification for new feature https://github.com/radekwojpl2/Simple.JsonApi/issues/8"

## User Scenarios & Testing *(mandatory)*

An endpoint built with this library sends a complete JSON:API document: primary data, plus the
envelope members `links`, `meta` and `included` when it has them. The published description stops at
`data`. Everything else the endpoint actually sends is undescribed, so a consumer reading the
description — a person or a client generator — cannot see it.

Since the originating issue was written, the library gained a way for an author to declare which
resource types a document may sideload. The description generator does not recognise a document
that uses it, and rejects the annotation outright: the project's own sample no longer starts. So the
gap is no longer only that the envelope is undescribed — declaring the very thing the issue asked
for now breaks the description.

The people affected are the API's consumers, who read the description or generate a client from it,
and the API's author, who declares a page shape and a sideload shape and expects both to be
published.

### User Story 1 - An endpoint that declares its sideloadable types can be described (Priority: P1)

An author declares which resource types their endpoint may sideload — the declaration the
originating issue asked for, which now exists. Annotating that endpoint's response is rejected, and
the application fails to start. This story makes the description generator accept a document that
declares its sideload shape, describing it no worse than the same document without a declaration.

**Why this priority**: Nothing else in this feature can be observed until it holds. An endpoint that
declares its sideloadable types cannot be described at all today, and the application carrying it
does not start, so every remaining story is unverifiable on that endpoint. It is also a regression
rather than a gap: the same endpoint was describable before the declaration existed.

**Independent Test**: Annotate an endpoint whose response declares its sideloadable resource types,
start the application, and read the published description. The application starts and the endpoint's
response is described exactly as the equivalent undeclared document is. No envelope member need be
described for this to be verifiable.

**Acceptance Scenarios**:

1. **Given** an endpoint whose response document declares its sideloadable resource types, **When**
   the application starts, **Then** it starts, and the annotation is accepted rather than rejected.
2. **Given** such an endpoint, **When** the published description is read, **Then** its primary data,
   resource objects and relationships are described identically to the same document with no
   sideload declaration.
3. **Given** an endpoint whose response declares no sideloadable types, **When** the published
   description is read, **Then** it is unchanged from today.
4. **Given** a document form that the description generator genuinely does not understand, **When**
   it is annotated, **Then** the failure still names the offending type and the forms that are
   accepted, as it does today.

---

### User Story 2 - A paged list endpoint publishes its page counts (Priority: P2)

An endpoint author declares the shape of the metadata their list endpoint returns — page totals,
counts, whatever the endpoint decided to report. That declaration is accepted and then discarded, so
it never reaches the published description. This story makes the declared shape appear in the
description as an object with its members named and typed.

**Why this priority**: It is the cheapest of the three envelope members and the only one where the
author has already stated the answer — the shape is declared at the endpoint and thrown away, so
nothing new needs inventing. On its own it closes the visible half of the gap on the project's own
sample collection endpoint, which is what the originating issue says is worth doing alone.

**Independent Test**: Read the published description for the sample's contact-collection endpoint and
confirm the response envelope describes a metadata object whose members match the declared shape,
each with its type. Verifiable without any link or sideload member being described.

**Acceptance Scenarios**:

1. **Given** a collection endpoint that declares a metadata shape with a total count and a page
   count, **When** the published description is read, **Then** the response envelope contains a
   metadata member describing both, each with its type.
2. **Given** an endpoint that declares no metadata shape of its own, **When** the published
   description is read, **Then** the response envelope either omits the metadata member or describes
   it as an object of unconstrained members, and in neither case reports members the endpoint cannot
   send.
3. **Given** a declared metadata shape containing a nested object, a list, or an enumerated value,
   **When** the published description is read, **Then** those members are described to the same depth
   and with the same naming and enumerated-value conventions as the endpoint's other described
   content.
4. **Given** an endpoint declaring a metadata shape, **When** its response is compared against the
   published description, **Then** every metadata member the endpoint sends is described, and no
   described member is absent from what the endpoint can send.

---

### User Story 3 - A paged list endpoint is visibly paged (Priority: P3)

The envelope's link members — the ones saying where this page is, where the first, previous, next and
last pages are, and what a relationship points at — are absent from the description. A consumer
cannot tell that a paged endpoint pages, and a generated client has no member to follow. This story
describes the link members on the document kinds that can carry them.

**Why this priority**: It is what makes a list endpoint usable without reading prose documentation,
but unlike Story 2 nothing about it is declared per endpoint — the set of link members is fixed by
the JSON:API specification, so it is described once and applied everywhere. It ranks below Story 2
because it must be written out by hand rather than derived from a declaration.

**Independent Test**: Read the published description for the sample's contact-collection endpoint and
confirm the response envelope describes the link members, each accepting either a plain URL or a URL
carrying its own metadata. Verifiable whether or not Story 2 has been delivered.

**Acceptance Scenarios**:

1. **Given** a collection endpoint that returns pagination links, **When** the published description
   is read, **Then** the response envelope describes the link members the specification defines for a
   document, and each is described as a URL.
2. **Given** a link that carries metadata alongside its URL, **When** the published description is
   read, **Then** the link member is described as accepting either a plain URL or an object carrying
   a URL and its metadata.
3. **Given** an endpoint that returns no links at all, **When** the published description is read,
   **Then** no link member is stated to be mandatory.
4. **Given** the published description, **When** it is validated against a response the endpoint
   actually produced, **Then** the response conforms.

---

### User Story 4 - Sideloaded resources are described from the declaration (Priority: P4)

An endpoint that sideloads related resources returns them in the envelope's sideload member. That
member holds resources of more than one type by design. The endpoint's author has now stated which
types may appear there, but the description does not report them, so a consumer still cannot know
what to expect. This story describes the member from the declaration the author already made.

**Why this priority**: It is the most visible payoff of the declaration, but it is the only story
that needs the declaration's contents to be legible to the description generator rather than merely
accepted by it, so it costs the most. It is also the least broadly applicable: the sample sideloads
on one endpoint and only when asked. Stories 1 to 3 leave the sample's list endpoint fully described
without it.

**Independent Test**: Take the endpoint that declares its sideloadable types, read the published
description, and confirm the envelope describes a list whose entries are constrained to the declared
types, each described as a full resource object. Verifiable independently of Stories 2 and 3, but
not of Story 1.

**Acceptance Scenarios**:

1. **Given** an endpoint whose response declares one or more sideloadable resource types, **When** the
   published description is read, **Then** the response envelope describes a list whose entries are
   constrained to those declared types, each described as a resource object with its attributes and
   relationships.
2. **Given** an endpoint that declares no sideloadable types, **When** the published description is
   read, **Then** the envelope either omits the sideload member or describes it as a list of
   unconstrained resource objects, and does not claim types the endpoint cannot return.
3. **Given** an endpoint declaring a sideloadable type, **When** a response that sideloads that type
   is validated against the published description, **Then** the response conforms.
4. **Given** an endpoint declaring several sideloadable types, **When** the published description is
   read, **Then** every declared type appears, and a consumer can tell which described resource
   object corresponds to which type.

---

### Edge Cases

- **An endpoint declares no metadata shape.** Most do not. The description must not invent members,
  and must not become invalid by declaring an object with nothing in it.
- **A declared metadata shape has no readable members.** The description must degrade to an
  unconstrained object rather than emit an empty one.
- **A declared metadata shape refers to itself**, directly or through another type. The description
  must terminate rather than recurse forever, exactly as the existing described content already does.
- **A declared sideload shape names no types at all.** It must be described exactly as no declaration
  is, never as a list that can hold nothing.
- **A sideloadable type is also the primary data's type.** A document may sideload resources of the
  type it returns as primary data. The description must accommodate this without the two colliding.
- **Error responses.** An error document carries envelope members, but has no primary data, so the
  pagination links, the `related` link and the sideload member do not apply to it. What is described
  for a data document must not contradict what is described for an error document.
- **Request bodies.** A request document is described from the same declarations as a response, so a
  request schema could pick up envelope members as a side effect. It must not.
- **A member is described as mandatory that an endpoint sometimes omits.** Every envelope member here
  is optional on the wire; describing one as required would make a valid response fail validation.
- **An endpoint sends an envelope member no declaration covers.** The description must continue to
  permit unstated members rather than reject them, as it does today.
- **A document form the generator does not understand.** Widening what is accepted must not turn a
  genuinely unsupported type into a silently empty schema; it must still fail loudly.
- **A link member the specification defines but the library cannot send.** The description must
  describe what an endpoint can actually produce, not everything the specification permits.

## Clarifications

### Session 2026-07-29

- Q: Should the description describe `describedby`, which the JSON:API specification defines as a
  document link member? → A: No — drop it. The library's links object has no such member
  (`libs/JsonApiLite/Documents/Links.cs:7-16` declares `Self`, `Related`, `About`, `First`, `Prev`,
  `Next`, `Last`), so no endpoint built on it can send one. FR-008 and FR-021 were written the
  opposite way — inherited from the superseded `001-document-envelope-schemas`, whose author had not
  checked the type — and are revised above, with FR-008a added to state the rule generally.
  - **Why this is the right way round**: describing a member the library cannot produce is a lie the
    tests cannot catch. Every envelope member is optional, so an absent `describedby` never fails
    validation — SC-005 would pass with the description claiming a member that can never appear.
  - **Rejected alternative**: adding a `DescribedBy` member to the core links object. That is a
    wire-model change, which FR-023 confines to making the sideload declaration legible, and it would
    have added surface to the zero-dependency package to satisfy a description requirement rather
    than a consumer's need. If an endpoint author later wants to send `describedby`, that is a
    separate feature against the wire model, and the description follows it rather than leading it.
  - **Accepted cost**: a consumer cannot discover a description document link from the published
    description. Nothing in the project sends one today, so the cost is currently zero.

## Requirements *(mandatory)*

### Functional Requirements

**Accepting a declared sideload shape (Story 1)**

- **FR-001**: The description generator MUST accept a response document that declares its
  sideloadable resource types. Annotating such an endpoint MUST NOT fail.
- **FR-002**: A document that declares its sideloadable types MUST have its primary data, resource
  objects and relationships described identically to the same document that declares none. The
  declaration MUST NOT degrade any part of the description that already worked.
- **FR-003**: A document type the generator genuinely does not support MUST still be rejected with a
  message naming the offending type and the forms that are accepted. Widening what is accepted MUST
  NOT be achieved by removing the check.

**Metadata (Story 2)**

- **FR-004**: When an endpoint declares the shape of its document metadata, the published description
  MUST describe that metadata in the endpoint's response schema as an object whose members are named
  and typed from the declared shape.
- **FR-005**: Declared metadata members MUST be described using the same member-naming and
  enumerated-value conventions the description already applies to an endpoint's other content, so the
  description and the wire cannot disagree.
- **FR-006**: Nested objects, lists and dictionaries inside a declared metadata shape MUST be
  described to the same depth as equivalent content elsewhere in the description, and MUST terminate
  on a self-referencing shape.
- **FR-007**: When an endpoint declares no metadata shape, the description MUST NOT state any
  metadata member names.

**Links (Story 3)**

- **FR-008**: The published description MUST describe the document link members the library can
  actually send, and the set MUST vary by document kind rather than being uniform:
  - `self` on every document kind.
  - The pagination links (`first`, `last`, `prev`, `next`) only on documents whose primary data is a
    collection — a resource collection or to-many linkage. [Spec: "Pagination links **MUST** appear in
    the links object that corresponds to a collection."]
  - `related` only on linkage documents, whose primary data represents a resource relationship.
    [Spec: "**related**: a related resource link when primary data represents a relationship."]
- **FR-008a**: The description MUST NOT describe a link member the library cannot produce. In
  particular `describedby`, which the JSON:API specification defines for a document, MUST NOT be
  described, because the library's links object has no such member. [Revised 2026-07-29 — FR-008 and
  FR-021 previously required `describedby`. See Clarifications.]
- **FR-009**: Each link member MUST be described as accepting either a plain URL or an object carrying
  a URL and its own metadata, matching what the library can send. [Spec: a link is "a string whose
  value is a URI-reference pointing to the link's target, a link object or `null` if the link does
  not exist."]
- **FR-010**: No link member may be described as mandatory.

**Sideloaded resources (Story 4)**

- **FR-011**: When a response document declares its sideloadable resource types, the description MUST
  describe the sideload member as a list constrained to those types, each entry described as a full
  resource object with its attributes and relationships.
- **FR-012**: The described sideload member MUST be a single flat list, as the specification requires:
  "In a compound document, all included resources **MUST** be represented as an array of resource
  objects in a top-level `included` member." The author's per-type declaration is how the types are
  named, not how they appear on the wire.
- **FR-013**: When no sideloadable types are declared, the description MUST NOT claim any specific
  sideloadable type.
- **FR-014**: The declared sideloadable types MUST be read from the declaration the author already
  makes on the document. No second declaration may be introduced for the same information.

**Applying to all four**

- **FR-015**: No envelope member introduced by this feature may be described as mandatory; all are
  optional on the wire.
- **FR-016**: The description MUST continue to permit envelope members it does not describe, so a
  document carrying an undescribed member remains valid.
- **FR-017**: Every response the project's sample application produces MUST validate against the
  description the sample publishes.
- **FR-018**: Existing described content — primary data, resource objects, relationships and error
  documents — MUST be unchanged except where an envelope member is added alongside it.
- **FR-019**: This feature MUST NOT change what any endpoint sends. It changes only what the
  description says, and what the description generator accepts. [Rationale: the wire behaviour is
  already correct and covered by tests; this is a description gap.]
- **FR-020**: The envelope members MUST be described on response schemas only. A request body schema
  MUST be unchanged — no metadata member, no link members, no sideload member — even when the
  request's document type carries a declared metadata shape.
- **FR-021**: An error document's response schema MUST describe a `links` member carrying `self`, and
  a metadata member described as an unconstrained object. It MUST NOT describe pagination links,
  `related`, or a sideload member. The metadata is unconstrained because the error document form is
  non-generic, so an author has no way to declare a shape for it. [Revised 2026-07-29 — this
  requirement previously also demanded `describedby`. See FR-008a and Clarifications.]
- **FR-022**: The sideload member MUST be described only on documents whose primary data is one or
  more resources. Linkage and error documents MUST NOT describe it. [Spec: the `included` member
  "only appears when the document contains a top-level `data` key"; those documents have no resource
  primary data to relate to.]
- **FR-023**: Any change this feature requires of the core wire-model package MUST be confined to
  making the existing sideload declaration legible to other tooling. It MUST NOT add a dependency to
  that package, and MUST NOT change how a document is written or read.

### Key Entities

- **Document envelope**: What wraps an endpoint's primary data — the members `links`, `meta` and
  `included` alongside `data`. All are optional; the specification defines the first two, the third
  holds resources related to the primary data.
- **Declared metadata shape**: The endpoint author's statement of what their document's metadata
  member contains. The specification reserves no metadata member names, so the shape is always the
  endpoint's own.
- **Declared sideload shape**: The author's statement of which resource types a document's sideload
  member may carry. Already expressible on the document; not yet legible to the description.
- **Link**: One entry in the links member. Either a plain URL, or a URL carrying its own metadata.
- **Published description**: The API description document the sample serves and that reader tools
  render. Reading it back is the acceptance evidence for every story here.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The sample application starts and publishes its description with every endpoint
  annotated, including the one that declares its sideloadable types — zero startup failures, down
  from a failure that stops the application today.
- **SC-002**: For the sample's paged collection endpoint, 100% of the envelope members it actually
  sends are described in the published description, excluding the version member the sample never
  sends.
- **SC-003**: A consumer reading the published description for a paged endpoint can identify how to
  reach the next page and how to read the total item count, without consulting source code or prose
  documentation.
- **SC-004**: A consumer reading the published description for the sample's sideloading endpoint can
  name every resource type that endpoint may sideload, without consulting source code.
- **SC-005**: Every response the sample application produces validates against the description the
  sample publishes, with zero failures.
- **SC-006**: Zero changes to what any endpoint sends: every existing test covering the wire format
  passes unmodified.
- **SC-007**: Zero endpoints require a declaration change to keep or improve their description. An
  endpoint that declares nothing new is described no worse than it is today, and an endpoint that has
  already declared its metadata or sideload shape gains the description of it without editing the
  endpoint.
- **SC-008**: An endpoint author publishes a metadata shape and a sideload shape by declaring each
  once, where they already declare the response — no second declaration, no configuration.

## Assumptions

- **Story 1 is a regression, and it is verified rather than inferred.** Running the sample
  (`dotnet run --project JsonApiPoc.Api`, against published preview `1.1.1-preview.10.44`) fails at
  startup:

  ```
  Unhandled exception. System.ArgumentException: 'JsonApiLite.ResourceDocument`4[JsonApiPoc.Api.ContactAttributes,JsonApiPoc.Api.ContactRelationships,JsonApiLite.Meta,JsonApiPoc.Api.ContactIncluded]' is not a JSON:API document the annotation understands — expected ResourceDocument<>, ResourceCollectionDocument<>, ToOneLinkageDocument, ToManyLinkageDocument, or ErrorDocument.
     at JsonApiLite.OpenApi.JsonApiBody.Describe(Type documentType)
     at JsonApiLite.JsonApiOpenApi.ProducesJsonApi[TDocument](RouteHandlerBuilder builder, Int32 statusCode)
     at Program.<Main>$(String[] args) in D:\git\JsonApiPoc\JsonApiPoc.Api\Program.cs:line 96
  ```

  The cause is that the set of accepted document forms names three arities per family and not the
  four-argument form (`libs/JsonApiLite.OpenApi/JsonApiBody.cs:48-60`).
- **The metadata gap is a plumbing gap, not a design gap.** The shape is declared at the endpoint and
  already walked correctly elsewhere in the description; it is simply not carried through. Verified at
  `libs/JsonApiLite.OpenApi/JsonApiBody.cs:87-94`, which reads `arguments[0]` and `arguments[1]` and
  never `arguments[2]`.
- **The envelope is a single line today.** `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs:32` is
  `return Object(new() { ["data"] = data }, ["data"]);` — so `links`, `meta` and `included` are absent
  by construction rather than by any conditional. This is not a validation failure: the schema never
  sets `additionalProperties`, so the extra members are permitted, just invisible.
- **Links cannot be derived from the shape of the types that carry them.** Those types write
  themselves to the wire through their own converters, so describing their internal structure would
  describe the wrong thing. The link members are therefore assumed to be written out by hand, as the
  error document's members already are — noted at
  `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs:135-136`.
- **A document whose metadata shape is not declared is described as an unconstrained object**, not
  omitted and not described with invented members. This keeps a caller from being told a member does
  not exist when it may.
- **The declaration is meant to be readable by this feature.** `002-typed-included-resources` FR-019
  requires it: "The declaration MUST be readable by other tooling in this project, so that the API
  description work tracked separately can report the declared types without a second declaration
  being invented for it." Story 4 is that consumer. The map from a wire type name to its declared
  member is currently internal to the core package
  (`libs/JsonApiLite/Serialization/IncludedShape.cs:30`), so satisfying FR-014 without inventing a
  second declaration is assumed to require exposing what already exists — which is why FR-023 permits
  a confined core change rather than forbidding one outright.
- **The version member (`jsonapi`) is out of scope**, per the originating issue: the sample never
  sends it.
- **Applying query parameters — filtering, sorting, paging as inputs — is out of scope.** This feature
  describes what an endpoint returns, not what it accepts as query parameters; that is tracked
  separately on the project roadmap.
- **Delivery is incremental and each story stands alone**, except that Stories 2 to 4 cannot be
  observed on the sample's sideloading endpoint until Story 1 holds. Stories 2 and 3 are observable
  on the collection endpoint immediately.
- **Evidence is behavioural.** Compiling is not evidence. Reading the published description back, and
  validating a real response against it, is the acceptance evidence for every story here.

## Dependencies

- **This feature supersedes `001-document-envelope-schemas`**, which specified the same issue before
  the sideload declaration existed. That specification's FR-016 — "This feature MUST NOT require a
  change to the core wire-model package" — was written when its Story 3 had no declaration to read,
  and is contradicted by `002-typed-included-resources`, whose own Dependencies section records that
  the two "must be reconciled rather than left contradicting each other". FR-023 above is that
  reconciliation. `001` must be retired rather than left to contradict this specification.
- **Story 1 and Story 4 depend on `002-typed-included-resources`**, which introduced the declaration
  and, in doing so, the document form the description generator rejects. Story 1 is not optional
  follow-up work for that feature: the sample does not start without it.
- **The sample application consumes the published packages rather than the local source**
  (`JsonApiPoc.Api/JsonApiPoc.Api.csproj:13-14`, currently `1.1.1-preview.10.44`), so verifying an
  outcome against the sample requires either a published preview package or a temporary local
  reference. This must be accounted for when evidence is gathered.
- **The sample is mid-migration across the `002` breaking change**: its sideload declaration still
  carries the member that commit `ddbb34b` removed from the core. Whatever state that migration is in
  must be settled before the sample can serve as evidence for any story here.
- The description is produced by the application's own description generator; this feature extends
  what that generator is told and what it accepts, and does not introduce a second source of
  descriptions.
