# Implementation Plan: Package Profile and Term Management

**Branch**: `017-package-profile-management` | **Date**: 2026-03-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/017-package-profile-management/spec.md`

## Summary

Establish a dedicated, isolated dashboard page for each Package (`/admin/content/packages/[id]`) that aggregates all settings, statistics, and sub-content management. Specifically, it allows the addition and management of nested "Terms" using centralized shared components mapped from the "Editorial Scholar" guidelines.

## Technical Context

**Language/Version**: Next.js 14, React 18, TypeScript, C# 12, .NET 8
**Primary Dependencies**: Tailwind CSS, React Query, Entity Framework Core, Lucide React
**Storage**: PostgreSQL
**Testing**: Playwright (E2E), XUnit (Backend API layer)
**Target Platform**: Web App (Admin Dashboard)
**Project Type**: Full Stack Platform / Admin Service
**Performance Goals**: Instant client-side routing, API resolution < 500ms
**Constraints**: Must exclusively use existing design system components (`AdminShellChrome`, `AdminStatCard`); no external styling allowed. Must enforce academic hierarchy.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Architecture**: Uses isolated React components and standard generic page rendering. Maps identically to Domain hierarchy.
- **Provider Abstraction First**: N/A for this domain entity.
- **Security & Access Control**: The `admin/content/...` route is secured by Next.js middleware and requires an Admin JWT.
- **Phased Delivery**: Builds strictly upon Phase 1 content structures (enhancing admin usability) without leaking unauthorized features.
- **Academic Content Integrity**: Directly enforces the exact Package → Term hierarchy as mandated by Section V of the constitution.
- **Single-Flow Registration & UX Simplicity**: N/A (Applies to student app).
- **Premium Editorial Design System**: Entire scope mandates using pre-existing `AdminStatCard` glass-effect components and `AdminShellChrome`. Complies fully with No-Line rule.

*Result: Pass. Feature respects all constitution principles.*

## Project Structure

### Documentation (this feature)

```text
specs/017-package-profile-management/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code

```text
frontend/
├── src/
│   ├── app/
│   │   └── admin/
│   │       └── content/
│   │           └── packages/
│   │               └── [id]/
│   │                   └── page.tsx
│   ├── components/
│   │   └── admin/
│   │       ├── PackageDetailsForm.tsx
│   │       └── TermListManager.tsx
│   └── services/
│       └── curriculum-service.ts
backend/
├── src/
│   ├── NaderGorge.Application/
│   │   └── Features/Curriculum/
│   │       ├── Commands/ (AddTermCommand)
│   │       └── Queries/ (GetPackageById)
│   └── NaderGorge.API/
│       └── Controllers/
│           ├── PackagesController.cs
│           └── TermsController.cs
```

**Structure Decision**: Utilizes the typical Next.js frontend and .NET Backend option, introducing a dynamic Next.js App Route under `/admin/content/packages/[id]` and specific React client components configured to leverage the Next-js App router fetching mechanisms.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*No violations.*
