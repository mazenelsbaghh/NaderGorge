# Tasks: Payroll, Teacher Finance, and Activated Code Accounting

**Input**: Design documents from `/specs/096-payroll-accounting/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

---

## Spec Kit Preparation Workflow

- [x] S001 Phase 1: Feature Specification (`speckit-specify`) complete
- [x] S002 Phase 2: Technical Planning (`speckit-plan`) complete
- [x] S003 Phase 3: Detailed Task Breakdown (`speckit-tasks`) complete

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure.

- [x] T001 Create project folders and file stubs for Payroll, Teacher Finance, and Code Accounting.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T002 Create domain enums: `PayrollStatus` (Draft, Approved) in `backend/src/NaderGorge.Domain/Enums/PayrollStatus.cs`, `PayrollAdjustmentType` (Addition, Deduction) in `backend/src/NaderGorge.Domain/Enums/PayrollAdjustmentType.cs`, and `PayoutStatus` (Pending, Paid, Rejected) in `backend/src/NaderGorge.Domain/Enums/PayoutStatus.cs`.
- [x] T003 Create domain entity `PayrollRecord` in `backend/src/NaderGorge.Domain/Entities/PayrollRecord.cs`.
- [x] T004 Create domain entity `PayrollAdjustment` in `backend/src/NaderGorge.Domain/Entities/PayrollAdjustment.cs`.
- [x] T005 Create domain entity `TeacherAccount` in `backend/src/NaderGorge.Domain/Entities/TeacherAccount.cs`.
- [x] T006 Create domain entity `TeacherPayout` in `backend/src/NaderGorge.Domain/Entities/TeacherPayout.cs`.
- [x] T007 Create domain entity `AccessCodeActivationLog` in `backend/src/NaderGorge.Domain/Entities/AccessCodeActivationLog.cs`.
- [x] T008 Add DbSets for the five new entities to `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`.
- [x] T009 Add DbSets and configure OnModelCreating mappings (primary keys, relationships, decimal precision (18,2), unique indexes) in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T010 Add EF Core Migration for the new finance/payroll tables.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Monthly Employee Payroll Management (Priority: P1)

**Goal**: Allow Admins/Supervisors to manage employee monthly payroll, add additions/deductions with clear reasons, and approve them.

**Independent Test**: Admin generates payroll for a month/year, adds a deduction, verifies Net Salary is recalculated, and approves it to make the record read-only.

### Tests for User Story 1 (MANDATORY)

- [x] T011 [P] [US1] Create unit tests in `backend/tests/NaderGorge.Application.Tests/Finance/PayrollTests.cs` verifying payroll salary calculation (Basic + Additions - Deductions) and locked month behavior.

### Implementation for User Story 1

- [x] T012 [US1] Implement MediatR CQRS commands/queries for Payroll in `backend/src/NaderGorge.Application/Features/Admin/Finance/Commands/` & `Queries/` (GeneratePayrollCommand, AddPayrollAdjustmentCommand, DeletePayrollAdjustmentCommand, ApprovePayrollCommand, GetPayrollQuery).
- [x] T013 [US1] Implement `AdminFinanceController` with payroll endpoints in `backend/src/NaderGorge.API/Controllers/AdminFinanceController.cs`.
- [x] T014 [US1] Build frontend service functions for payroll in `frontend/src/services/finance-service.ts`.
- [x] T015 [US1] Create Admin payroll management page at `frontend/src/app/admin/finance/page.tsx` showing monthly employee listings, adjustment drawer, and approval button.

**Checkpoint**: User Story 1 is fully functional and testable independently.

---

## Phase 4: User Story 2 - Teacher Account Balance & Commission Tracking (Priority: P1)

**Goal**: Allow Teachers to view their total earnings, current balance, commission rate, and transaction ledger.

**Independent Test**: Teacher opens finance dashboard, views correct balance and list of activated codes.

### Tests for User Story 2 (MANDATORY)

- [x] T016 [P] [US2] Create unit tests in `backend/tests/NaderGorge.Application.Tests/Finance/CommissionTests.cs` verifying teacher data isolation boundaries.

### Implementation for User Story 2

- [x] T017 [US2] Implement MediatR CQRS queries for Teacher Finance in `backend/src/NaderGorge.Application/Features/Teacher/Finance/Queries/` (GetTeacherAccountQuery, GetTeacherTransactionsQuery).
- [x] T018 [US2] Implement `TeacherFinanceController` with account and transactions endpoints in `backend/src/NaderGorge.API/Controllers/TeacherFinanceController.cs`.
- [x] T019 [US2] Build frontend service functions for teacher account in `frontend/src/services/finance-service.ts`.
- [x] T020 [US2] Create Teacher dashboard page at `frontend/src/app/teacher/finance/page.tsx` showing current balance, total earnings, commission rate, and transaction ledger.

**Checkpoint**: User Story 2 is fully functional and testable independently.

---

## Phase 5: User Story 3 - Teacher Payout Requests (Priority: P2)

**Goal**: Allow teachers to request payouts from their balance, and admins to approve or reject requests.

**Independent Test**: Teacher submits payout request, which appears as Pending on Admin panel. Admin approves payout, teacher balance decreases.

### Tests for User Story 3 (MANDATORY)

- [x] T021 [P] [US3] Create unit tests checking double-payout prevention and validation of request amount vs balance.

### Implementation for User Story 3

- [x] T022 [US3] Implement MediatR CQRS handlers for Payouts (RequestPayoutCommand, ResolvePayoutCommand, GetPayoutsQuery, GetTeacherPayoutsQuery).
- [x] T023 [US3] Add payout endpoints to `AdminFinanceController` and `TeacherFinanceController`.
- [x] T024 [US3] Build frontend service functions for payouts in `frontend/src/services/finance-service.ts`.
- [x] T025 [US3] Add Admin Payout Requests tab/component to `/admin/finance/page.tsx` and Payout Request modal to `/teacher/finance/page.tsx`.

**Checkpoint**: User Story 3 is fully functional and testable independently.

---

## Phase 6: User Story 4 - Activated Access Code Accounting & Reconciliations (Priority: P2)

**Goal**: Calculate and credit teacher commissions automatically on access code activation, and provide reconciliations.

**Independent Test**: Student activates access code. Teacher balance increases, and activation log appears in admin code-accounting.

### Tests for User Story 4 (MANDATORY)

- [x] T026 [P] [US4] Create unit test verifying commission calculation (Item Price * CommissionRate) discounted by code group discount.

### Implementation for User Story 4

- [x] T027 [US4] Modify `ActivateCodeCommandHandler.cs` to calculate, lock, and credit teacher commission at activation time in transaction.
- [x] T028 [US4] Implement MediatR CQRS query for Admin Reconciliations (GetCodeAccountingQuery).
- [x] T029 [US4] Add `code-accounting` endpoint to `AdminFinanceController`.
- [x] T030 [US4] Add Code Accounting tab/component to `/admin/finance/page.tsx` for reconciliations.

**Checkpoint**: User Story 4 is fully functional and testable independently.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T031 Document payroll and teacher finance endpoints in `PRODUCT.md` and `DESIGN.md`.
- [x] T032 Add a Python API integration test file `tests/test_teacher_finance.py` verifying permissions and tenant isolation.

---

## Phase 8: Quality Gates & End-of-Phase Verification

**Purpose**: Run C# and TypeScript code audits, unit tests, and Docker migration runs to ensure no regression.

- [x] T033 Run `clean-code-guard` against all modified/created C# and TypeScript code. Resolve and check off all findings.
- [x] T034 Run `test-guard` against all C# and Python test files. Resolve and check off all findings.
- [x] T035 Run `make up` and `make migrate` to verify Docker environment.
- [x] T036 Run backend tests (`dotnet test backend/tests/NaderGorge.Application.Tests --filter Category=Finance`) and Python tests (`python3 -m pytest tests/test_teacher_finance.py -v`).
- [x] T037 Perform manual QA checklist for Payroll, Teacher Finance, Payouts, and Code Activation.
- [x] T038 Write end-of-phase report and update `achievements.md` to 100% completion.

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories.
- **User Stories (Phases 3-6)**: Depends on Foundational completion.
- **Polish (Phase 7)**: Depends on User Stories completion.
- **Verification (Phase 8)**: Depends on all implementation and polish tasks.
