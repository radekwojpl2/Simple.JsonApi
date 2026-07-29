# Feature Specification: Document Envelope Schemas

**Feature Branch**: `001-document-envelope-schemas`
**Created**: 2026-07-27
**Status**: **Superseded by [`003-openapi-envelope-schemas`](../003-openapi-envelope-schemas/spec.md)** (2026-07-29)
**Input**: User description: "Read this for specification https://github.com/radekwojpl2/Simple.JsonApi/issues/8"

> ## Superseded — do not plan or implement from this document
>
> This specification covers https://github.com/radekwojpl2/Simple.JsonApi/issues/8 and was written on
> 2026-07-27, before `002-typed-included-resources` existed. Two things have since made it wrong
> rather than merely incomplete:
>
> 1. **FR-016 forbids what the feature now requires.** It states "This feature MUST NOT require a
>    change to the core wire-model package". Its Story 3 was written when no declaration of
>    sideloadable types existed; `002` then added one, and the map from a wire type name to its
>    declared member is internal to the core package
>    (`libs/JsonApiLite/Serialization/IncludedShape.cs:30`). Describing the sideload member now
>    requires exposing it. `002`'s own Dependencies section anticipated this, recording that the two
>    specifications "must be reconciled rather than left contradicting each other".
> 2. **It does not know the description generator is broken.** `002` shipped the four-argument
>    document form and the sample adopted it; the annotation rejects it and the sample no longer
>    starts. That outranks all three of this document's stories and is `003`'s P1.
>
> `003` also drops `describedby` from the link members, which this document requires at FR-005 and
> FR-019: `libs/JsonApiLite/Documents/Links.cs:7-16` has no such member, so no endpoint built on
> this library can send one.
>
> Kept rather than deleted because `003` inherits four of its clarifications — responses only,
> error-document envelope members, per-kind link sets, and Story 3 being in scope — and the record of
> when and why those were decided lives here.

## User Scenarios & Testing *(mandatory)*

An endpoint built with this library already sends a complete JSON:API document: primary data, plus
the envelope members `links`, `meta` and `included` when it has them. The generated API description
stops at `data`. Everything else the endpoint actually sends is undescribed, so anyone reading the
description — a person or a client generator — cannot see it.

The people affected are the API's consumers (who read the description or generate a client from it)
and the API's author (who declared a page shape and expects it to be published).

### User Story 1 - A paged list endpoint publishes its page counts (Priority: P1)

An endpoint author declares the shape of the metadata their list endpoint returns — page totals,
counts, whatever the endpoint decided to report. Today that declaration is accepted and then
discarded, so it never reaches the published description. This story makes the declared shape appear
in the description as a described object with its members named and typed.

**Why this priority**: It is the cheapest of the three and the only one where the author has already
stated the answer — the shape is declared at the endpoint and simply thrown away. Nothing new needs
to be invented for it, and on its own it closes the visible half of the gap on the project's own
sample collection endpoint.

**Independent Test**: Read the published description for the sample's contact-collection endpoint
and confirm the response envelope describes a metadata object whose members match the shape the
endpoint declared, with each member's type stated. No other envelope member needs to be present for
this to be verifiable.

**Acceptance Scenarios**:

1. **Given** a collection endpoint that declares a metadata shape with a total count and a page
   count, **When** the published description is read, **Then** the response envelope contains a
   metadata member describing both, each with its type.
2. **Given** an endpoint that declares no metadata shape of its own, **When** the published
   description is read, **Then** the response envelope either omits the metadata member or describes
   it as an object of unconstrained members, and in neither case reports members the endpoint cannot
   send.
3. **Given** a declared metadata shape containing a nested object, a list, or an enumerated value,
   **When** the published description is read, **Then** those members are described to the same
   depth and with the same naming and enumerated-value conventions as the endpoint's other described
   content.
