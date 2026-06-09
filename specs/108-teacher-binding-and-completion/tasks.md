# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in [spec.md](./spec.md)
- [x] Phase 2: Technical Planning (`speckit-plan`) completed in [plan.md](./plan.md)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed in [tasks.md](./tasks.md)

---

# Tasks: Teacher Binding and Teacher/Student Completion

**Input**: Design documents from `/specs/108-teacher-binding-and-completion/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: E2E domain surface isolation checks and backend build checks are mandatory.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup and API Contracts

**Purpose**: Set up teacher service calls and query/command definitions.

- [ ] T001 [P] [US1] Add endpoints to `frontend/src/services/teacher-service.ts` for:
  - `getDashboardStats`
  - `getStudents`
  - `getEssays`
  - `gradeEssay`
  - `getMyProfile`
  - `updateMyProfile`
- [ ] T002 [P] [US1] Define C# queries and commands in `backend/src/NaderGorge.Application/Features/Teacher/`:
  - `GetTeacherDashboardStatsQuery`: Query returning active students count, packages count, exams count, and pending essays count.
  - `GetTeacherStudentsQuery`: Query returning list of students who activated packages for the current teacher.
  - `GetPendingTeacherEssaysQuery`: Query returning essay submissions awaiting grading.
  - `GetTeacherProfileQuery`: Query returning profile details.
  - `UpdateTeacherProfileCommand`: Command updating bio, specialization, contact info, and avatar URL.

---

## Phase 2: Controller & Fallback Prevention

**Purpose**: Implement the backend API endpoints and secure bindings.

- [ ] T003 [US2] Create C# controller `backend/src/NaderGorge.API/Controllers/TeacherController.cs` exposing the workspace actions and mapping them to their corresponding MediatR commands/queries.
- [ ] T004 [US2] In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs`, modify `CreateInlineExamCommandHandler` to remove fallbacks. Return `ApiResponse.Fail` if the teacher or subject cannot be resolved.
- [ ] T005 [US2] In `backend/src/NaderGorge.Application/Features/Admin/Commands/BulkGenerateCodesCommand.cs`, modify `BulkGenerateCodesCommandHandler` to return `ApiResponse.Fail` if `groupTeacherId` evaluates to Guid.Empty, preventing defaulting.

---

## Phase 3: Frontend Pages - Teacher Dashboard & Profile

**Purpose**: Complete dashboard stats integration and profile edit page.

- [ ] T006 [US1] In `frontend/src/app/teacher/page.tsx`, replace static cards with dynamic state variables fetched from `/api/v1/teacher/dashboard/stats`. Ensure RTL rendering with Tajawal/Cairo fonts.
- [ ] T007 [US3] Create `frontend/src/app/teacher/profile/page.tsx` rendering a form to edit biography, specialization, contact details, and avatar image.

---

## Phase 4: Frontend Pages - Students & Essay Grading

**Purpose**: Implement roster viewing and essay grading workspace.

- [ ] T008 [US3] Create `frontend/src/app/teacher/students/page.tsx` rendering a table of active students.
- [ ] T009 [US3] Create `frontend/src/app/teacher/essays/page.tsx` rendering a list of essays with status `WaitTeacher`. Clicking a submission opens an inline grading form to input final score and feedback.

---

## Phase 5: Student Branding

**Purpose**: Display the teacher's profile on package details and lesson pages.

- [ ] T010 [US4] Update `frontend/src/app/student/packages/[packageId]/page.tsx` to display an "About the Teacher" card.

---

## Phase 6: Polish and Build Verification

**Purpose**: Run checks to verify code formatting and compilation.

- [ ] T011 Run Next.js linting and build checks: `npm run lint` and `npm run build` in `frontend/`.
- [ ] T012 Run C# backend build check: `dotnet build` in `backend/`.
- [ ] T013 Run backend unit tests: `dotnet test`.

---

## Phase 7: Quality Gates (Mandatory)

**Purpose**: Run Clean Code Guard and Test Guard.

- [ ] T014 Run `clean-code-guard` on all modified/added production-code files.
- [ ] T015 Run `test-guard` on test suites.
- [ ] T016 Run Python-based surface validation scripts.

---

## Phase 8: End-of-Phase Verification & Docker Gate

**Purpose**: Final run check inside Docker stack.

- [ ] T017 Run `docker compose config -q`.
- [ ] T018 Run `make up` and check service health.
- [ ] T019 Perform manual QA of the teacher dashboard, profile editing, and essay grading flows. Write the final walkthrough and achievements report.
