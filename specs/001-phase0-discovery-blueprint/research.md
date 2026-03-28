# Research: Phase 0 — Discovery, Planning, and Product Blueprint

**Branch**: `001-phase0-discovery-blueprint`
**Date**: 2026-03-21
**Status**: Complete — no NEEDS CLARIFICATION remaining

## Research Summary

Phase 0 is a documentation-only phase. All technical decisions are already resolved in the plan.md (primary source of truth) and clarified during the `/speckit-clarify` session. This research document consolidates the key decisions and their rationale.

---

## Decision 1: Deliverable Format

**Decision**: All 8 Phase 0 deliverables produced as Markdown files in the repository under `specs/001-phase0-discovery-blueprint/deliverables/`.

**Rationale**: Keeps everything version-controlled, PR-reviewable, and searchable alongside code. No external tool dependencies.

**Alternatives considered**:
- Notion/Confluence pages — rejected: outside repo, not version-controlled with code
- Figma + Markdown split — rejected: adds tool dependency for wireframes that can be described textually at this stage
- Google Docs — rejected: not version-controlled, permission management overhead

---

## Decision 2: Canonical Terminology for Content Groups

**Decision**: "Content Section" is the canonical term for the grouping layer between Package and Lesson. The legacy term "months" is an internal documentation alias only — MUST NOT appear in API names, UI labels, or data model identifiers.

**Rationale**: "Content Section" is self-explanatory and avoids confusion with calendar months. New team members understand the term immediately without context.

**Alternatives considered**:
- "Month" as canonical — rejected: creates ongoing confusion about whether it's time-based
- "Bundle" — rejected: too generic, conflicts with other potential uses (e.g., code bundles)

---

## Decision 3: Teacher vs. Admin Role Model

**Decision**: Teacher and Admin are separate roles. A single user (e.g., Nader George) can hold multiple roles simultaneously via multi-role assignment.

**Rationale**: Keeps the permission model clean and scalable. When new admins or teachers join later, roles can be assigned independently without refactoring.

**Alternatives considered**:
- Merged "Owner" super-role — rejected: creates a rigid model that doesn't scale when team grows
- Teacher inherits Admin — rejected: semantically incorrect; a teacher shouldn't automatically have system management permissions

---

## Decision 4: Deliverable Count and Scope

**Decision**: 10 distinct deliverable files (not 8) to properly separate concerns. The original 8 from the plan are preserved, but System Blueprint and Data Model Draft are separate files from Technical Architecture Document.

**Rationale**: Keeping each deliverable focused on one concern makes review easier and aligns with the modular architecture principle from the constitution.

**Deliverable mapping**:

| # | Deliverable | Covers Spec User Story |
|---|-------------|------------------------|
| 01 | Product Requirements Document | US1 |
| 02 | Content Blueprint | US1 |
| 03 | Access Blueprint (Code System) | US2 |
| 04 | Data Blueprint | US2 |
| 05 | User Roles Matrix | US4 |
| 06 | Technical Architecture Document | US3 |
| 07 | Business Rules Document | US1, US2 |
| 08 | UX Direction & Sitemap | US5 |
| 09 | Data Model Draft | US3 |
| 10 | System Blueprint (Deployment) | US3 |

---

## Decision 5: Authentication Method

**Decision**: Phone-based registration with JWT authentication and refresh token flow. No OTP verification mentioned in the updated plan (removed by project owner).

**Rationale**: Phone-based auth is standard for the Egyptian education market. JWT + refresh tokens provide stateless authentication suitable for the multi-service architecture.

**Alternatives considered**:
- Email-based auth — rejected: students in this demographic use phones primarily
- Social login (Google/Facebook) — rejected: adds external dependency, not aligned with platform's standalone approach

---

## Decision 6: AI Scope Boundaries for Phase 0

**Decision**: AI scope is documented in Phase 0 but NOT implemented until Phase 4. The AI Scope Definition deliverable will include explicit "in-scope" and "out-of-scope" lists. All AI features must operate within teacher-approved academic content only.

**Rationale**: Constitution Principle V (Academic Content Integrity) mandates bounded AI. Documenting boundaries early prevents over-engineering data models for AI in Phase 1.

**Alternatives considered**:
- Defer AI documentation to Phase 4 — rejected: late documentation risks AI-unfriendly data models being locked in before Phase 4
- Include AI-ready fields in Phase 1 schema — acceptable per constitution: "Schema fields for future features MAY be added early, but business logic MUST NOT be implemented ahead of schedule"