4. **Given** an endpoint declaring a metadata shape, **When** the endpoint's response is compared
   against the published description, **Then** every metadata member the endpoint sends is described
   and no described member is absent from what the endpoint can send.

---

### User Story 2 - A paged list endpoint is visibly paged (Priority: P2)

The envelope's link members — the ones that say where this page is, where the first, previous, next
and last pages are, and where an error is documented — are absent from the description. A consumer
therefore cannot tell that a paged endpoint pages, and a generated client has no member to follow.
This story describes the link members on document envelopes that can carry them.

**Why this priority**: It is what makes a list endpoint usable without reading its prose
documentation, but unlike Story 1 nothing about it is declared per-endpoint — the set of link
members is fixed by the JSON:API specification, so it can be described once and applied everywhere.
It is ranked second because it must be written out by hand rather than derived from a declaration.

**Independent Test**: Read the published description for the sample's contact-collection endpoint and
confirm the response envelope describes the link members, each accepting either a plain URL or a URL
carrying its own metadata. Verifiable whether or not Story 1 has been delivered.

**Acceptance Scenarios**:

1. **Given** a collection endpoint that returns pagination links, **When** the published description
   is read, **Then** the response envelope describes the link members the specification defines for
   a document, and each is described as a URL.
2. **Given** a link that carries metadata alongside its URL, **When** the published description is
   read, **Then** the link member is described as accepting either a plain URL or an object carrying
   a URL and its metadata.
3. **Given** an endpoint that returns no links at all, **When** the published description is read,
   **Then** no link member is stated to be mandatory.
4. **Given** the published description, **When** it is validated against a response the endpoint
   actually produced, **Then** the response conforms.

---

### User Story 3 - Sideloaded resources are declared and described (Priority: P3)

An endpoint that sideloads related resources returns them in the envelope's `included` member. That
member holds resources of more than one type by design, and no endpoint declaration today states
which types may appear there — so the description cannot report them, and a consumer cannot know
what to expect. This story lets an endpoint author declare the resource types their endpoint may
sideload, and describes the member from that declaration.

**Why this priority**: It is the only one of the three that requires new declaration surface for
authors to learn and for the project to support indefinitely. It is also the least visible: the
sample sideloads on one endpoint and only when asked. Delivering Stories 1 and 2 without it still
leaves the sample's list endpoint fully described.

**Independent Test**: Take an endpoint that sideloads a related resource, declare the type it may
sideload, then read the published description and confirm the envelope describes a list of resources
constrained to the declared types. Verifiable independently of Stories 1 and 2.

**Acceptance Scenarios**:

1. **Given** an endpoint that declares one or more sideloadable resource types, **When** the
   published description is read, **Then** the response envelope describes a list whose entries are
   constrained to those declared types, each described as a resource object.
2. **Given** an endpoint that declares no sideloadable types, **When** the published description is
   read, **Then** the envelope either omits the sideload member or describes it as a list of
   unconstrained resource objects, and does not claim types the endpoint cannot return.
3. **Given** an endpoint declaring a sideloadable type, **When** a response that sideloads that type
   is validated against the published description, **Then** the response conforms.

---

### Edge Cases

- **An endpoint declares no metadata shape.** Most endpoints do not. The description must not invent
  members, and must not become invalid by declaring an object with nothing in it.
- **A declared metadata shape has no readable members.** The description must degrade to an
  unconstrained object rather than emit an empty one.
- **A declared metadata shape refers to itself**, directly or through another type. The description
  must terminate rather than recurse forever, exactly as the existing described content already
  does.
- **Error responses.** An error document carries envelope members too, but has no primary data, so
  the pagination and `related` links and the sideload member do not apply to it. Whatever is
  described for a data document must not contradict what is described for an error document.
- **Request bodies.** A request document is described by the same declarations as a response, so a
  request schema could pick up envelope members as a side effect. It must not: request schemas are
  unchanged by this feature, and a document type carrying a declared metadata shape must describe
  that shape on the response only.
