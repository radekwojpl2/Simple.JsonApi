# Implementation Plan: OpenAPI Envelope Schemas

**Branch**: `003-openapi-envelope-schemas` | **Date**: 2026-07-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-openapi-envelope-schemas/spec.md`

## Summary

The published API description stops at `data`: `links`, `meta` and `included` are absent from every
document schema, because the envelope is one line — `JsonApiSchemaBuilder.cs:32`. Worse, since `002`
shipped the four-argument document form, annotating a document that declares its sideloadable types
throws and the sample no longer starts.

The approach, in priority order:

1. **Resolve document types through their inheritance chain** rather than by enumerating arities.
   The families are a chain rooted at the four-argument form, so one walk handles every arity and
   the crash disappears (research R1).
2. **Carry the third type argument through** and run the existing schema walker over it, guarding
   the case where it derives from `Meta` and its wire form is converter-owned (R2).
3. **Write the link members out by hand**, per document kind, from what `Links` can actually carry
   (R3).
4. **Publish a minimal accessor on the core package** so the description can read the sideload
   declaration the author already made, and describe `included` from it (R4).

Evidence comes from a new test project for the OpenAPI package — which has none today — plus the
sample's `/openapi/v1.json` (R5).

## Technical Context

**Language/Version**: C# / .NET — `net8.0` and `net10.0` for the core library, `net10.0` only for the
OpenAPI package and its new tests
**Primary Dependencies**: core takes **none** (Principle III); OpenAPI package uses
`Microsoft.AspNetCore.OpenApi` 10.0.1 and `Microsoft.OpenApi` 2.8.0, both already referenced
**Storage**: N/A
**Testing**: xunit 2.9.3, `Microsoft.NET.Test.Sdk` 17.14.1 — matching `JsonApiLite.Tests`
**Target Platform**: cross-platform library; the sample is an ASP.NET Core minimal API
**Project Type**: library (wire model) plus a companion description package and a sample consumer
**Performance Goals**: schema construction happens once at startup per annotated document type; the
sideload declaration is already cached per closed type (`IncludedShape.cs:32`) and this feature adds
no per-request work
**Constraints**: no change to serialized output (FR-019, SC-006); no new core dependency (FR-023,
Principle III); no envelope member described as required (FR-015); request schemas untouched (FR-020)
**Scale/Scope**: two source files in the OpenAPI package (`JsonApiBody.cs`,
`JsonApiSchemaBuilder.cs`), one new public accessor in the core package, one new test project

No `NEEDS CLARIFICATION` items remain. Two open decisions are recorded under *Open questions* below;
neither blocks implementation of Stories 1, 2 and 4.

## Constitution Check

*GATE: checked before Phase 0 and re-checked after Phase 1 design. Both passes below.*

| Principle | Gate | Verdict |
| --- | --- | --- |
| **I. The Specification Decides** | Every described member traces to a spec clause, quoted verbatim | **PASS.** Link placement follows "Pagination links **MUST** appear in the links object that corresponds to a collection" and "**related**: a related resource link when primary data represents a relationship". The flat `included` array follows "In a compound document, all included resources **MUST** be represented as an array of resource objects in a top-level `included` member." |
| **II. Verify or Say You Did Not** | Claims cited or marked `notSure` | **PASS.** The central claim — the sample crashes — is shown as run output, not inferred. Research R1–R5 cite `file:line` throughout. One `notSure` is recorded in R3, about document-level `about`. |
| **III. Core Takes No Dependencies** | `libs/JsonApiLite` keeps zero package references | **PASS.** The one core addition is a reflection accessor over types already present. Both TFMs keep building. Framework-dependent work stays in the companion package, where it already lives. |
| **IV. Model the Wire, Nothing Else** | No HTTP, validation, persistence or framework creep | **PASS.** This describes documents someone else's endpoints send; it generates no endpoints and validates nothing. ROADMAP entry advanced: the OpenAPI description gap tracked as issue #8. |
| **V. House Style** | No ternary returns; comments justify; public API carries XML docs | **PASS by construction, enforced at review.** The new public accessor needs XML docs saying what it is *for*. The link-set selection is a `switch`/`if` chain, not nested `? :`. |

**Post-Phase-1 re-check**: no gate moved. The design added one public member to the core package,
which Principle III permits (it is a dependency ban, not an API freeze), and one test project, which
the verification gates require rather than forbid.

**Complexity Tracking**: not required — no violations to justify.

### One gate that needs stating plainly

The constitution requires "Tests run against both `net8.0` and `net10.0`. A change that passes on one
TFM only is a failing change." The new OpenAPI test project **cannot** target `net8.0`: the package
under test is `net10.0`-only, for the reason recorded in its csproj — "There is nothing to
multi-target back to net8.0." This is not a violation of the principle's intent (the core library's
tests still run on both), but it is the first test project in the repository that does not
multi-target, and reviewers should know it is deliberate.

## Project Structure

### Documentation (this feature)

```text
specs/003-openapi-envelope-schemas/
├── plan.md              # This file
├── research.md          # Phase 0 — R1..R5 and the open items
├── data-model.md        # Phase 1 — Description extensions, the core accessor, schema fragments
├── quickstart.md        # Phase 1 — how to verify each story, with the reproduction first
├── contracts/
│   └── public-api.md    # Phase 1 — public surface, accepted types, emitted schemas
├── checklists/
│   └── requirements.md  # From /speckit-specify
└── tasks.md             # Phase 2 — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
libs/
├── JsonApiLite/                          # core wire model — net8.0, net10.0, zero dependencies
│   ├── Documents/
│   │   ├── ResourceDocument.cs           # read: the 4-arg base and its two convenience subtypes
│   │   ├── ResourceCollectionDocument.cs # read: same chain
│   │   ├── Links.cs                      # read: the link members that exist (no describedby)
│   │   ├── Link.cs, Meta.cs, IMeta.cs    # read: converter-owned wire forms
│   │   └── ErrorDocument.cs
│   ├── Resources/
│   │   ├── IIncluded.cs, AnyIncluded.cs  # read: the undeclared default
│   │   └── Resource.cs                   # read: Resource<A,R> unwrapping
│   └── Serialization/
│       └── IncludedShape.cs              # CHANGE: publish a minimal accessor over this cache
│
├── JsonApiLite.OpenApi/                  # description package — net10.0
│   ├── JsonApiBody.cs                    # CHANGE: chain walk; carry Meta and Included
│   └── JsonApiSchemaBuilder.cs           # CHANGE: envelope fragments — meta, links, included
│
└── tests/
    ├── JsonApiLite.Tests/                # unchanged; its wire tests must pass untouched
    └── JsonApiLite.OpenApi.Tests/        # NEW — net10.0, xunit, asserts emitted schema JSON

