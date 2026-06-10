# Implementation Plan: Remove Programs and Link Packages Directly to Subjects

**Branch**: `115-remove-programs-link-subject` | **Date**: 2026-06-10 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/115-remove-programs-link-subject/spec.md)
**Input**: Feature specification from `/specs/115-remove-programs-link-subject/spec.md`

## Summary
The "Program" entity will be removed to simplify the learning catalog configuration down to a two-tier hierarchy (`Package` -> `Subject`). The `Package` entity will directly reference `SubjectId` and have a `TargetGrade` column. A database migration will copy the subject associations and target grades from existing programs onto existing packages before dropping the `programs` table. The package creation and listing endpoints, along with the admin and teacher frontends, will be updated to remove programs and allow direct subject/grade selection.

## Technical Context

**Language/Version**: C# 13 (.NET 9.0), TypeScript 5.x (Next.js 16.2.1 / React 19)  
**Primary Dependencies**: Microsoft.EntityFrameworkCore 9.0.6, MediatR 12.4.1, Axios 1.13.6  
**Storage**: PostgreSQL 16  
**Testing**: xUnit (`dotnet test`)  
**Target Platform**: Linux server via Docker  
**Project Type**: Web application (C# Web API + Next.js App Router)  
**Performance Goals**: Package creation and API queries execute in <100ms  
**Constraints**: Zero data loss for existing packages, student enrollment grants, terms, sections, and lessons  
**Scale/Scope**: ~10k students, ~100 active packages  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact across backend, frontend, worker, database, and Docker**:
  - **Backend**: Update domain entities, DB context, API controllers, MediatR commands/queries, and authorization services.
  - **Frontend**: Update package management forms (Admin & Teacher surfaces) to remove the Program selector and add Subject/Grade selectors.
  - **Database**: Add columns, migrate data, drop `programs` table.
  - **Worker**: No impact (AI transcription/mind-maps are unrelated to packages/programs).
  - **Docker**: DB migration runs via `make migrate`.
- **Automated tests required for the phase's critical paths**:
  - Backend tests in `NaderGorge.Application.Tests` (specifically `MultiTeacher` and isolation tests) must be updated to mock the new package properties.
- **Manual QA flows required from the product owner**:
  - Administrator creates a package for a teacher, choosing Subject and Grade level.
  - Teacher creates a package, selecting from their assigned subjects and selecting Grade level.
- **Docker gate commands**:
  - `docker compose config -q`
  - `make up`
  - `make migrate`
- **Next-phase enforcement**: Next phase (tasks and implementation) cannot start until plan is approved.

## Project Structure

### Documentation (this feature)

```text
specs/115-remove-programs-link-subject/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── quickstart.md        # Phase 1 output
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   │       ├── AdminController.cs
│   │       └── TeacherController.cs
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   ├── ContentEntities.cs
│   │   │   └── Subject.cs
│   │   └── Interfaces/
│   │       └── IAppDbContext.cs
│   └── NaderGorge.Application/
│       ├── Features/
│       │   ├── Admin/
│       │   │   ├── Commands/
│       │   │   │   └── AdminContentCommands.cs
│       │   │   └── Queries/
│       │   │       ├── GetAdminPackagesListQuery.cs
│       │   │       └── GetAdminProgramsQuery.cs (to be DELETED)
│       │   └── Content/
│       │       └── Queries/
│       │           ├── GetPackageByIdQuery.cs
│       │           └── GetPackagesQuery.cs
│       └── Services/
│           └── TeacherAuthorizationService.cs
└── tests/
    └── NaderGorge.Application.Tests/
        └── MultiTeacher/
            └── TeacherIsolationTests.cs

frontend/
├── src/
│   ├── app/
│   │   ├── admin/
│   │   │   └── content/
│   │   │       └── page.tsx
│   │   └── teacher/
│   │       └── packages/
│   │           └── page.tsx
│   └── services/
│       ├── admin-service.ts
│       └── content-service.ts
```

**Structure Decision**: Standard C# clean architecture backend and Next.js frontend structure.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `cd backend && dotnet test` to run all application unit and integration tests.

**Docker Gate Required**:
- `docker compose config -q`
- `make up`
- `make migrate`
- `curl -f http://localhost:5245/api/health`

**Manual QA Required**:
- Log in as Admin: go to `/admin/content` (or package page). Click "New Package". Choose Teacher, choose Subject (assigned to teacher), select Grade, and create.
- Log in as Teacher: go to `/teacher/packages`. Click "New Package". Choose Subject, select Grade, and create. Verify the package is listed and can be populated with terms, sections, and lessons.
- Verify that teachers cannot see other teachers' packages or subjects, returning 403 where unauthorized.

**End-of-Phase Report Format**:
- Implemented scope details
- Migration execution logs
- Linter/compiler output verification
- Automated test run outcomes
- Manual verification screen captures
