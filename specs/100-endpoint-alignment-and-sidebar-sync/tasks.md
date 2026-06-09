# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Endpoint Alignment and Sidebar Sync

**Input**: Design documents from `specs/100-endpoint-alignment-and-sidebar-sync/`
**Prerequisites**: [plan.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/100-endpoint-alignment-and-sidebar-sync/plan.md), [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/100-endpoint-alignment-and-sidebar-sync/spec.md)

## Phase 1: Setup & Pre-requisites

- [x] T001 Verify Docker container status via `docker compose ps` and check git branch is `100-endpoint-alignment-and-sidebar-sync`

## Phase 2: Foundational (Blocking Prerequisites)

- [x] T002 Verify that local environment variables (`NEXT_PUBLIC_API_URL` etc.) are loaded correctly in `.env` and that backend endpoints respond.

## Phase 3: User Story 1 - Sidebar Navigation Completeness (Priority: P1)

**Goal**: Add the 4 missing admin links to the sidebar and enable vertical scrolling in the mobile menu modal.
**Independent Test**: Log in as Admin and verify that all 4 links appear and redirect to their correct URL paths.

- [x] T003 [P] [US1] Import `Library`, `GraduationCap`, `Coins`, and `Users` from `lucide-react` in [AdminShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AdminShellChrome.tsx).
- [x] T004 [US1] Add `/admin/subjects`, `/admin/teachers`, `/admin/finance`, and `/admin/hr` to the `AdminShellRoute` type in [AdminShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AdminShellChrome.tsx).
- [x] T005 [US1] Add the 4 missing navigation configurations to the `navItems` array in [AdminShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AdminShellChrome.tsx) with permissions (`content.manage` for subjects, `users.manage` for teachers/finance, `hr.manage` for hr) and their respective icons.
- [x] T006 [US1] Add Tailwind classes `max-h-[80vh] overflow-y-auto` to the mobile menu `<aside>` container in [AdminShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AdminShellChrome.tsx) to enable vertical scroll when elements overflow.

## Phase 4: User Story 2 - Syncing DTOs & Enum Normalization (Priority: P1)

**Goal**: Audit and synchronize property casings between C# backend and TS frontend DTOs and verify enums parse safely.
**Independent Test**: Submit/fetch payloads across the 4 newly added areas and verify no serialization errors occur.

- [x] T007 [P] [US2] Review and verify `SubjectDto` and `TeacherDto` schemas in [teacher-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/teacher-service.ts) against endpoints in `AdminController.cs`.
- [x] T008 [P] [US2] Review and verify `EmployeeProfileDto`, `EmployeeDto`, `AttendanceLogDto`, and `VacationDto` in [hr-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/hr-service.ts) against `AdminHrController.cs` and `HrController.cs`.
- [x] T009 [P] [US2] Review and verify `PayrollRecordDto`, `AdminPayoutDto`, `TeacherAccountDto`, and `TeacherPayoutDto` in [finance-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/finance-service.ts) against `AdminFinanceController.cs` and `TeacherFinanceController.cs`.
- [x] T010 [P] [US2] Inspect frontend badge rendering components for status and priority enums to ensure both string values (e.g. `"New"`, `"Pending"`) and numeric IDs (e.g. `1`, `2`) are handled.

## Phase 5: Polish & Build Verification

- [x] T011 Rebuild the Next.js admin frontend and ensure zero build/typescript compilation warnings.
- [x] T012 Run the end-to-end endpoint test script `python3 scratch/test_all_endpoints.py` using `.venv` to verify all backend routes are accessible and return valid response status codes.

## Phase 6: Quality Gates (Mandatory)

- [x] T013 Run `clean-code-guard` against all modified frontend files and resolve any reported findings.
- [x] T014 Run `test-guard` to review and verify test files and run `pytest tests/test_operations_tasks.py`.

## Phase 7: End-of-Phase Verification & Docker Gate

- [x] T015 Verify container health using `docker compose ps` and run service health checks.
- [x] T016 Perform manual QA verification:
  - Log in as Admin (`20000000000`/`password`), verify the sidebar displays the 4 links.
  - Log in as Student (`20000000001`/`password`), verify that none of these links are visible or accessible.
- [x] T017 Update achievements.md to mark all phases completed, and draft the final walkthrough.md report.
