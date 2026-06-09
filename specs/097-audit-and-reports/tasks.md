# Tasks: Audit Trail, KPI Dashboards, and Reports

**Input**: Design documents from `/specs/097-audit-and-reports/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Spec Kit Preparation Workflow

- [x] S001 Phase 1: Feature Specification (`speckit-specify`) complete
- [x] S002 Phase 2: Technical Planning (`speckit-plan`) complete
- [x] S003 Phase 3: Detailed Task Breakdown (`speckit-tasks`) complete

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure.

- [x] T001 Create folders for reports CQRS queries under `backend/src/NaderGorge.Application/Features/Admin/Reports/` and reports page folder under `frontend/src/app/admin/reports/`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T002 Create the report client service client in `frontend/src/services/report-service.ts`.
- [x] T003 [P] Add audit logging call (`_db.AuditLogs.Add(...)`) to `CreateTaskCommand.cs` handler in `backend/src/NaderGorge.Application/Features/Operations/Commands/CreateTaskCommand.cs`.
- [x] T004 [P] Add audit logging call to `UpdateTaskStatusCommand.cs` handler in `backend/src/NaderGorge.Application/Features/Operations/Commands/UpdateTaskStatusCommand.cs`.
- [x] T005 [P] Add audit logging call to `AdminResolveApprovalCommand.cs` handler in `backend/src/NaderGorge.Application/Features/Operations/Commands/AdminResolveApprovalCommand.cs`.
- [x] T006 [P] Add audit logging call to `LogCrmCallCommand.cs` handler in `backend/src/NaderGorge.Application/Features/CRM/Commands/LogCrmCallCommand.cs`.
- [x] T007 [P] Add audit logging call to `AssignStudentToAgentCommand.cs` handler in `backend/src/NaderGorge.Application/Features/CRM/Commands/AssignStudentToAgentCommand.cs`.
- [x] T008 [P] Add audit logging call to `ActivateCodeCommand.cs` handler in `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs`.
- [x] T009 [P] Add audit logging call to `CreateMediaPipelineCommand.cs` handler in `backend/src/NaderGorge.Application/Features/Admin/Media/Commands/CreateMediaPipelineCommand.cs`.
- [x] T010 [P] Add audit logging call to `UpdateMediaPipelineCommand.cs` handler in `backend/src/NaderGorge.Application/Features/Admin/Media/Commands/UpdateMediaPipelineCommand.cs`.
- [x] T011 [P] Add audit logging call to `CreateSocialPlanCommand.cs` handler in `backend/src/NaderGorge.Application/Features/Admin/Media/Commands/CreateSocialPlanCommand.cs`.

**Checkpoint**: Foundational audit logs are in place across all target operations subsystems.

---

## Phase 3: User Story 1 - Central Audit Trail (Priority: P1)

**Goal**: Allow Admins/Supervisors to view, search, and filter a central audit log of all sensitive actions.

**Independent Test**: Admin modifies a record (e.g. adjusts a student balance), visits the Audit Trail tab, and verifies a corresponding entry with IP, PerformedBy, Action, and old/new values JSON is displayed correctly.

### Tests for User Story 1 (MANDATORY)

- [x] T012 [P] [US1] Create unit tests in `backend/tests/NaderGorge.Application.Tests/Reports/AuditTrailTests.cs` verifying audit query filtering and sensitive payload redaction.

### Implementation for User Story 1

- [x] T013 [US1] Implement MediatR query `GetAdminAuditLogsQuery` in `backend/src/NaderGorge.Application/Features/Admin/Reports/Queries/GetAdminAuditLogsQuery.cs` with filters and pagination.
- [x] T014 [US1] Create reports controller `AdminReportsController.cs` in `backend/src/NaderGorge.API/Controllers/AdminReportsController.cs` with authorization check and route `/api/admin/reports/audit`.
- [x] T015 [US1] Build audit log history view (table, filters, and JSON payload viewer modal) inside `frontend/src/app/admin/reports/page.tsx`.

**Checkpoint**: User Story 1 central audit trail is fully functional and testable on the UI.

---

## Phase 4: User Story 2 - Operational KPI Dashboard (Priority: P1)

**Goal**: Provide Admins/Supervisors with a cockpit displaying attendance rates, task counts, CRM outcomes, media delays, and payment matching rates.

**Independent Test**: Seed a set of CRM call outcomes and employee attendance logs. Verify that charts on the reports dashboard dynamically load and recalculate accurately when date filters are applied.

### Tests for User Story 2 (MANDATORY)

- [x] T016 [P] [US2] Create unit tests in `backend/tests/NaderGorge.Application.Tests/Reports/KpiDashboardTests.cs` verifying mathematical aggregates for attendance rates, media delays, and task ratios.

### Implementation for User Story 2

- [x] T017 [US2] Implement MediatR query `GetAdminKpiDashboardQuery` in `backend/src/NaderGorge.Application/Features/Admin/Reports/Queries/GetAdminKpiDashboardQuery.cs` calculating metrics from the database.
- [x] T018 [US2] Add KPI route `/api/admin/reports/kpi` in `backend/src/NaderGorge.API/Controllers/AdminReportsController.cs`.
- [x] T019 [US2] Build visual cockpit dashboard (charts, cards, date filters) inside `frontend/src/app/admin/reports/page.tsx`.

**Checkpoint**: User Story 2 KPI Cockpit displays dynamic charts and statistics.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [x] T020 Document reports endpoints in `PRODUCT.md` and `DESIGN.md`.
- [x] T021 Add Python integration test in `tests/test_audit_and_reports.py` checking permissions and date range filtering.

---

## Phase 6: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Verify the complete feature complies with linting, compilation, tests, and security restrictions.

- [x] T022 Run `clean-code-guard` against all changed production C# and TypeScript code.
- [x] T023 Run `test-guard` against C# and Python test files.
- [x] T024 Run backend tests: `dotnet test backend/tests/NaderGorge.Application.Tests --filter FullyQualifiedName~Reports`.
- [x] T025 Run Python tests: `tests/venv/bin/python -m pytest tests/test_audit_and_reports.py -v`.
- [x] T026 Run frontend linter: `npm run lint` inside `frontend`.
- [x] T027 Run frontend TypeScript checks: `npx tsc --noEmit` inside `frontend`.
- [x] T028 Run API Inventory update script: `node scripts/generate-endpoint-inventory.mjs`.
- [x] T029 Write Phase 10 walkthrough report and update achievements progress tracking to 100%.

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories.
- **User Stories (Phases 3-4)**: Depends on Foundational completion.
- **Polish (Phase 5)**: Depends on User Stories.
- **Verification (Phase 6)**: Blocks completion until both quality gates (`clean-code-guard` and `test-guard`) are complete and verified.
