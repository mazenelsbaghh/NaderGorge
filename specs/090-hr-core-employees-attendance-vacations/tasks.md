# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: HR Core - Employees, Attendance, Vacations

**Input**: Design documents from `/specs/090-hr-core-employees-attendance-vacations/`
**Prerequisites**: plan.md (required), spec.md (required)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files/commands, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Path Conventions

- **Web app**: `backend/src/`, `frontend/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and specs verification

- [x] T001 Verify specs directory and configuration file `.specify/feature.json` points to the correct directory path.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model extensions and database tables that MUST be complete before user stories can start

- [x] T002 Backend Domain: Create `EmployeeProfile.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/EmployeeProfile.cs` storing `UserId`, `BasicSalary`, `StandardStartTime` (TimeSpan), and `TargetDailyHours` (int).
- [x] T003 Backend Domain: Create `AttendanceStatus.cs` enum in `backend/src/NaderGorge.Domain/Enums/AttendanceStatus.cs` (Present=1, Late=2, Absent=3, Sick=4, Leave=5).
- [x] T004 Backend Domain: Create `AttendanceLog.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/AttendanceLog.cs` storing `EmployeeId`, `Date` (DateOnly), `ClockIn` (DateTime), `ClockOut` (DateTime?), `LateMinutes` (int), `Status` (enum), `IpAddress` (string), and `UserAgent` (string).
- [x] T005 Backend Domain: Create `VacationStatus.cs` enum in `backend/src/NaderGorge.Domain/Enums/VacationStatus.cs` (Pending=1, Approved=2, Rejected=3).
- [x] T006 Backend Domain: Create `EmployeeVacation.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/EmployeeVacation.cs` storing `EmployeeId`, `StartDate` (DateOnly), `EndDate` (DateOnly), `Status` (enum), `Reason` (string), `HandledBy` (Guid?), and `HandledAt` (DateTime?).
- [x] T007 Backend Infrastructure: Register DbSets for the three new entities in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`. Configure a unique index on `EmployeeProfile.UserId` and FK indexes.
- [x] T008 Database Migration: Generate an EF Core migration named `AddHREntities` by running `dotnet ef migrations add AddHREntities --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API`.

**Checkpoint**: Foundation ready - user story database tables and domain objects are established.

---

## Phase 3: User Story 1 - Employee Profile Setup & Management (Priority: P1) 🎯 MVP

**Goal**: Expose administration endpoints to configure and persist job metrics for staff.

**Independent Test**: Verify that Admin can set salary/hours details and query them via API.

### Tests for User Story 1
- [x] T009 [P] [US1] Application Tests: Create unit test in `backend/tests/NaderGorge.Application.Tests/HR/EmployeeProfileTests.cs` to assert duplicate profile links throw validation errors.
- [x] T010 [P] [US1] Backend Application: Create `AdminSaveEmployeeProfileCommand.cs` in `backend/src/NaderGorge.Application/Features/HR/Commands/AdminSaveEmployeeProfileCommand.cs` to create/update settings. Add FluentValidation.
- [x] T011 [P] [US1] Backend Application: Create `AdminGetEmployeesQuery.cs` in `backend/src/NaderGorge.Application/Features/HR/Queries/AdminGetEmployeesQuery.cs` to list all registered staff.
- [x] T012 [US1] Backend API: Implement `AdminHrController.cs` in `backend/src/NaderGorge.API/Controllers/AdminHrController.cs` protected by `[HasPermission("hr.manage")]`. Add save/list endpoints.
- [x] T013 [P] [US1] Frontend Service: Create REST client service `frontend/src/services/hr-service.ts` exposing endpoints to administrative commands.
- [x] T014 [US1] Frontend Component: Build `EmployeeProfileDrawer.tsx` in `frontend/src/components/admin/EmployeeProfileDrawer.tsx` containing fields for Basic Salary, standard start time, and daily hour target.
- [x] T015 [US1] Frontend Integration: Wire the configuration drawer trigger button inside the user management table `frontend/src/app/admin/users/page.tsx` for staff roles.

**Checkpoint**: User Story 1 employee profiles management is fully functional.

---

## Phase 4: User Story 2 - Employee Attendance Logging (Clock-in/out) (Priority: P1) 🎯 MVP

**Goal**: Enable staff to clock in, compute late durations, and record work hours.

**Independent Test**: Login as employee, register clock-in and clock-out, then review calculation accuracy.

### Tests for User Story 2
- [x] T016 [P] [US2] Application Tests: Create unit tests in `backend/tests/NaderGorge.Application.Tests/HR/AttendanceTests.cs` validating late minutes calculations and double clock-in preventions.

