<!--
Sync Impact Report
==================
Version change: (template, unversioned) → 1.0.0
Bump rationale: First ratification. All placeholder tokens replaced with concrete,
project-specific governance derived from CLAUDE.md and ROADMAP.md.

Modified principles (all newly defined, none renamed):
  - [PRINCIPLE_1_NAME] → I. The Specification Decides
  - [PRINCIPLE_2_NAME] → II. Verify or Say You Did Not (NON-NEGOTIABLE)
  - [PRINCIPLE_3_NAME] → III. The Core Package Takes No Dependencies
  - [PRINCIPLE_4_NAME] → IV. Model the Wire, Nothing Else
  - [PRINCIPLE_5_NAME] → V. House Style Is Not Negotiable Per-Change

Added sections:
  - Build, Test and Verification Gates (was [SECTION_2_NAME])
  - Development Workflow (was [SECTION_3_NAME])

Removed sections: none

Templates requiring updates:
  ✅ .specify/templates/plan-template.md — "Constitution Check" resolves gates from this file
     at plan time; generic placeholder remains correct, no edit required.
  ✅ .specify/templates/spec-template.md — no constitution-mandated sections added or removed.
  ✅ .specify/templates/tasks-template.md — no new principle-driven task categories; the
     testing-discipline note stays accurate (tests are required for library behaviour, see
     Principle II and the Verification Gates section).
  ✅ .specify/templates/commands/*.md — directory does not exist in this repo; nothing to check.
  ✅ CLAUDE.md — the source of these principles; already consistent, no edit required.
  ✅ ROADMAP.md — scope boundaries in Principle IV quote it directly; no edit required.

Follow-up TODOs: none. No placeholder was intentionally deferred.
-->

# Simple.JsonApi Constitution

## Core Principles

### I. The Specification Decides

JSON:API 1.1 (https://jsonapi.org/format/) is the contract, and it settles every question about
behaviour. Intuition about what a REST API "should" do MUST NOT override it, and neither MUST the
behaviour of another JSON:API library.

- A change that contradicts a `MUST` in the specification MUST be rejected, regardless of how
  convenient the resulting API is.
- `MUST` / `SHOULD` / `MAY` MUST be kept straight when a gap is reported. An unimplemented `MAY` is
  not a defect; an unimplemented `MUST` is a conformance bug and MUST be recorded as one.
- The surrounding paragraph MUST be read, not only the sentence that matched the search. Exceptions
  to a `MUST` are usually one sentence away — as with `id` and `lid`.

**Rationale**: The product's value is that its types say exactly what the wire format says. Every
divergence from the specification is a bug the consumer discovers at integration time, long after
it was cheap to fix.

### II. Verify or Say You Did Not (NON-NEGOTIABLE)

Any claim about the specification, a framework, a package's behaviour, or this codebase MUST be
either checked against the source or explicitly marked as unchecked. There is no third state.

A verified claim MUST be reported with both of:

1. **Where it came from** — the URL plus the section, the command that was run, or the `file:line`.
2. **One or two sentences quoted verbatim** from that source, so a reader can judge the claim
   without opening it.

A claim that has not been checked MUST carry the literal token `notSure`, spelled exactly that way,
plus **what specifically** is unknown (a named method, clause or behaviour — not a topic) and
**what would resolve it** (the page to read, the command to run, the probe to write).

- Normative statements MUST NOT be paraphrased and presented as verified.
- Hedging words — "should work", "typically", "I believe" — MUST NOT stand in for the `notSure`
  marker; they read as knowledge to anyone skimming.
- A claim that can be settled by running something MUST be settled by running it, with the output
  shown, rather than reasoned about.
- Where inventing a method name, overload, spec clause, package version or behaviour is the only
  way to answer, the answer MUST be reported as unknown and the work MUST stop there.

**Rationale**: A fabricated answer reads exactly like a checked one, so the reader has no way to
tell them apart. It costs them the time to discover it is wrong, plus whatever was built on it in
the meantime. An admitted dead end is a usable result.

### III. The Core Package Takes No Dependencies

`libs/JsonApiLite` MUST have zero package references. Anything that needs a framework MUST ship as
a separate package alongside it, as `Simple.JsonApi.OpenApi` does.

- The core library MUST continue to target both `net8.0` and `net10.0`. The dependency ban is what
  makes that possible, and `Simple.JsonApi.OpenApi` targets `net10.0` only because ASP.NET Core
  forces it.
- Splitting a feature across packages when part of it needs a framework is the expected outcome,
  not a workaround. Parsing that is pure string handling belongs in the core; binding that needs
  ASP.NET Core belongs in a companion package.
- Package ids and assembly names differ deliberately and MUST NOT be "corrected": `Simple.JsonApi`
  ships `JsonApiLite.dll`, `Simple.JsonApi.OpenApi` ships `JsonApiLite.OpenApi.dll`. The nuget ids
  `JsonApiLite` and `JsonApiKit` were already taken.

**Rationale**: A wire model that drags in a web framework cannot be referenced from a console tool,
a message handler, or a net8.0 service. The zero-dependency rule is the reason the package is
usable in places a server framework is not.

### IV. Model the Wire, Nothing Else

The library models JSON:API documents and nothing else. The restraint is the product, not an
omission. [ROADMAP.md](../../ROADMAP.md) MUST be read before a feature is proposed, and the
boundaries it lists under *Not planned* MUST be treated as decided:

- **HTTP** — content negotiation, status codes, the `Location` header on a 201, and `406`/`415`
  responses belong to the caller. `JsonApiMediaType.Value` is the only concession.
- **Validation** — documents are not checked against the specification beyond what parsing
  requires.
- **Persistence, ORM or resource-graph integration** — no repositories, no `IQueryable`
  translation, no EF Core.
- **A server framework** — no controllers, no conventions, no routing. The annotations describe
  endpoints someone else wrote; they do not generate them.

Errors MUST be static problem details, produced by the `JsonApi.NotFound` / `Invalid` / `Malformed`
helpers in the sample's `JsonApi.cs`. Configurable error formatters and error abstractions MUST NOT
be reintroduced; that layer was removed deliberately.

A request to cross one of these boundaries MAY be reconsidered only through the *Requests* process
in ROADMAP.md — an issue describing a use case the wire model cannot express — never inline inside
an unrelated change.

**Rationale**: Every one of these boundaries is what keeps the library small enough to adopt
without adopting a framework. Libraries that map a database to JSON:API endpoints already exist and
are a different product.

### V. House Style Is Not Negotiable Per-Change

New code MUST read like the code around it. These rules are enforced in review:

- **No ternary returns.** Explicit `if` blocks with early returns, not `return cond ? a : b`, and
  no nested `? :` in an initializer. A single-level ternary in an initializer is permitted and is
  already used.
- **Comments explain why, not what.** A comment MUST justify a decision or record a constraint. A
  comment restating the code is worse than no comment.
- **Public API carries XML docs**, and they MUST say what the member is *for*, not restate its
  name.

**Rationale**: Style drift is a review cost paid on every subsequent change. These three rules are
the ones this codebase actually diverges on, so they are the ones written down.

## Build, Test and Verification Gates

Every change MUST pass, before it is proposed as complete:

```
dotnet build JsonApiLite.sln -c Release
dotnet test  JsonApiLite.sln -c Release
dotnet build JsonApiPoc.Api/JsonApiPoc.Api.csproj -c Release   # not covered by the solution
```

- `JsonApiPoc.Api` is **not** in `JsonApiLite.sln`, so a solution build does not compile it. It MUST
  be built explicitly after any change to something it consumes. It consumes the **published**
  packages, so a local change is not reflected there until published — that gap MUST be stated
  rather than glossed over when reporting results.
- Tests run against both `net8.0` and `net10.0`. A change that passes on one TFM only is a failing
  change.
- A schema change MUST be verified by reading the generated document back from `/openapi/v1.json`
  (Development only; Swagger UI at `/swagger`, Scalar at `/scalar` render that same document). A
  schema MUST NOT be called correct because it compiles.
- Failing or skipped steps MUST be reported as such, with the output. "Tests pass" MUST mean the
  command was run and passed.

## Development Workflow

- **Conventional Commits are mandatory.** semantic-release computes the version on merge to `main`.
- **Versions MUST NOT be set by hand**, and `CHANGELOG.md` MUST NOT be edited — it is generated.
- **Framework-dependent code goes in a companion package** at the moment it is written, not as a
  follow-up cleanup (Principle III).
- **A feature proposal MUST cite ROADMAP.md** — either the entry it advances, or why the boundary
  it crosses should move (Principle IV).
- **Conformance claims in a review MUST carry a citation** in the form Principle II requires. A
  reviewer MAY reject an uncited normative claim without further argument.

## Governance

This constitution supersedes other practice documents in this repository where they conflict.
[CLAUDE.md](../../CLAUDE.md) is the day-to-day working guidance and is expected to stay consistent
with these principles; [ROADMAP.md](../../ROADMAP.md) is the authority on scope under Principle IV.

**Amendment procedure.** An amendment MUST be proposed as a change to this file, MUST state which
principle it adds, removes or redefines, and MUST update the Sync Impact Report at the top of this
file in the same change. An amendment that alters what code is permitted MUST also name the
templates and guidance documents it affects, and update them in the same change or record them as
pending.

**Versioning policy.** This constitution is versioned independently of the packages, which are
versioned by semantic-release.

- **MAJOR** — a principle is removed, or redefined in a way that permits what it previously
  forbade.
- **MINOR** — a principle or section is added, or existing guidance is materially expanded.
- **PATCH** — clarification, rewording, typo, or a non-semantic refinement.

**Compliance review.** Every review MUST check the change against these principles, and the three
commands under *Build, Test and Verification Gates* MUST have been run. A violation MUST be either
fixed or justified explicitly in the change description — an unremarked violation is a blocking
review comment. Complexity that a principle forbids MUST be justified against the simpler
alternative it replaces, not merely asserted to be necessary.

**Version**: 1.0.0 | **Ratified**: 2026-07-27 | **Last Amended**: 2026-07-27