- **A member is described as mandatory that an endpoint sometimes omits.** Every envelope member here
  is optional on the wire; describing one as required would make a valid response fail validation.
- **An endpoint sends an envelope member no declaration covers.** The description must continue to
  permit unstated members rather than reject them, as it does today.

## Clarifications

### Session 2026-07-27

- Q: Is Story 3 (sideloaded resources) in scope for this feature, or deferred? → A: In scope as P3 — the new declaration surface is designed and shipped here.
- Q: Do the envelope members belong on request body descriptions as well as responses, or responses only? → A: Responses only — request schemas are unchanged by this feature.
- Q: Which link members are described on which document kinds? → A: Per-kind sets — `self`/`describedby` everywhere, pagination links only where primary data is a collection, `related` only on linkage documents.
- Q: Do error documents gain envelope members, and which? → A: Yes — `links` with `self`/`describedby` plus an unconstrained metadata member; no pagination, no `related`, no sideload member.

## Requirements *(mandatory)*

### Functional Requirements

**Metadata (Story 1)**

- **FR-001**: When an endpoint declares the shape of its document metadata, the published
  description MUST describe that metadata in the endpoint's response schema as an object whose
  members are named and typed from the declared shape.
- **FR-002**: Declared metadata members MUST be described using the same member-naming and
  enumerated-value conventions the description already applies to an endpoint's other content, so
  that the description and the wire cannot disagree.
- **FR-003**: Nested objects, lists and dictionaries inside a declared metadata shape MUST be
  described to the same depth as equivalent content elsewhere in the description, and MUST terminate
  on a self-referencing shape.
- **FR-004**: When an endpoint declares no metadata shape, the description MUST NOT state any
  metadata member names.

**Links (Story 2)**

