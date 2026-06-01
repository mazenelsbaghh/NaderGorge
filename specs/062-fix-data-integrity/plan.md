# Implementation Plan: Phase 2 Data Integrity Fixes

**Branch**: `[062-fix-data-integrity]` | **Date**: 2026-04-09 | **Spec**: [/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/spec.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/spec.md)
**Input**: Feature specification from `/specs/062-fix-data-integrity/spec.md`

**Note**: This plan covers Phase 0 research and Phase 1 design artifacts for the data-integrity repair scope only.

## Summary

Repair five persisted-data inconsistencies without widening scope: unify lesson watch-threshold evaluation across both watch-tracking flows, ensure first-view counts start from zero and require real duration input, persist and return student theme mode while auto-creating missing student profiles, retain optional essay audio references and expose written corrections only after exam completion, and store plus return rejection reasons for rejected extra watch requests. The implementation stays inside the existing clean-architecture split by confining domain and persistence changes to backend layers, documenting API contract adjustments explicitly, and limiting frontend impact to request and response shape alignment.

## Technical Context

**Language/Version**: C# / .NET 8 backend, TypeScript 5.x with Next.js 16.2.1 and React 19 frontend  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, Entity Framework Core, PostgreSQL provider, Next.js App Router, Axios service layer, Tailwind CSS  
**Storage**: PostgreSQL for watch tracking, student profile, exam submission, and extra watch request state  
**Testing**: .NET application tests in `backend/tests/NaderGorge.Application.Tests`, frontend linting via ESLint, existing Playwright end-to-end coverage for regression verification where useful  
**Target Platform**: Web application with ASP.NET Core backend API and Next.js frontend  
**Project Type**: Multi-project web application with backend API and frontend client  
**Performance Goals**: Preserve standard CRUD latency expectations from the constitution, avoid extra watch-count recomputation passes per request, and keep preference/status/result reads within normal student interaction latency  
**Constraints**: Must preserve clean architecture layering, use versioned EF Core migrations, keep exam-result visibility rules server-side, reject watch-tracking requests that lack usable duration data, and avoid destructive backfills that force students to resubmit existing data  
**Scale/Scope**: One feature branch spanning five high-priority data-integrity repairs across tracking, student preferences, exams, and extra watch requests, with backend-dominant changes and limited frontend/API contract updates

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. The plan keeps behavior in API, Application, Domain, and Infrastructure layers with explicit DTO and migration updates instead of cross-layer shortcuts.
- **III. Security & Access Control by Default**: Pass. The affected endpoints stay under existing student/admin authorization boundaries and add validation rather than relaxing access.
- **IV. Phased Delivery with MVP Discipline**: Pass. Scope is restricted to Phase 2 data-integrity repairs already identified in `FIXES_PLAN.md`; no adjacent performance or moderation features are pulled in.
- **VII. Observability & Operational Readiness**: Pass. The design relies on versioned migrations, deterministic validation failures for missing duration data, and explicit status fields rather than hidden fallbacks.
- **IX. Assessment & Time Integrity**: Pass. Exam-result visibility remains server-controlled, and written corrections are explicitly withheld until the attempt is no longer in progress.
- **X. Pricing & Currency Localization**: Not applicable.

**Post-Design Re-check**: Pass. Phase 1 artifacts preserve clean boundaries, document all contract changes that leak across services, and require no constitution exceptions.

## Project Structure

### Documentation (this feature)

```text
specs/062-fix-data-integrity/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── data-integrity.openapi.yaml
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   ├── NaderGorge.Application/
│   │   ├── Features/Exams/
│   │   ├── Features/Student/
│   │   └── Features/Tracking/
│   ├── NaderGorge.Domain/
│   │   └── Entities/
│   └── NaderGorge.Infrastructure/
│       ├── Data/
│       └── Migrations/
└── tests/
    └── NaderGorge.Application.Tests/

frontend/
├── src/
│   ├── app/
│   │   └── student/
│   ├── components/
│   │   └── student/
│   └── services/
└── tests/
    └── e2e/
```

**Structure Decision**: Use the existing web-application split. Most work lands in backend tracking, student, and exam features plus EF migrations; frontend impact is confined to service DTO alignment and student-facing consumers of the revised contracts.

## Complexity Tracking

No constitution violations currently identified. No exceptions required.
