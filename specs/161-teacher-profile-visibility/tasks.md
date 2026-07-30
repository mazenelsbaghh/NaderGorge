# Tasks: Teacher Profile & Content Visibility

**Input**: Design documents from `specs/161-teacher-profile-visibility/`
**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/teacher-management-api.md`, `quickstart.md`

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Phase 1: Setup

- [ ] T001 Record `161-teacher-profile-visibility` as the active feature in `.specify/feature.json` and keep `AGENTS.md` SPECKIT markers pointing to `specs/161-teacher-profile-visibility/plan.md`.
- [ ] T002 Inventory every current teacher public/student route and query in `backend/src/NaderGorge.API/Controllers/PublicTeachersController.cs`, `backend/src/NaderGorge.API/Controllers/PublicController.cs`, `backend/src/NaderGorge.Application/Features/Public/`, `frontend/src/services/student-service.ts`, `frontend/src/app/student/teachers/`, and `frontend/src/app/teachers/`; record the final list in `achievements.md`.
- [ ] T003 [P] Add feature-specific backend test fixtures for an Admin, non-Admin staff user, visitor request, teacher, previous purchaser, teacher-owned package/content, and audit history in `backend/tests/NaderGorge.Application.Tests/`.
- [ ] T004 [P] Add the feature test command list and manual QA checklist from `quickstart.md` to `achievements.md` under the current run.

## Phase 2: Foundational

- [ ] T005 [P] Add `IsVisibleToStudents` and `IsContentVisibleToStudents` to `backend/src/NaderGorge.Domain/Entities/TeacherProfile.cs` with defaults that preserve current visibility for existing teachers.
- [ ] T006 [P] Configure the two fields and teacher ownership query indexes in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [ ] T007 Create `backend/src/NaderGorge.Infrastructure/Migrations/<timestamp>_AddTeacherVisibilityControls.cs` and its model snapshot update; verify existing teachers migrate to visible without changing purchase/grant rows.
- [ ] T008 Add `TeacherVisibilityService` and its interface under `backend/src/NaderGorge.Application/Services/` with methods that resolve teacher visibility and content visibility for teacher IDs and inherited package/term/section/lesson/video/exam/community/shared-package owners.
- [ ] T009 [P] Add FluentValidation rules under `backend/src/NaderGorge.Application/Features/Admin/Validators/` for full name, normalized unique phone, optional new password strength, commission range, URL/length limits, subject IDs, and visibility fields.
- [ ] T010 Update `backend/src/NaderGorge.Infrastructure/Data/StaffRealtimeChangeDetector.cs` and DI registration so teacher/profile visibility changes publish the existing staff data scopes and do not publish password values.

## Phase 3: User Story 1 - Edit a Teacher Completely (Priority: P1) 🎯 MVP

**Goal**: Admin can edit linked User and TeacherProfile fields atomically, including write-only password replacement.

**Independent Test**: Admin updates all supported fields, reloads the record, verifies persistence and audit data, then verifies a non-Admin update is denied and no password secret is returned.

### Tests for User Story 1

- [ ] T011 [P] [US1] Add handler tests in `backend/tests/NaderGorge.Application.Tests/TeacherProfileAdminTests.cs` for valid full update persistence, subject synchronization, duplicate phone rejection, invalid password/URL/commission rejection, and atomic no-partial-save behavior.
- [ ] T012 [P] [US1] Add authorization tests in `backend/tests/NaderGorge.Application.Tests/TeacherProfileAdminAuthorizationTests.cs` proving non-Admin requests cannot update User or TeacherProfile fields and read DTOs never contain password hash/value.
- [ ] T013 [P] [US1] Add audit/realtime tests in `backend/tests/NaderGorge.Application.Tests/TeacherProfileAdminAuditTests.cs` proving old/new non-secret values, actor, target, and refresh outbox scopes are recorded for one successful update.

### Implementation for User Story 1

- [ ] T014 Extend `TeacherDto` and `GetTeachersQueryHandler`/`GetTeacherByIdQueryHandler` in `backend/src/NaderGorge.Application/Features/Admin/Queries/AdminTeacherQueries.cs` with supported account/profile fields and both visibility states, excluding all password fields.
- [ ] T015 Extend `UpdateTeacherProfileCommand` and its handler in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminTeacherCommands.cs` to load User and TeacherProfile, normalize/validate identity fields, sync subjects, apply optional password replacement, and save atomically.
- [ ] T016 Add audit logging and credential invalidation to `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminTeacherCommands.cs` using existing `AuditLog`, `PasswordResetVersion`, `SecurityStampVersion`, and refresh-token revocation patterns; never serialize the submitted password or hash.
- [ ] T017 Update `backend/src/NaderGorge.API/Controllers/AdminController.cs` and `UpdateTeacherProfileRequestDto` to accept the complete Admin update contract while retaining `[HasPermission("users.manage")]`.
- [ ] T018 Update `frontend/src/services/teacher-service.ts` `TeacherDto`, `getTeachers`, `getTeacherById`, and `updateTeacher` payloads to match `contracts/teacher-management-api.md` and invalidate teacher/public/content query keys after success.
- [ ] T019 Update `frontend/src/app/admin/teachers/AdminTeachersPageClient.tsx` and `frontend/src/app/admin/teachers/[id]/TeacherProfilePageClient.tsx` to edit full name, phone, write-only new password, profile/contact/social/commission/subjects fields, and show field-level loading, validation, success, and failure states.
- [ ] T020 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TeacherProfileAdmin"`; expected result is all new US1 tests pass and no password data appears in response/audit assertions.

