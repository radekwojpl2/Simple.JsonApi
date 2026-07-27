# Specification Quality Checklist: Typed Included Resources

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-27
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**Status**: all items pass. Validated 2026-07-27 after the clarification session; 23 functional
requirements (FR-001–FR-023) and 9 success criteria (SC-001–SC-009), zero open markers.

- **Both clarifications resolved (Session 2026-07-27)**, each answered B:
  - *Delivery shape* — the existing document forms change rather than gaining a parallel typed
    family. Encoded as FR-021, with the break bounded by FR-016, made mechanical by FR-022 and
    published as breaking by FR-023.
  - *View coexistence* — declaring commits the author to the typed view. Encoded as FR-017.

- **The two answers interact, and the spec was adjusted for it.** Because a declared document no
  longer offers an untyped fallback, Story 3's undeclared sideloaded resources lost their implicit
  home. FR-012 now requires an explicitly named place for them that exists on every declared
  document — without it, the two answers together would silently make undeclared resources
  unreachable. This was the one substantive consequence of the pairing and is the thing most worth
  re-reading before planning.

- **Requirements that reversed.** FR-016 and SC-004 previously demanded zero call-site edits. Under
  the chosen answer that is no longer achievable, so both were rewritten to bound the break rather
  than deny it: one edit to a document's declaration, zero to the code around it. Reviewers of an
  earlier draft should note these two flipped rather than assume they still hold.

- **Mechanism still unfixed.** The originating issue offers three candidate designs; the spec states
  the required outcome (no runtime type test at the point of use, FR-004) and leaves the choice to
  `/speckit-plan`. Two of the three cannot satisfy FR-004 and are excluded by it rather than by name.

- **Register note.** "Non-technical stakeholder" is read as it was for
  `001-document-envelope-schemas`: this is a developer library, so the user *is* a developer. The
  spec avoids naming types, methods and language features. One `file:line` citation appears in
  Assumptions as evidence for a claim about current behaviour, per the project's rule that verified
  claims cite their source.

- **Cross-spec conflict recorded, not resolved.** `001-document-envelope-schemas` FR-016 forbids
  changing the core wire-model package; this feature requires it, and now also breaks that package's
  public surface. Captured under Dependencies. Reconciling the two specs is a decision for the
  project owner and should happen before either is planned.
