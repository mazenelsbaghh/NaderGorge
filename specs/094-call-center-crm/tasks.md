# Tasks: Call Center CRM and Student Follow-Up

**Input**: Design documents from `/specs/094-call-center-crm/`  
**Prerequisites**: plan.md (required), spec.md (required)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

---

## Technical Implementation Checklist

### 1. Database & Foundational Setup (Backend)

- [x] T001 Create CRM Enums in `backend/src/NaderGorge.Domain/Enums/`:
  - `CrmStatus.cs`: `Unassigned = 0`, `Assigned = 1`, `InProgress = 2`, `Cold = 3`, `Closed = 4`
  - `CrmPriority.cs`: `Low = 0`, `Medium = 1`, `High = 2`, `Critical = 3`
  - `CallOutcome.cs`: `Completed = 0`, `Pending = 1`, `NoAnswer = 2`, `Postponed = 3`, `Closed = 4`
- [x] T002 Create `CrmStudentStatus.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/` containing fields:
  - `StudentId` (Guid, PK), `Status` (`CrmStatus`), `AssignedAgentId` (Guid?, Nullable), `Priority` (`CrmPriority`), `NextFollowUpDate` (DateTime?), `LastCalledAt` (DateTime?), `Notes` (string?).
- [x] T003 Create `CrmCallLog.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/` containing fields:
  - `Id` (Guid, PK), `StudentId` (Guid), `AgentId` (Guid), `CallDate` (DateTime), `Outcome` (`CallOutcome`), `Notes` (string?), `NextFollowUpDate` (DateTime?).
- [x] T004 Register DbSet properties in `IAppDbContext.cs` and configure Fluent mappings in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` (setup Keys, Foreign Key relations with Users, Cascade deletes, and index fields).
- [x] T005 Run database migrations setup commands:
  - `dotnet ef migrations add AddCrmEntities --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API`
  - `dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API`

### 2. User Story 1 - Call List Assignment and Follow-Up Queue

- [x] T006 [P] [US1] Implement MediatR command `AssignStudentToAgentCommand.cs` in `backend/src/NaderGorge.Application/Features/CRM/Commands/`:
  - Validates that the student user has the `Student` role, and the assigned agent has a staff/admin role. Creates or updates the `CrmStudentStatus` record.
- [x] T007 [P] [US1] Implement MediatR query `GetCrmStudentsQuery.cs` in `backend/src/NaderGorge.Application/Features/CRM/Queries/`:
  - Returns paginated list of students and their CRM status. Applies row-level filter: if user is not Admin/Supervisor, filters by `AssignedAgentId = CurrentUserId`. Supports searching, priority sorting, and `onlyOverdue` filter.
- [x] T008 [US1] Implement API controller endpoints in `backend/src/NaderGorge.API/Controllers/CrmController.cs`:
  - `GET /api/crm/students` mapped to `GetCrmStudentsQuery`.
  - `POST /api/crm/students/{studentId}/assign` mapped to `AssignStudentToAgentCommand`.
  - Protects the controller with `[Authorize(Roles = "Admin,Supervisor,Assistant,Teacher,Staff")]` and checks permission claims for `crm.manage`.
- [x] T009 [P] [US1] Create frontend REST client service `crm-service.ts` in `frontend/src/services/crm-service.ts` with API call methods:
  - `getStudents(params)`, `assignStudent(studentId, payload)`.
- [x] T010 [US1] Create frontend pages and sidebar items:
  - Page `frontend/src/app/admin/crm/page.tsx` for manager dashboard (full list, agent selector dropdowns).
  - Page `frontend/src/app/assistant/crm/page.tsx` for call center agent dashboard (personal queue list).
  - Add CRM links to `AdminShellChrome.tsx` and sidebar navigation files.

### 3. User Story 2 - Logging Calls and Scheduling Reschedules

- [x] T011 [P] [US2] Implement MediatR command `LogCrmCallCommand.cs` in `backend/src/NaderGorge.Application/Features/CRM/Commands/`:
  - Saves the `CrmCallLog` entry. Updates the `CrmStudentStatus` status to `InProgress` (or `Closed` depending on outcome), set `LastCalledAt = UtcNow`, and updates `NextFollowUpDate`.
- [x] T012 [P] [US2] Implement MediatR query `GetCrmStudentHistoryQuery.cs` in `backend/src/NaderGorge.Application/Features/CRM/Queries/`:
  - Retrieves chronological history of call logs for a student (checks that the requesting agent is assigned to the student or is Admin/Supervisor).
- [x] T013 [US2] Expose endpoints in `backend/src/NaderGorge.API/Controllers/CrmController.cs`:
  - `POST /api/crm/students/{studentId}/calls` mapped to `LogCrmCallCommand`.
  - `GET /api/crm/students/{studentId}/history` mapped to `GetCrmStudentHistoryQuery`.
- [x] T014 [P] [US2] Update `crm-service.ts` in `frontend/src/services/crm-service.ts` with methods:
  - `logCall(studentId, payload)`, `getCallHistory(studentId)`.
- [x] T015 [US2] Implement UI Components in `frontend/src/components/crm/`:
  - `CrmCallLogModal.tsx`: Form to select outcome, write notes, and select future reschedule date.
  - `CrmCallHistoryTimeline.tsx`: Detailed audit timeline of calls.

### 4. User Story 3 - Quick Communication Actions

- [x] T016 [P] [US3] Create phone number utility helper `frontend/src/utils/phone-utils.ts` to normalize Egyptian mobile numbers to E.164 (`+20...`) format.
- [x] T017 [US3] Integrate quick WhatsApp action buttons inside `CrmStudentQueue.tsx` card views:
  - Computes redirect link using the utility helper and templates pre-filled greeting text.

### 5. User Story 4 - Agent Performance and CRM Metrics Dashboard

- [x] T018 [P] [US4] Implement MediatR query `GetCrmPerformanceReportQuery.cs` in `backend/src/NaderGorge.Application/Features/CRM/Queries/`:
  - Aggregates metrics (Total calls, outcome counts, calls per agent) for supervisor analytics dashboard.
- [x] T019 [US4] Expose endpoint `GET /api/crm/reports/performance` in `CrmController.cs` (restricted to Admin/Supervisor roles).
- [x] T020 [P] [US4] Update `crm-service.ts` in `frontend/src/services/crm-service.ts` with `getPerformanceReport()` api method.
- [x] T021 [US4] Create reports dashboard panel `CrmReportsPanel.tsx` in `frontend/src/components/crm/CrmReportsPanel.tsx` displaying statistics tables and graphs.

### 6. Verification & Quality Gates

- [x] T022 [P] Write backend access control and logic tests in `backend/tests/NaderGorge.Application.Tests/CRM/`:
  - `CrmSecurityTests.cs`: Asserts that non-assigned assistants are blocked from querying student history, and student roles are blocked from all endpoints.
  - `CrmCallLogTests.cs`: Tests correct transition of CrmStudentStatus when call logs are registered.
- [x] T023 Run `clean-code-guard` against all modified/created files.
- [x] T024 Run `test-guard` against all test files.
- [x] T025 Run full compilation and validation checks:
  - `dotnet build backend/` (verify 0 warnings/errors)
  - `npm run lint` inside `frontend/` (verify 0 warnings/errors)
  - `npm run build` inside `frontend/` (verify successful Next.js build)