## Phase 4: User Story 2 - Independently Hide or Show a Teacher (Priority: P1)

**Goal**: Admin can independently remove a teacher from all student/visitor discovery and restore it without deleting the account.

**Independent Test**: Admin changes only teacher visibility, visitor/student teacher list/detail/search/community requests exclude the teacher, then Admin shows it and discovery returns while content state is unchanged.

### Tests for User Story 2

- [ ] T021 [P] [US2] Add backend tests in `backend/tests/NaderGorge.Application.Tests/TeacherVisibilityTests.cs` for visible/hidden/show transitions, idempotent repeats, Admin-only mutation, and audit/outbox evidence.
- [ ] T022 [P] [US2] Add public API regression tests in `backend/tests/NaderGorge.Application.Tests/PublicTeacherVisibilityTests.cs` for `/api/public/teachers`, `/landing`, `/{slugOrId}`, and `/{teacherId}/community-posts` exclusion and direct hidden-profile non-disclosure.

### Implementation for User Story 2

- [ ] T023 Extend `UpdateTeacherProfileCommand` and `TeacherDto` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminTeacherCommands.cs` and `backend/src/NaderGorge.Application/Features/Admin/Queries/AdminTeacherQueries.cs` to persist and return `IsVisibleToStudents` independently of content visibility.
- [ ] T024 Apply `TeacherVisibilityService` to `backend/src/NaderGorge.API/Controllers/PublicTeachersController.cs` and `backend/src/NaderGorge.Application/Features/Public/Queries/GetActiveTeachersQuery.cs` before projection, including detail, landing, public list, and teacher community paths.
- [ ] T025 Update `frontend/src/services/student-service.ts`, `frontend/src/app/student/teachers/`, and `frontend/src/app/teachers/` to handle hidden teacher not-found/empty states without exposing a stale cached profile.
- [ ] T026 Add Admin hide/show controls and explicit current-state badges to `frontend/src/app/admin/teachers/AdminTeachersPageClient.tsx` and `frontend/src/app/admin/teachers/[id]/TeacherProfilePageClient.tsx`, keeping the content toggle independent.
- [ ] T027 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TeacherVisibility|FullyQualifiedName~PublicTeacherVisibility"`; expected result is hidden teachers absent from all tested public/student responses and restored teachers visible again.

## Phase 5: User Story 3 - Independently Hide or Show Teacher Content (Priority: P1)

**Goal**: Admin can suspend all teacher-owned content for visitors, students, and previous purchasers without deleting historical records.

**Independent Test**: Admin hides only content, public/student course and teacher surfaces omit it, a previous purchaser is denied direct protected access, purchase/grant history remains, and showing restores access without repurchase.

### Tests for User Story 3

- [ ] T028 [P] [US3] Add access tests in `backend/tests/NaderGorge.Application.Tests/TeacherContentVisibilityAccessTests.cs` for package/term/section/lesson/video/exam inherited ownership, visitor denial, current-student denial, previous-purchaser denial, and show restoration.
- [ ] T029 [P] [US3] Add content projection tests in `backend/tests/NaderGorge.Application.Tests/TeacherContentVisibilityProjectionTests.cs` for public teacher detail packages/lessons/shared packages, student course lists, search/recommendations, community/related content, and direct identifier non-disclosure.
- [ ] T030 [P] [US3] Add regression tests in `backend/tests/NaderGorge.Application.Tests/TeacherContentVisibilityHistoryTests.cs` proving purchase, access grant, finance, academic progress, and audit rows survive hide/show and no duplicate purchase is created on restore.

### Implementation for User Story 3

