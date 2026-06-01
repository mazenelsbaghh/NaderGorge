# Implementation Plan: Package-Specific Code Page Profiles

**Branch**: `060-package-code-profiles` | **Date**: 2026-04-08 | **Spec**: [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/spec.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/spec.md)
**Input**: Feature specification from `/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/spec.md`

## Summary

Add a package-scoped code redemption profile system on top of the existing package profile and student code-redemption flows. The feature will introduce a dedicated `PackageCodePageProfile` data model, admin read/update/reset endpoints, a new package-specific code page route that reuses the current activation form, and a fallback strategy that keeps the current generic redemption experience active whenever a package has no published custom profile.

## Technical Context

**Language/Version**: TypeScript 5.x / Next.js 16.2.1 / React 19 frontend, C# / ASP.NET Core `net8.0` backend (repo current state)  
**Primary Dependencies**: Next.js App Router, Tailwind CSS, Framer Motion, Axios; MediatR, EF Core 8, PostgreSQL provider  
**Storage**: PostgreSQL via EF Core migrations  
**Testing**: Existing Playwright E2E suite in `frontend/tests/e2e`; add package-profile/code-page coverage there, and add backend handler/API coverage in a new or adjacent .NET test project for profile persistence/validation  
**Target Platform**: Web application with Next.js frontend and ASP.NET Core API  
**Project Type**: Full-stack web application  
**Performance Goals**: Admin profile load/save within standard CRUD budget (<500ms p95 API target from constitution); package code page first content render remains within the current student-page expectation (<3s)  
**Constraints**: Preserve package identity vs profile customization boundaries, keep fallback behavior for packages without a published custom profile, use shared admin components, keep student activation flow compatible with existing `/student/code-redemption`, and implement schema changes only through EF migrations  
**Scale/Scope**: One code page profile per package; affects admin package profile page, student code-redemption surface, backend package/profile endpoints, and one new persistence aggregate

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Clean Architecture | PASS | Backend work stays split across Domain/Application/Infrastructure/API; frontend work remains in app/components/services layers. |
| III. Security & Access Control by Default | PASS | Profile mutation remains admin-only; student-facing profile reads stay separate from write contracts; validation enforced server-side. |
| VI. Single-Flow Registration & UX Simplicity | PASS | Student flow extends the existing activation experience instead of creating a fragmented parallel process. |
| VII. Observability & Operational Readiness | PASS | New API endpoints follow existing error envelope and migration workflow. |
| VIII. Premium Editorial Design System | PASS | Admin edits stay inside shared package profile chrome; student page reuses the current editorial code-redemption composition and tokens. |
| X. Pricing & Currency Localization | PASS | Package offer details continue to render localized Egyptian Pound wording and do not mix with gamification data. |

Post-design re-check: PASS. The selected design keeps package marketing copy isolated from package commerce/entity data, uses existing route families, and avoids any constitution exception.

## Project Structure

### Documentation (this feature)

```text
specs/060-package-code-profiles/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── package-code-page-profile.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   ├── NaderGorge.Application/
│   │   └── Features/
│   │       ├── Admin/
│   │       └── Content/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   └── Interfaces/
│   └── NaderGorge.Infrastructure/
│       └── Data/
│           └── Migrations/
├── NaderGorge.sln
└── tests/                    # To be added/extended for backend feature coverage

frontend/
├── src/
│   ├── app/
│   │   ├── admin/content/packages/[id]/
│   │   └── student/code-redemption/
│   ├── components/
│   │   ├── admin/
│   │   ├── forms/
│   │   └── student-pages/
│   └── services/
└── tests/
    └── e2e/

worker/
└── src/                     # No planned changes
```

**Structure Decision**: Use the existing split full-stack structure. Backend changes belong in `NaderGorge.Domain`, `NaderGorge.Application`, `NaderGorge.Infrastructure`, and `NaderGorge.API`. Frontend changes belong in the existing admin package profile route, a new package-aware student code-redemption route segment, shared UI components, and the service layer. The worker remains out of scope.

## Phase 0: Outline & Research

Resolved in [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/research.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/research.md):

1. Package-specific code pages should extend the existing student code-redemption flow instead of replacing package details or creating an unrelated entry point.
2. Profile customization should live in a dedicated persistence model, separate from `Package`, to satisfy FR-010 and enable independent reset/fallback behavior.
3. The profile should use a structured editable template rather than arbitrary page-builder JSON, so admin editing remains manageable and validation stays enforceable.
4. Draft vs published semantics should gate what students see: incomplete drafts are editable by admins but never shown publicly; students see either a published package profile or the generic fallback.
5. Test coverage should center on Playwright E2E for the admin/student flow and focused backend tests for validation, fallback, and reset behavior.

## Phase 1: Design & Contracts

Artifacts produced:

- [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/data-model.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/data-model.md)
- [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/contracts/package-code-page-profile.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/contracts/package-code-page-profile.md)
- [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/quickstart.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/quickstart.md)

Design decisions carried into implementation:

1. Add a `PackageCodePageProfile` aggregate with a unique `PackageId` and explicit presentation fields for hero copy, offer messaging, info cards, and publication state.
2. Expose admin endpoints to get, save, and reset a package profile, all scoped under the existing admin package route family.
3. Expose a student-facing read model for a package code page that merges `Package` commerce data with either the published custom profile or the generic fallback copy.
4. Extend `/admin/content/packages/[id]` with a dedicated code-profile tab using shared admin shell/components, not a separate management surface.
5. Add a package-aware student route under the code-redemption area so locked package CTAs can deep-link directly into package-branded activation.

## Phase 2: Implementation Strategy

Expected implementation slices for `/speckit.tasks`:

1. Persistence and migration
2. Application commands/queries and API endpoints
3. Admin package profile UI
4. Student package-specific code page UI
5. E2E and backend validation coverage

## Complexity Tracking

No constitution violations or special complexity exemptions are required for this feature.
