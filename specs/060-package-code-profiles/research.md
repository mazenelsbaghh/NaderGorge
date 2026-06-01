# Research: Package-Specific Code Page Profiles

## Decision 1: Package-specific code pages should extend the existing code-redemption flow

**Decision**: Add a package-aware route under the existing student code-redemption experience, instead of replacing the current generic page or overloading the package details page.

**Rationale**: The live app already has a generic activation route at `/student/code-redemption` and a polished student package route at `/student/packages/[packageId]`. A package-specific code page should feel like the same activation experience, but branded for a selected package. Keeping it in the code-redemption route family preserves navigation clarity and minimizes regression risk.

**Alternatives considered**:
- Replace `/student/packages/[packageId]` with the code page. Rejected because that route already serves as the package detail/contents surface.
- Add package-specific content to the existing generic page only through query params. Rejected because it weakens route clarity and makes direct linking less robust.

## Decision 2: Keep profile customization in a dedicated persistence model

**Decision**: Introduce a new `PackageCodePageProfile` aggregate linked one-to-one to `Package`, rather than storing code-page fields directly on `Package`.

**Rationale**: The specification explicitly requires a clear distinction between package identity data and code page customization data. Keeping the profile in a separate entity supports FR-010, keeps `Package` focused on academic/commercial identity, and allows reset/fallback semantics without mutating core package fields.

**Alternatives considered**:
- Add nullable code-page columns directly to `packages`. Rejected because it mixes package identity with view-specific marketing copy and weakens maintainability.
- Store arbitrary profile JSON in a generic settings table. Rejected because it makes validation, tooling, and typed contracts weaker than this feature needs.

## Decision 3: Use a structured template, not a free-form page builder

**Decision**: The profile will expose a bounded set of editable fields that map to the current code page composition: hero badge/title/description, activation intro copy, offer panel copy, and informational/support blocks, plus a profile status.

**Rationale**: The user request asks to edit "everything" within the package code profile experience, not to build arbitrary layouts. A structured template keeps the admin workflow manageable, makes required-field validation feasible, and preserves the editorial design language already used by the student redemption page.

**Alternatives considered**:
- Full block-based page builder. Rejected because it exceeds the stated scope and would require much heavier validation and rendering infrastructure.
- Very small profile with only title/description overrides. Rejected because it would not cover the request to tailor messaging and offer details per package.

## Decision 4: Draft and published states should govern fallback behavior

**Decision**: A package code page profile can be edited in draft, but the student-facing code page only uses the custom profile when it is valid and published. Otherwise, the page falls back to the generic default profile.

**Rationale**: The spec asks both for safe management and for prevention of invalid public output. Draft/published semantics provide a clean answer: admins can iterate without breaking the student page, and incomplete configurations never leak to users.

**Alternatives considered**:
- Save every edit directly as live content. Rejected because partially complete data could break the public page.
- Require only valid final saves and no draft state. Rejected because it makes iterative editing clumsy and does not address partial work gracefully.

## Decision 5: Reset should restore fallback behavior without deleting package identity

**Decision**: Resetting a package code page should disable or clear the custom profile so the package immediately returns to the generic default experience, while keeping package identity and profile auditability intact.

**Rationale**: This satisfies FR-009 and the edge-case requirement around administrative traceability. The reset operation should be semantically about removing custom presentation, not deleting the package or corrupting history.

**Alternatives considered**:
- Hard-delete the profile record. Rejected because it complicates traceability and future auditing.
- Keep custom content live but hidden behind flags without clearing it. Rejected because it makes the true active state harder to reason about.

## Decision 6: Test coverage should combine existing Playwright flows with backend profile validation tests

**Decision**: Extend the existing frontend Playwright coverage for admin content and code activation flows, and add backend tests around profile validation, fallback selection, and reset behavior.

**Rationale**: The user stories are end-to-end in nature, and the repo already contains `frontend/tests/e2e`. At the same time, fallback and publication logic are easy to regress if they are only tested through UI flows, so backend coverage is warranted for the new profile aggregate.

**Alternatives considered**:
- UI-only coverage. Rejected because publication/fallback logic needs faster, more isolated protection.
- Backend-only coverage. Rejected because admin workflow and student route wiring are central acceptance criteria.