- [ ] T031 Extend the Admin update contract and frontend state in `backend/src/NaderGorge.API/Controllers/AdminController.cs`, `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminTeacherCommands.cs`, `frontend/src/services/teacher-service.ts`, and the two Admin teacher page clients for an independent `isContentVisibleToStudents` toggle with confirmation copy.
- [ ] T032 Apply the content visibility predicate to `backend/src/NaderGorge.Application/Services/AccessCheckService.cs` before historical grant success is returned, preserving Admin/teacher operational bypass rules and returning the existing safe denial shape.
- [ ] T033 Apply the content visibility predicate to all teacher-owned student/public projection handlers identified in T002, including package/term/section/lesson/video/exam/shared-package/community and purchase/preview routes; document each changed file in `achievements.md`.
- [ ] T034 Update `frontend/src/services/student-service.ts`, content services, and affected student/public components to invalidate/ignore hidden responses, render the existing empty/not-found states, and never rely on frontend-only hiding for authorization.
- [ ] T035 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TeacherContentVisibility"`; expected result is previous purchasers denied while hidden, history preserved, and access restored after show.

## Phase 6: Deep Architectural, Code & UI/UX Critique

- [ ] T036 Compare all changed backend files against `spec.md`, `plan.md`, `data-model.md`, and `contracts/teacher-management-api.md`; record every authorization, query-leak, transaction, cache, audit, migration, and concurrency finding in `achievements.md` and fix it in the owning file.
- [ ] T037 Review changed Admin and student/public components for component size, accessibility labels, responsive layout, loading/empty/error states, stale cache behavior, and independent toggle clarity; fix findings in the owning component files and record evidence.
- [ ] T038 Re-run focused tests after every critique fix and check `git diff --check` before guards.

## Phase 7: Clean Code Guard

- [ ] T039 Run `clean-code-guard` against every changed production file (excluding tests), resolve all findings, and record the guard result in `achievements.md`.

## Phase 8: Test Guard

- [ ] T040 Run `test-guard` against every changed test file, resolve all findings, and record the guard result in `achievements.md`.

## Phase 9: Feature Tests, Final Verification & Summary

- [ ] T041 Run `python3 .agents/skills/speckit-all/scripts/extract_test_commands.py --spec-dir specs/161-teacher-profile-visibility` and execute every applicable extracted command.
- [ ] T042 Run the complete feature matrix from `spec.md`, `quickstart.md`, and `.agents/skills/speckit-all/references/feature-test-matrix.md`: Admin happy path, non-Admin denial, validation failures, independent hide/show, visitor/student/previous-purchaser denial, persistence, restore, audit, cache/realtime, and regression paths.
- [ ] T043 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj`, `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj -c Release`, `cd frontend && npm run lint && npm run typecheck && npm run build`, and `git diff --check`; expected result is zero feature-introduced errors/warnings.
- [ ] T044 Run `docker compose config -q`, apply the EF migration, run `docker compose ps`, `curl -fsS http://127.0.0.1:5245/api/health`, and `curl -fsS http://127.0.0.1:3001/ready`; expected result is healthy database, Redis, backend, worker, and frontend surfaces.
- [ ] T045 Complete manual QA from `specs/161-teacher-profile-visibility/quickstart.md` with Admin, visitor, student, and previous-purchaser sessions; record pass/fail evidence and any blocked external dependency in `achievements.md`.
- [ ] T046 Run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/161-teacher-profile-visibility`; resolve every failure, mark all phases complete only after evidence is recorded, and write the final readiness report.

## Dependencies & Execution Order

- Setup T001-T004 precedes all implementation; T002 must finish before T024/T033.
- Foundational T005-T010 precedes all user stories; T007 migration must pass before runtime verification.
- US1 T011-T020 is the MVP and can be delivered independently after foundation.
- US2 T021-T027 and US3 T028-T035 can run in parallel after T008/T010, but both share the US1 DTO/endpoint shape and should merge after T017/T018.
- Critique T036-T038 follows all story work; `clean-code-guard` T039 must precede `test-guard` T040; feature tests T041-T046 follow both guards.

## Parallel Execution Examples

```text
After T005-T010:
  Agent A: T011-T020 (Admin full edit and credential workflow)
  Agent B: T021-T027 (teacher discovery visibility and public routes)
  Agent C: T028-T035 (content visibility and access denial)

Within US3:
  T028, T029, and T030 can run in parallel because they use separate test files.
  T032 and T033 can run in parallel after the shared visibility service exists.
```

## Implementation Strategy

1. Deliver US1 first as the MVP because it establishes the complete Admin DTO, validation, audit, and atomic update path.
2. Add teacher discovery hiding, then content hiding/access denial, keeping the two persisted states independent.
3. Run critique and guards in the mandated order, then execute the full feature matrix and Docker gate before reporting readiness.
