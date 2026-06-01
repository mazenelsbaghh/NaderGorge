# Implementation Plan: Restore Critical Learning Workflows

**Branch**: `[061-fix-critical-workflows]` | **Date**: 2026-04-09 | **Spec**: [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/061-fix-critical-workflows/spec.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/061-fix-critical-workflows/spec.md)
**Input**: Feature specification from `/specs/061-fix-critical-workflows/spec.md`

**Note**: This plan covers Phase 0 research and Phase 1 design artifacts for the critical workflow restoration feature only.

## Summary

Restore three broken learning workflows without widening scope: require administrator moderation before community comments become visible, grade find-the-mistake questions with type-specific answer matching instead of multiple-choice logic, and make essay grading move through an explicit, queryable lifecycle that supports partial results until teacher grading is complete. The implementation will stay within the existing clean-architecture boundaries by updating backend domain/application/API contracts, adding the minimal persistence changes required for moderation and grading states, and aligning affected frontend/admin consumers to the revised response contracts.

## Technical Context

**Language/Version**: C# / .NET 8 backend, TypeScript 5.x with Next.js 16.2.1 and React 19 frontend  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, Entity Framework Core, PostgreSQL provider, Next.js App Router, Axios service layer, Tailwind CSS  
**Storage**: PostgreSQL for relational workflow state; Redis remains available for background and callback-adjacent infrastructure but is not the primary persistence target for this feature  
**Testing**: .NET application tests in `backend/tests/NaderGorge.Application.Tests`, frontend linting via ESLint, existing end-to-end coverage via Playwright where useful for regression checks  
**Target Platform**: Web application with ASP.NET Core backend API and Next.js frontend, plus internal webhook callback surface
**Project Type**: Multi-project web application with backend API, frontend client, and supporting internal callbacks  
**Performance Goals**: Preserve standard CRUD latency expectations from the constitution, keep moderation and grading-status reads within normal admin/student interaction budgets, and avoid additional round trips for final-result recalculation after teacher grading  
**Constraints**: Must preserve clean architecture layering, enforce admin-only moderation actions, use versioned EF migrations, keep exam timing/result integrity server-side, and make AI callback handling idempotent enough to avoid duplicate grade transitions  
**Scale/Scope**: One feature branch spanning 3 broken workflows, focused primarily on backend domain/application/API changes with targeted frontend/admin contract alignment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. Changes stay within Domain, Application, API, Infrastructure migrations, and frontend service/component consumers without bypassing boundaries.
- **III. Security & Access Control by Default**: Pass. Community moderation endpoints remain admin-only, internal essay callback remains token-protected, and no new public elevation paths are introduced.
- **IV. Phased Delivery with MVP Discipline**: Pass. Scope is limited to repairing already-shipped critical workflows rather than introducing later-phase product expansion.
- **VII. Observability & Operational Readiness**: Pass. The plan uses versioned migrations, keeps callback flows explicit, and requires predictable result states instead of silent grading failures.
- **IX. Assessment & Time Integrity**: Pass. Essay grading changes preserve server-side exam truth and avoid marking unfinished attempts as final.
- **X. Pricing & Currency Localization**: Not applicable.

**Post-Design Re-check**: Pass. Phase 1 artifacts preserve clean boundaries, document contract changes explicitly, and do not require constitution exceptions.

## Project Structure

### Documentation (this feature)

```text
specs/061-fix-critical-workflows/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── critical-workflows.openapi.yaml
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   ├── NaderGorge.Application/
│   │   ├── Features/Admin/
│   │   ├── Features/Community/
│   │   ├── Features/Exams/
│   │   └── Features/Webhooks/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   └── Enums/
│   └── NaderGorge.Infrastructure/
│       ├── Data/
│       └── Migrations/
└── tests/
    └── NaderGorge.Application.Tests/

frontend/
└── src/
    ├── components/
    │   ├── admin/
    │   └── student/
    └── services/
```

**Structure Decision**: Use the existing web-application split. Most behavior changes land in backend domain/application/API layers and migrations, while frontend work is limited to service contracts and admin/student UI states that consume the revised APIs.

## Complexity Tracking

No constitution violations currently identified. No exceptions required.