JsonApiPoc.Api/                           # sample; consumes PUBLISHED packages
├── Program.cs                            # the annotations under test (line 88, 138)
└── Contracts.cs                          # PageMeta, ContactIncluded — the declarations described
```

**Structure Decision**: The existing three-package split is kept exactly as it is, and this feature
does not move code between packages. The framework-dependent work — everything about
`OpenApiSchema` — stays in `libs/JsonApiLite.OpenApi`, which is where Principle III requires it. The
single core edit is deliberately a *reflection accessor*, not a schema concept, so no OpenAPI notion
leaks into the zero-dependency package. The one structural addition is
`libs/tests/JsonApiLite.OpenApi.Tests`, which must also be added to `JsonApiLite.sln` so
`dotnet test JsonApiLite.sln` covers it.

## Delivery order

Story order is P1 → P4, and it is also the dependency order.

| Step | Story | Touches | Done when |
| --- | --- | --- | --- |
| 1 | — | `libs/tests/JsonApiLite.OpenApi.Tests` (new), `JsonApiLite.sln` | `dotnet test JsonApiLite.sln` runs the new project; a test pinning today's `data`-only envelope passes |
| 2 | **P1** | `JsonApiBody.cs` | `ResourceDocument<A,R,M,I>` is accepted; its `data` is identical to `ResourceDocument<A,R>`; unsupported types still throw; the sample starts |
| 3 | **P2** | `JsonApiBody.cs`, `JsonApiSchemaBuilder.cs` | `PageMeta` appears walked; a `Meta` document shows an unconstrained object with no `members` property |
| 4 | **P3** | `JsonApiSchemaBuilder.cs` | The R3 per-kind link table holds; nothing new is `required` |
| 5 | **P4** | `IncludedShape.cs` (accessor), `JsonApiSchemaBuilder.cs` | `included` is an `anyOf` over declared types; undeclared documents describe an unconstrained resource array |
| 6 | all | `JsonApiPoc.Api` (temporarily) | `/openapi/v1.json` read back and reported, with the reference mode stated |

Step 1 first because every later step's evidence is a schema assertion, and there is nowhere to put
one today. Step 2 before 3–5 because the sample cannot start — and therefore cannot serve as
evidence — until it lands.

## Open questions

Both are closed as of 2026-07-29. Recorded here so the decisions are not re-litigated.

1. ~~`describedby` cannot be described.~~ **Closed** — the spec drops it. FR-008 now says "the
   document link members the library can actually send"; FR-008a states the general rule, "The
   description MUST NOT describe a link member the library cannot produce"; FR-021 no longer names
   it. Adding `DescribedBy` to core `Links` was the rejected alternative — a wire-model change to
   satisfy a description requirement. The R3 link table is unchanged by this: it never listed
   `describedby`.
2. ~~`001-document-envelope-schemas` is still live.~~ **Closed** — marked superseded by `003`, with
   the FR-016 contradiction spelled out in its header. Kept rather than deleted because `003`
   inherits four of its clarifications and the record of when those were decided lives there.

Remaining, not blocking: the unfinished `002` migration in the working tree (see Risks).

## Risks

| Risk | Mitigation |
| --- | --- |
| The sample cannot verify local changes (it consumes published packages) | Temporary `ProjectReference` swap during verification, reverted before commit, with the mode stated in any reported output (constitution requires this disclosure) |
| A `Meta`-derived type gets walked and describes `members` | Explicit derivation guard, with a test for `ResourceDocument<A,R,Meta>` asserting no `members` property (R2) |
| Widening accepted types hides a real mistake behind an empty schema | The chain walk falls through to the existing `ArgumentException`; a test pins that an unsupported type still throws (FR-003) |
| An envelope member described as required breaks a valid response | A test asserts every `required` array in the emitted document contains only `data`, `errors`, `type`/`id`, or `href` |
| The unfinished `002` migration in the working tree confuses evidence | `JsonApiPoc.Api/Contracts.cs:59` still declares the removed `Undeclared` member and `JsonApiLite.sln` has been edited to include the sample; settle both before using the sample as evidence |
