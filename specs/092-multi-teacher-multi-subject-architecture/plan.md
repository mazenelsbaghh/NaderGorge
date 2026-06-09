# Implementation Plan: Multi-Teacher Multi-Subject Architecture and Teacher Isolation

**Branch**: `092-multi-teacher-multi-subject-architecture` | **Date**: 2026-06-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/092-multi-teacher-multi-subject-architecture/spec.md`

## Summary

We will transform the platform from a single-teacher platform (focused on History) to a multi-teacher, multi-subject system where teachers are fully isolated. 
Key steps:
1. Introduce new database entities: `Subject`, `TeacherProfile`, and `TeacherSubject` (join table).
2. Associate existing content and code entities with `SubjectId` and `TeacherId` to enforce isolation.
3. Update database seeding and migrations to create default subject ("History") and teacher profile, linking all existing resources to them.
4. Enforce strict isolation in the backend handlers: Teachers and their assistants can only read or write resources (packages, code groups, exams, question banks, essay submissions) they own.
5. Create a new Next.js dashboard area `/teacher` for teachers to view and manage their packages, codes, exams, and grading.
6. Add filter options in the Admin Dashboard, allow creating and managing subjects and teachers.
7. Update student-facing UI to show teacher information and group packages by subject.

## Technical Context

- **Language/Version**: C# 13 (.NET 9), TypeScript 5.x, HTML5
- **Primary Dependencies**: EF Core 9.0, MediatR, FluentValidation, Next.js 16.2.1, React 19, Framer Motion, Tailwind CSS
- **Storage**: PostgreSQL, Redis cache
- **Testing**: xUnit (`NaderGorge.Application.Tests`), Playwright E2E (`frontend/tests/e2e/`)
- **Target Platform**: Linux Server (Dockerized)
- **Project Type**: Web Application (React frontend, C# API backend, Node.js worker)
- **Performance Goals**: All resource authorization checks executed via indexed foreign keys (O(1)); page loading under 2s.
- **Constraints**: Enforced row-level validation at Application service layer.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact across backend, frontend, worker, database, and Docker**:
  - **Backend**: Update `User` and create `TeacherProfile`, `Subject`, `TeacherSubject` domain entities. Modify `ContentEntities.cs`, `CodeEntities.cs`, `ExamEntities.cs`, and `EssaySubmission.cs`. Update `IAppDbContext` and `AppDbContext` configure Fluent API composite keys. Enforce ownership validation in MediatR behaviors/handlers. Add admin commands and queries.
  - **Frontend**: Create new layout and dashboard pages for `/src/app/teacher/`. Create administration pages under `/src/app/admin/teachers/` and `/src/app/admin/subjects/`. Add dropdown filters on admin content pages. Update student package detail and dashboard views.
  - **Worker**: No modifications needed.
  - **Database**: Add EF Core migration for PostgreSQL. Seed default History subject and default Teacher profile. Update existing records to link to these.
  - **Docker**: Health checks must verify PostgreSQL migration and container orchestration is fully functional.
- **Automated tests required**: Add unit tests validating that Teacher A cannot view Teacher B's packages, code groups, exams, or student answers.
- **Manual QA flows**: Log in as Admin to create a subject and teacher. Log in as Teacher to see empty dashboard. Link teacher to package. Log in as student to see package grouped by subject with teacher avatar.
- **Docker gate commands**: `make up && make migrate`.
- **Next Phase Rule**: Do not advance until unit tests and migration validation checks pass.

## Project Structure

### Documentation (this feature)

```text
specs/092-multi-teacher-multi-subject-architecture/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── tasks.md             # Detailed Task Breakdown
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   ├── Subject.cs
│   │   │   ├── TeacherProfile.cs
│   │   │   ├── TeacherSubject.cs
│   │   │   ├── User.cs (modified)
│   │   │   ├── ContentEntities.cs (modified)
│   │   │   ├── CodeEntities.cs (modified)
│   │   │   ├── ExamEntities.cs (modified)
│   │   │   └── EssaySubmission.cs (modified)
│   │   └── Interfaces/
│   │       └── IAppDbContext.cs (modified)
│   ├── NaderGorge.Infrastructure/
│   │   └── Data/
│   │       └── AppDbContext.cs (modified)
│   └── NaderGorge.Application/
│       └── Features/
│           ├── Admin/
│           └── Teacher/
frontend/
├── src/
│   ├── app/
│   │   ├── admin/
│   │   │   ├── teachers/
│   │   │   └── subjects/
│   │   ├── teacher/ (new dashboard)
│   │   └── (public)/
│   ├── components/
│   │   └── teacher/
│   └── services/
│       └── teacher-service.ts
```

**Structure Decision**: Multi-project layered architecture. Split into .NET backend (API, Application, Infrastructure, Domain), React Next.js frontend, and Node.js worker.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `dotnet test backend/NaderGorge.sln`
- Run frontend linter and builders: `npm run lint && npm run build` (inside `frontend/`)

**Docker Gate Required**:
- `make up` and `make migrate`
- Verify backend health: `curl -f http://localhost:5245/api/health`

**Manual QA Required**:
- Admin creates a teacher user and profile.
- Admin links teacher to a subject.
- Admin creates a package and links it to that teacher.
- Log in as the teacher, verify they can only see their package, create an exam, and generate registration codes.
- Log in as another teacher, verify they cannot view or edit these resources.
- Log in as a student, activate the generated registration code, and check their dashboard to see the package and teacher bio.

**End-of-Phase Report Format**:
- Summary of implemented scope.
- Tests validation output.
- Verification checks screenshot / logs.
- Go/No-go for Phase 5.

## Complexity Tracking

*No violations of Constitution identified.*