### Implementation for User Story 2
- [x] T017 [US2] Backend Application: Create `ClockInCommand.cs` in `backend/src/NaderGorge.Application/Features/HR/Commands/ClockInCommand.cs` resolving user profiles, evaluating standard start times, and logging late variables.
- [x] T018 [US2] Backend Application: Create `ClockOutCommand.cs` in `backend/src/NaderGorge.Application/Features/HR/Commands/ClockOutCommand.cs` confirming active sessions, logging out timestamps, and calculating elapsed minutes.
- [x] T019 [P] [US2] Backend Application: Create query `GetMyAttendanceQuery.cs` in `backend/src/NaderGorge.Application/Features/HR/Queries/GetMyAttendanceQuery.cs`.
- [x] T020 [P] [US2] Backend Application: Create query `AdminGetAttendanceQuery.cs` in `backend/src/NaderGorge.Application/Features/HR/Queries/AdminGetAttendanceQuery.cs`.
- [x] T021 [US2] Backend API: Implement `HrController.cs` in `backend/src/NaderGorge.API/Controllers/HrController.cs` containing self-service clock-in, clock-out, and log queries. Add search endpoint in `AdminHrController.cs`.
- [x] T022 [US2] Frontend Component: Create `ClockInOutWidget.tsx` in `frontend/src/components/admin/ClockInOutWidget.tsx` displaying interactive clock CTA buttons and active timer overlays.
- [x] T023 [P] [US2] Frontend Component: Create `AttendanceLogTable.tsx` in `frontend/src/components/admin/AttendanceLogTable.tsx` listing historic logs and status badges.
- [x] T024 [US2] Frontend Integration: Render the clock widget inside the Admin Home dashboard `/admin/page.tsx` and create history page `/admin/hr/my-attendance/page.tsx`.

**Checkpoint**: User Story 2 clock-in/out operations and late-minutes metrics are operational.

---

## Phase 5: User Story 3 - Vacation Request & Approvals (Priority: P2)

**Goal**: Model vacation workflows, approve requests, and populate attendance logs with vacation status.

**Independent Test**: Request vacation, approve as admin, verify attendance log has automatic vacation placeholders.

### Tests for User Story 3
- [x] T025 [P] [US3] Application Tests: Create unit tests in `backend/tests/NaderGorge.Application.Tests/HR/VacationTests.cs` asserting permission gates on approvals and automatic creation of vacation log leaves.

### Implementation for User Story 3
- [x] T026 [US3] Backend Application: Create `SubmitVacationCommand.cs` in `backend/src/NaderGorge.Application/Features/HR/Commands/SubmitVacationCommand.cs` with validation blocking overlapping dates.
- [x] T027 [US3] Backend Application: Create `AdminApproveVacationCommand.cs` in `backend/src/NaderGorge.Application/Features/HR/Commands/AdminApproveVacationCommand.cs` updating status and generating `Leave` status in `AttendanceLog` for the approved date range.
- [x] T028 [P] [US3] Backend Application: Create command `AdminRejectVacationCommand.cs` in `backend/src/NaderGorge.Application/Features/HR/Commands/AdminRejectVacationCommand.cs`.
- [x] T029 [P] [US3] Backend Application: Create queries `GetMyVacationsQuery.cs` and `AdminGetVacationsQuery.cs` inside `backend/src/NaderGorge.Application/Features/HR/Queries/`.
- [x] T030 [US3] Backend API: Connect vacation request endpoints in `HrController.cs` and approval/rejection endpoints in `AdminHrController.cs`.
- [x] T031 [US3] Frontend Component: Create `VacationRequestModal.tsx` in `frontend/src/components/admin/VacationRequestModal.tsx`.
- [x] T032 [US3] Frontend Page: Build `/admin/hr/page.tsx` displaying employee log details and vacation moderation tables.

**Checkpoint**: User Story 3 vacation requests and approval workflows are verified.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Performance, validation cleanup, and audit logging

- [x] T033 Integrate audit logs for sensitive HR actions (salary updates, attendance modifications, vacation approvals) inside the command handlers.
- [x] T034 Run code format checks across frontend and backend workspaces.

---

## Phase 7: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment before starting the next phase.

- [x] T035 Run backend build/test and frontend lint checks.
- [x] T036 Output the final verification report.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories.
- **User Stories (Phase 3+)**: All depend on Foundational phase completion.
- **Polish (Final Phase)**: Depends on all desired user stories being complete.
- **End-of-Phase Verification**: Depends on all implementation and polish tasks.
