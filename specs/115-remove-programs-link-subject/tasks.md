# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Remove Programs and Link Packages Directly to Subjects

**Input**: Design documents from `/specs/115-remove-programs-link-subject/`
**Prerequisites**: plan.md (required), spec.md (required)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Verify active feature directory structure under `specs/115-remove-programs-link-subject/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [ ] T002 Update C# entity model `Package.cs` in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs` to add `SubjectId`, `Subject` navigation property, and `TargetGrade` string property; remove `ProgramId` and `Program`.
- [ ] T003 Remove `Program.cs` domain entity from `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs` (or from its file if standalone).
- [ ] T004 Update `IAppDbContext.cs` in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` to remove `DbSet<Program> Programs`.
- [ ] T005 Update `AppDbContext.cs` in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` to remove `DbSet<Program> Programs` and the EF configuration block mapping `Program`. Configure `Package` relationship to `Subject` directly.
- [ ] T006 [P] Update `Subject.cs` in `backend/src/NaderGorge.Domain/Entities/Subject.cs` to remove `ICollection<Program> Programs` and add `ICollection<Package> Packages`.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Simplified Package Setup and Direct Subject/Grade Association (Priority: P1) 🎯 MVP

**Goal**: Enable creating packages with direct Subject and Grade selectors, filtering subjects by teacher.

**Independent Test**: Create a package as admin and as teacher, verifying it requires only Subject and Grade.

### Implementation for User Story 1

- [ ] T007 [P] [US1] Update `GetSubjectsQuery` and handler in `backend/src/NaderGorge.Application/Features/Admin/Queries/AdminSubjectQueries.cs` to accept optional `TeacherId` filter and filter subjects accordingly.
- [ ] T008 [US1] Expose `GET /api/teacher/subjects` endpoint in `backend/src/NaderGorge.API/Controllers/TeacherController.cs` to list the logged-in teacher's assigned subjects.
- [ ] T009 [US1] Update `CreatePackageCommand` and handler in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs` to accept `SubjectId` and `TargetGrade` directly, validating that the teacher teaches that subject.
- [ ] T010 [US1] Update `GetPackagesQuery.cs` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackagesQuery.cs` to read `SubjectId`, `SubjectName`, and `TargetGrade` directly from `Package`.
- [ ] T011 [US1] Update `GetPackageByIdQuery.cs` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackageByIdQuery.cs` to return package details directly from the modified model.
- [ ] T012 [US1] Update `GetAdminPackagesListQuery.cs` in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetAdminPackagesListQuery.cs` to map `SubjectId` directly from `Package` instead of `Program`.
- [ ] T013 [US1] Update `TeacherAuthorizationService.cs` in `backend/src/NaderGorge.Application/Services/TeacherAuthorizationService.cs` to check package access directly via `Package.SubjectId` and remove `CanAccessProgramAsync`.
- [ ] T014 [US1] Update `admin-service.ts` in `frontend/src/services/admin-service.ts` to add `listSubjects` method and update `createPackage` payload to pass `subjectId` and `targetGrade`.
- [ ] T015 [US1] Update the teacher package creation form in `frontend/src/app/teacher/packages/page.tsx` to select Subject and Grade Level directly.
- [ ] T016 [US1] Update the admin package creation form/page to select Subject and Grade Level directly.

**Checkpoint**: User Story 1 is functional for package creation and retrieval.

---

## Phase 4: User Story 2 - Legacy Package Data Migration and Continuity (Priority: P2)

**Goal**: Automatically migrate all existing packages from programs to direct subject and grade links with zero data loss.

**Independent Test**: Verify that existing packages are populated with the correct `SubjectId` and `TargetGrade` after migration.

### Implementation for User Story 2

- [ ] T017 [US2] Generate an EF Core migration (e.g. `AddPackageSubjectDirectLink`). In the `Up` method, add a SQL command to copy `SubjectId` and `TargetGrade` from `programs` to `packages` where `packages.ProgramId = programs.Id`, before dropping the foreign key and dropping the `programs` table.

**Checkpoint**: Migration generated and data preservation verified.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, test compilation, and unused query removal.

- [ ] T018 Delete `GetAdminProgramsQuery.cs` and `/teacher/programs` / `/admin/programs` endpoints since programs are no longer used.
- [ ] T019 Update any backend test files (e.g., `TeacherIsolationTests.cs` and others) to compile and pass with the new Package/Subject properties.
- [ ] T020 Run `dotnet test` to verify all backend tests pass.
- [ ] T021 Run `npm run lint` and `npm run build` on the frontend to verify compilation.

---

## Phase 6: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Execute Quality Gates and verify build completeness.

- [ ] T022 Run `clean-code-guard` against all modified production files.
- [ ] T023 Run `test-guard` to verify test suite health.
- [ ] T024 Run `docker compose config -q`
- [ ] T025 Run `make up` and `make migrate`
- [ ] T026 Complete manual QA checklist and compile final report.