- **FR-005**: The published description MUST describe the document link members defined by the
  JSON:API specification, and the set MUST vary by document kind rather than being uniform:
  - `self` and `describedby` on every document kind.
  - The pagination links (`first`, `last`, `prev`, `next`) only on documents whose primary data is a
    collection — a resource collection or to-many linkage. [Spec: "Pagination links MUST appear in
    the links object that corresponds to a collection."]
  - `related` only on linkage documents, whose primary data represents a resource relationship.
    [Spec: "`related`: a related resource link when the primary data represents a resource
    relationship."]
- **FR-006**: Each link member MUST be described as accepting either a plain URL or an object
  carrying a URL and its own metadata, matching what the library can send.
- **FR-007**: No link member may be described as mandatory.

**Sideloaded resources (Story 3)**

- **FR-008**: An endpoint author MUST be able to declare which resource types their endpoint may
  sideload.
- **FR-009**: When such a declaration is present, the description MUST describe the sideload member
  as a list constrained to the declared types, each described as a full resource object.
- **FR-010**: When no such declaration is present, the description MUST NOT claim any specific
  sideloadable type.

**Applying to all three**

- **FR-011**: No envelope member introduced by this feature may be described as mandatory; all are
  optional on the wire.
- **FR-012**: The description MUST continue to permit envelope members it does not describe, so that
  a document carrying an undescribed member remains valid.
- **FR-013**: Every response the project's sample application produces MUST validate against the
  description the sample publishes.
- **FR-014**: Existing described content — primary data, resource objects, relationships and error
  documents — MUST be unchanged by this feature except where an envelope member is added alongside
  it.
- **FR-015**: This feature MUST NOT change what any endpoint sends. It changes only what the
  description says. [Rationale: the wire behaviour is already correct and covered by tests; this is a
  description gap.]
- **FR-016**: This feature MUST NOT require a change to the core wire-model package, which takes no
  dependencies and already serializes all three envelope members.
- **FR-019**: An error document's response schema MUST describe a `links` member carrying `self` and
  `describedby`, and a metadata member described as an unconstrained object. It MUST NOT describe
  pagination links, `related`, or a sideload member. The metadata is unconstrained because the error
  document type is non-generic, so an author has no way to declare a shape for it.
- **FR-020**: The sideload member MUST be described only on documents whose primary data is one or
  more resources. Linkage and error documents MUST NOT describe it. [Spec: `included` is "an array of
  resource objects that are related to the primary data"; those documents have no resource primary
  data to relate to.]

**Open scope questions**

- **FR-017**: The envelope members MUST be described on response schemas only. A request body schema
  MUST be unchanged by this feature — no metadata member, no link members, no sideload member — even
  when the request's document type carries a declared metadata shape.
- **FR-018**: All three stories are in scope for this feature, including Story 3. The new
  author-facing declaration surface Story 3 requires MUST be designed and shipped here, not deferred.
  Delivery remains incremental and story-ordered (P1 → P2 → P3), so Stories 1 and 2 may ship before
  Story 3 is complete.

### Key Entities

- **Document envelope**: What wraps an endpoint's primary data — the members `links`, `meta` and
  `included` alongside `data`. All are optional; the specification defines the first two, the third
  holds resources related to the primary data.
- **Declared metadata shape**: The endpoint author's statement of what their document's metadata
  member contains. The specification reserves no metadata member names, so the shape is always the
  endpoint's own.
- **Link**: One entry in the links member. Either a plain URL, or a URL carrying its own metadata.
- **Sideloadable resource type**: A resource type an endpoint may return in its sideload member.
  Heterogeneous by design — one endpoint may return several types.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For the sample's paged collection endpoint, 100% of the envelope members it actually
  sends are described in the published description, excluding the version member the sample never
  sends.
- **SC-002**: A consumer reading the published description for a paged endpoint can identify how to
  reach the next page and how to read the total item count, without consulting source code or prose
  documentation.
- **SC-003**: Every response the sample application produces validates against the description the
  sample publishes, with zero failures.
- **SC-004**: Zero changes to what any endpoint sends: every existing test covering the wire format
  passes unmodified.
- **SC-005**: Zero endpoints require a declaration change to keep their current description. An
  endpoint that declares nothing new is described no worse than it is today.
- **SC-006**: An endpoint author can publish a metadata shape by declaring it once, at the point
  they already declare the response — no second declaration, no configuration.

## Assumptions

- **The metadata gap is a plumbing gap, not a design gap.** The shape is already declared at the
  endpoint and already walked correctly elsewhere in the description; it is simply not carried
  through. Verified at `libs/JsonApiLite.OpenApi/JsonApiBody.cs:87-94`, which reads the first two
  declared types and never the third.
- **Links cannot be derived from the shape of the types that carry them.** Those types write
  themselves to the wire through their own conversion rules, so describing their internal structure
  would describe the wrong thing. The link members are therefore assumed to be written out by hand,
  as the error document's members already are — noted at
  `libs/JsonApiLite.OpenApi/JsonApiSchemaBuilder.cs:135-136`.
- **A document whose metadata shape is not declared is described as an unconstrained object**, not
  omitted and not described with invented members. This keeps a caller from being told a member does
  not exist when it may.
- **The version member (`jsonapi`) is out of scope**, per the originating issue: the sample never
  sends it.
- **Applying query parameters — filtering, sorting, paging as inputs — is out of scope.** This
  feature describes what an endpoint returns, not what it accepts as query parameters; that is
  tracked separately on the project roadmap.
- **Delivery is incremental and each story stands alone.** Story 1 alone is worth shipping; the
  originating issue states as much.
- **"Published description" means the API description document the sample serves and that reader
  tools render.** Reading that document back is the acceptance evidence for every story here —
  compiling is not evidence.

## Dependencies

- The description is produced by the application's own description generator; this feature extends
  what that generator is told, and does not introduce a second source of descriptions.
- The project's sample application consumes the published packages rather than the local source, so
  verifying an outcome against the sample requires either a published package or a temporary local
  reference. This must be accounted for when evidence is gathered.
