# Implementation Plan: Student Theme Color Customization

**Branch**: `059-theme-color-customization` | **Date**: 2026-04-08 | **Spec**: [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/059-theme-color-customization/spec.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/059-theme-color-customization/spec.md)
**Input**: Feature specification from `/specs/059-theme-color-customization/spec.md`

## Summary

Add student-facing theme color personalization on top of the existing light/dark mode behavior by introducing curated color palettes for each mode, persisting each student's choices in backend-managed preferences, and applying those palettes only within the student experience without altering admin or public flows.

## Technical Context

**Language/Version**: TypeScript 5.x / Next.js 16.2.1 / React 19, C# 13 / .NET 9  
**Primary Dependencies**: Next.js App Router, Tailwind CSS, Axios service layer, MediatR, Entity Framework Core 9  
**Storage**: PostgreSQL for persistent student theme preferences, browser storage for last-known theme mode bootstrap only  
**Testing**: Frontend linting and test suite via `npm test && npm run lint`; backend unit and API contract coverage in existing .NET test projects  
**Target Platform**: Responsive web application for student-facing desktop and mobile browsers  
**Project Type**: Full-stack web application with Next.js frontend and .NET Web API backend  
**Performance Goals**: Theme changes feel immediate in-session, saved preference restoration occurs on first authenticated student render, no perceptible delay added to student shell navigation  
**Constraints**: Student-only scope, preserve readability across all approved palettes, keep clean architecture boundaries, use versioned migration if persistence schema changes, avoid breaking existing light/dark toggling behavior  
**Scale/Scope**: Authenticated student interface across dashboard, lesson, community, balance, code redemption, and related student pages for the active academic-year student base

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. Plan keeps backend changes inside Domain/Application/API/Infrastructure layers and frontend changes inside app/components/services/hooks/lib boundaries.
- **III. Security & Access Control by Default**: Pass. Preference endpoints remain authenticated and scoped to the current student; preference updates are state-changing and should emit audit logs.
- **VI. Single-Flow Registration & UX Simplicity**: Pass. Feature is student-facing, responsive, and does not add registration complexity.
- **VIII. Premium Editorial Design System**: Pass with design guardrails. Palettes must extend the existing editorial token system and avoid arbitrary or low-contrast combinations.
- **VII. Observability & Operational Readiness**: Pass. Any persistence change follows migration discipline and uses existing API error handling patterns.
- **Result**: No constitution violations identified. Phase 0 may proceed.

## Project Structure

### Documentation (this feature)

```text
specs/059-theme-color-customization/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── student-theme-preferences.openapi.yaml
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
│   │       └── Student/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   └── Interfaces/
│   └── NaderGorge.Infrastructure/
│       ├── Data/
│       └── Migrations/

frontend/
├── src/
│   ├── app/
│   │   └── student/
│   ├── components/
│   │   ├── layout/
│   │   ├── student/
│   │   └── ui/
│   ├── hooks/
│   ├── lib/
│   └── services/
```

**Structure Decision**: Use the existing web-application split. Backend owns persisted student preference data and authenticated contracts. Frontend owns palette selection UI, runtime token application, and student-shell integration. No new top-level projects are needed.

## Phase 0: Research Summary

- Persist palette preference in a backend-owned student preference model instead of localStorage-only state so choices survive device changes and account sessions.
- Keep theme mode (`light` / `dark`) conceptually separate from theme palette identity so students can choose different palettes per mode without rewriting current mode toggling behavior.
- Extend the existing CSS custom property approach rather than replacing it, because the student shell and other screens already rely on runtime token injection.
- Expose a minimal authenticated student API contract for reading and updating theme preferences using the current service/controller patterns.

## Phase 1: Design Summary

- Introduce a dedicated persisted preference shape for student theme mode selections and palette identifiers.
- Add a student contract with:
  - `GET /api/student/theme-preferences`
  - `PUT /api/student/theme-preferences`
- Add frontend palette registry and a student-facing theme hook that composes:
  - current mode
  - selected palette for that mode
  - computed CSS variables
- Add a student settings entry point from the existing shell chrome to manage theme choices without affecting admin surfaces.

## Post-Design Constitution Check

- **Architecture**: Still passes. Design keeps persistence, API, and UI concerns separated.
- **Security**: Still passes. Preferences remain user-scoped and authenticated.
- **Design System**: Still passes. Plan requires curated approved palettes rather than arbitrary user-defined colors.
- **Operational Readiness**: Still passes. Database change, API contract, and frontend rollout are all explicit.
- **Result**: No justified exceptions required.

## Complexity Tracking

No constitution exceptions or additional complexity justifications are required for this feature.
