# Implementation Plan: Phase 3 Logic and Performance Fixes

**Branch**: `063-fix-logic-performance` | **Date**: 2026-04-09 | **Spec**: [spec.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/063-fix-logic-performance/spec.md)
**Input**: Feature specification from `/specs/063-fix-logic-performance/spec.md`

## Summary

Deliver the seven medium-priority fixes by tightening existing backend workflows rather than introducing new subsystems: cache platform settings for watch logic, enforce strict status and duration validation, cap extra watch requests per video, remove moderation N+1 aggregation, and make exam penalty and per-question timing outcomes deterministic in persisted answer records and result building.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend, TypeScript 5.x / Next.js 16.2.1 frontend, Node.js worker present but not directly changed in this feature  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, Entity Framework Core, PostgreSQL provider, in-process memory cache, existing frontend Axios service layer  
**Storage**: PostgreSQL for relational state, in-memory cache for 10-minute platform settings reuse  
**Testing**: .NET unit/integration tests, API contract checks, frontend service integration verification, migration validation against a clean database  
**Target Platform**: Web application with ASP.NET Core backend API, Next.js frontend, and existing worker infrastructure  
**Project Type**: Multi-project web application  
**Performance Goals**: Remove repeated settings reads from hot watch-tracking paths and eliminate per-post moderation count amplification; support the spec target of at least 30% median improvement for the admin community moderation list on 100-post datasets  
**Constraints**: Preserve clean architecture boundaries, keep database access in Infrastructure/Application query handlers only, maintain role-based API behavior, use migrations for schema changes, and treat server-side timestamps as the source of truth for exam timing  
**Scale/Scope**: Focused update to existing tracking, moderation, and exams workflows across backend API/application/domain layers with minor frontend/service contract alignment where response semantics become stricter

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. Changes stay within existing API, Application, Domain, and Infrastructure boundaries.
- **III. Security & Access Control by Default**: Pass. Existing role restrictions remain; stricter validation and cache invalidation improve correctness without weakening controls.
- **IV. Phased Delivery with MVP Discipline**: Pass. Scope is limited to the seven items already defined for the current phase.
- **VII. Observability & Operational Readiness**: Pass. Validation and cache behavior remain compatible with structured error handling and migration discipline.
- **IX. Assessment & Time Integrity**: Pass with explicit design requirement. Per-question timing will rely on persisted server timestamps, not client clocks.
- **Result**: No constitution violations identified before research. Complexity tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/063-fix-logic-performance/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── api-contracts.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   ├── Controllers/
│   │   └── Program.cs
│   ├── NaderGorge.Application/
│   │   ├── Features/Admin/
│   │   ├── Features/Content/
│   │   ├── Features/Exams/
│   │   ├── Features/Student/
│   │   └── Features/Tracking/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   └── Interfaces/
│   └── NaderGorge.Infrastructure/
│       ├── Data/
│       └── Migrations/
└── tests/

frontend/
├── src/
│   ├── app/
│   ├── components/
│   └── services/
└── tests/

worker/
└── src/
```

**Structure Decision**: Use the existing web-application layout. This feature is backend-heavy, with any frontend touch points limited to existing service consumers and admin/student views that depend on corrected API semantics.

## Phase 0: Research

- Completed in [research.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/063-fix-logic-performance/research.md).
- All technical decisions are resolved; no open `NEEDS CLARIFICATION` items remain.

## Phase 1: Design & Contracts

- Data model documented in [data-model.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/063-fix-logic-performance/data-model.md).
- API and behavior contracts documented in [api-contracts.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/063-fix-logic-performance/contracts/api-contracts.md).
- Validation flow and verification steps documented in [quickstart.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/063-fix-logic-performance/quickstart.md).
- Agent context must be refreshed after artifact generation.
- Implementation note: `QuestionStartedAt` is persisted by seeding reusable `StudentAnswer` placeholders when an attempt starts, so later help-tool usage and timeout checks update the same answer records instead of creating parallel rows.

## Post-Design Constitution Check

- **I. Modular Clean Architecture**: Pass. Shared rules are introduced as focused supporting components rather than leaking infrastructure into controllers or domain models.
- **III. Security & Access Control by Default**: Pass. Invalid status, duration, and over-limit requests are rejected explicitly, preserving secure defaults.
- **IV. Phased Delivery with MVP Discipline**: Pass. No additional feature creep beyond the scoped seven fixes.
- **VII. Observability & Operational Readiness**: Pass. Design preserves migration-based schema updates and consistent API validation/error flows.
- **IX. Assessment & Time Integrity**: Pass. Question timing is anchored to persisted server timestamps and enforced during submission/result calculation.
- **Result**: No post-design constitution violations identified.

## Complexity Tracking

No constitution exceptions or justified complexity deviations are required for this feature.
