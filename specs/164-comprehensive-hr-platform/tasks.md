# Tasks: منظومة الموارد البشرية المتكاملة

**Input**: `specs/164-comprehensive-hr-platform/{spec.md,plan.md,research.md,data-model.md,quickstart.md,contracts/}`

## Spec Kit Prep

- [x] Phase 1 — specification مكتملة ومؤكدة في `specs/164-comprehensive-hr-platform/spec.md`
- [x] Phase 2 — خمس توضيحات عربية مدمجة في `specs/164-comprehensive-hr-platform/spec.md`
- [x] Phase 3 — التخطيط والعقود ونموذج البيانات في `specs/164-comprehensive-hr-platform/plan.md`
- [x] Phase 4 — تفصيل المهام في `specs/164-comprehensive-hr-platform/tasks.md`

## Phase 1: Setup and baseline

- [x] T001 Capture dirty-worktree and migration-snapshot ownership in `specs/164-comprehensive-hr-platform/implementation-log.md` before product edits
- [x] T002 Run baseline `make verify` and record exact result in `specs/164-comprehensive-hr-platform/implementation-log.md`
- [x] T003 [P] Run `cd frontend && npm run lint && npm run build` and record exact result in `specs/164-comprehensive-hr-platform/implementation-log.md`
- [x] T004 [P] Inventory legacy HR/finance routes, query keys, permission maps and realtime consumers in `specs/164-comprehensive-hr-platform/implementation-log.md`
- [x] T005 Define wave flags and compatibility-removal checkpoints in `specs/164-comprehensive-hr-platform/implementation-log.md`

## Phase 2: Foundational safety — blocking

- [x] T006 Write failing regression proving approved future leave does not create an open attendance row in `backend/tests/NaderGorge.Application.Tests/HR/VacationTests.cs`
- [x] T007 Stop synthetic leave AttendanceLog creation and preserve leave as a separate fact in `backend/src/NaderGorge.Application/Features/HR/Commands/AdminApproveVacationCommand.cs`
- [x] T008 Write failing employee-list regression for teacher/admin exclusion in `backend/tests/NaderGorge.Application.Tests/HR/EmployeeProfileTests.cs`
- [x] T009 Query EmployeeProfiles as the workforce source of truth in `backend/src/NaderGorge.Application/Features/HR/Queries/AdminGetEmployeesQuery.cs`
- [x] T010 [P] Add current-actor and correlation abstractions in `backend/src/NaderGorge.Application/Common/HR/`
- [x] T011 Require actor/system identity and redacted before/after audit for HR mutations in `backend/src/NaderGorge.Application/Features/HR/`
- [x] T012 [P] Add HR idempotency entities and contracts in `backend/src/NaderGorge.Domain/Entities/HR/` and `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [x] T013 [P] Add module rollout and migration evidence entities in `backend/src/NaderGorge.Domain/Entities/HR/`
- [x] T014 Configure idempotency, rollout, audit retention and restrictive relationships in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [x] T015 Create reviewed EF migration for Wave 0 tables and delete-behavior corrections in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T016 [P] Define granular HR/payroll permission catalog in `backend/src/NaderGorge.Application/Common/HR/HrPermissions.cs`
- [x] T017 Apply handler-level permission and organization-scope behaviors in `backend/src/NaderGorge.Application/Common/HR/`
- [x] T018 Align API, navbar, route cards and settings to one permission map in `frontend/src/lib/hr-permissions.ts`, `frontend/src/components/admin/AdminShellChrome.tsx`, `frontend/src/app/admin/AdminRootPageClient.tsx`, and `frontend/src/app/admin/settings/AdminSettingsPageClient.tsx`
- [x] T019 Add PostgreSQL integration tests for idempotency, restrictive deletes and rollout single-writer invariants in `backend/tests/NaderGorge.Integration.Tests/HR/HrFoundationPostgresTests.cs`
- [x] T020 Run Wave 0 focused tests and record expected pass results in `specs/164-comprehensive-hr-platform/implementation-log.md`

## Phase 3: User Story 1 — Atomic employee creation (P1 MVP)

**Independent Test**: valid submit creates account/profile/assignment/contract/shift/balance/tasks; any invalid child or replay leaves exactly one complete employee and no partial rows.

- [x] T021 [P] [US1] Write transactional provisioning and replay tests in `backend/tests/NaderGorge.Application.Tests/HR/EmployeeProvisioningTests.cs`
- [x] T022 [P] [US1] Add EmployeeProfile lifecycle fields and EmployeeNumber rules in `backend/src/NaderGorge.Domain/Entities/EmployeeProfile.cs`
- [x] T023 [US1] Implement atomic CreateEmployee command and validation in `backend/src/NaderGorge.Application/Features/HR/Commands/CreateEmployeeCommand.cs`
- [x] T024 [US1] Expose `POST /api/admin/hr/employees/provision` with actor and idempotency headers in `backend/src/NaderGorge.API/Controllers/AdminHrController.cs`
- [x] T025 [US1] Configure employee uniqueness and non-cascading User relationship in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [x] T026 [US1] Add Wave 1 employee schema/backfill migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T027 [P] [US1] Add typed provisioning DTO/service in `frontend/src/services/hr-service.ts`
- [x] T028 [US1] Replace assistant account-only creation with a single account-and-profile form in `frontend/src/app/admin/users/components/AddUserDrawer.tsx`
- [x] T029 [US1] Route existing create-employee entry points to the wizard in `frontend/src/features/employee/useEmployeeQueries.ts` and `frontend/src/app/admin/users/components/AddUserDrawer.tsx`
- [x] T030 [US1] Add API authorization and browser journey tests in `backend/tests/NaderGorge.Application.Tests/HR/EmployeeProvisioningTests.cs` and `frontend/e2e/hr-employee-provisioning.spec.ts`

## Phase 4: User Story 2 — Organization, contracts and lifecycle (P1)

**Independent Test**: effective-dated transfer/contract change preserves previous record, prevents cycles/overlap/self-manager and disables future access on completed exit without deleting history.

- [x] T031 [P] [US2] Write organization cycle, assignment overlap and contract transition tests in `backend/tests/NaderGorge.Application.Tests/HR/OrganizationContractTests.cs`
- [x] T032 [P] [US2] Add organization, job, grade, location and cost-center entities in `backend/src/NaderGorge.Domain/Entities/HR/Organization/`
- [x] T033 [P] [US2] Add EmploymentAssignment and EmploymentContract history entities in `backend/src/NaderGorge.Domain/Entities/HR/People/`
- [x] T034 [US2] Implement organization scope resolver and cycle validation in `backend/src/NaderGorge.Application/Features/HR/Organization/`
- [x] T035 [US2] Implement transfer, promotion, contract and lifecycle commands in `backend/src/NaderGorge.Application/Features/HR/People/`
- [x] T036 [US2] Configure effective-date indexes and restrictive history relationships in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [x] T037 [US2] Add organization/contract EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T038 [US2] Expose scoped employee/organization/contract endpoints in `backend/src/NaderGorge.API/Controllers/HrOrganizationController.cs` and `backend/src/NaderGorge.API/Controllers/HrEmployeesController.cs`
- [x] T039 [P] [US2] Build organization tree and employee profile sections in `frontend/src/app/admin/hr/organization/` and `frontend/src/app/admin/hr/employees/[id]/`
- [x] T040 [US2] Add contract expiry/probation outbox notifications in `backend/src/NaderGorge.Application/Features/HR/People/HrLifecycleNotificationService.cs`

## Phase 5: User Story 3 — Shift planning (P1)

**Independent Test**: publish day/night/flexible/split shifts, reject overlap, attribute midnight correctly and approve a swap while retaining the original schedule.

- [x] T041 [P] [US3] Write shift segment, overnight, overlap and swap tests in `backend/tests/NaderGorge.Application.Tests/HR/ShiftTests.cs`
- [x] T042 [P] [US3] Add WorkCalendar, ShiftTemplate, ShiftSegment and ShiftAssignment entities in `backend/src/NaderGorge.Domain/Entities/HR/Scheduling/`
- [x] T043 [US3] Implement schedule validation/publish and work-date resolver in `backend/src/NaderGorge.Application/Features/HR/Scheduling/`
- [x] T044 [US3] Implement shift swap request integration with approvals in `backend/src/NaderGorge.Application/Features/HR/Scheduling/Commands/SubmitShiftSwapCommand.cs`
- [x] T045 [US3] Configure schedule constraints and create EF migration in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T046 [US3] Expose shift templates, validation, publish and swap APIs in `backend/src/NaderGorge.API/Controllers/HrShiftsController.cs`
- [x] T047 [P] [US3] Build shift calendar/editor/conflict UI in `frontend/src/app/admin/hr/shifts/`
- [x] T048 [US3] Add shift contract and responsive browser tests in `frontend/e2e/hr-shifts.spec.ts`

## Phase 6: User Story 4 — Trusted attendance (P1)

**Independent Test**: unrestricted/geofence/trusted-device policies accept or reject with evidence, remote exception wins, replay/concurrent clock-in creates one session and future leave never blocks today.

- [x] T049 [P] [US4] Write three-policy, remote, replay, concurrency and live-support regression tests in `backend/tests/NaderGorge.Application.Tests/HR/AttendancePolicyTests.cs`
- [x] T050 [P] [US4] Add AttendanceAttempt, AttendanceSession, AttendanceBreak, policy/device/exception entities in `backend/src/NaderGorge.Domain/Entities/HR/Attendance/`
- [x] T051 [US4] Implement attendance policy precedence and evidence evaluator in `backend/src/NaderGorge.Application/Features/HR/Attendance/AttendancePolicyEvaluator.cs`
- [x] T052 [US4] Replace legacy clock commands with session/break/work-date/idempotent commands in `backend/src/NaderGorge.Application/Features/HR/Attendance/Commands/`
- [x] T053 [US4] Preserve live-support assignment coordination on accepted clock state changes in `backend/src/NaderGorge.Application/Features/HR/Attendance/Commands/`
- [x] T054 [US4] Add partial open-session and source uniqueness migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T055 [US4] Expose self/admin attendance and trusted-device APIs in `backend/src/NaderGorge.API/Controllers/HrAttendanceController.cs`
- [x] T056 [P] [US4] Build shared self-service clock/break/error components in `frontend/src/features/hr/attendance/`
- [x] T057 [US4] Mount shared attendance in employee/admin/assistant routes in `frontend/src/app/employee/attendance/`, `frontend/src/app/admin/hr/my-attendance/`, and `frontend/src/app/assistant/attendance/`
- [x] T058 [US4] Add mobile/browser E2E for all policy outcomes in `frontend/e2e/hr-attendance.spec.ts`

## Phase 7: User Story 5 — Attendance correction and calculations (P1)

**Independent Test**: a missing clock correction follows manager then HR, applies once with before/after, rejection leaves the original and calculations follow effective policy.

- [x] T059 [P] [US5] Write correction transition/calculation tests in `backend/tests/NaderGorge.Application.Tests/HR/AttendanceCorrectionTests.cs`
- [x] T060 [P] [US5] Add WorkdayClassification and AttendanceCorrection entities in `backend/src/NaderGorge.Domain/Entities/HR/Attendance/`
- [x] T061 [US5] Implement late/early/absence/overtime calculator and missing-clock policy in `backend/src/NaderGorge.Application/Features/HR/Attendance/AttendanceCalculator.cs`
- [x] T062 [US5] Implement submit/apply correction commands with approval and version checks in `backend/src/NaderGorge.Application/Features/HR/Attendance/Corrections/`
- [x] T063 [US5] Expose correction and dry-run recalculation APIs in `backend/src/NaderGorge.API/Controllers/HrAttendanceController.cs`
- [x] T064 [P] [US5] Build employee correction form and reviewer diff UI in `frontend/src/features/hr/attendance/`
- [x] T065 [US5] Add correction role/scope E2E in `frontend/e2e/hr-attendance-corrections.spec.ts`

## Phase 8: User Story 6 — Leave and multilevel approvals (P1)

**Independent Test**: workday-aware leave reserves balance, manager then HR approve, delegation and escalation work in their windows, self-approval fails, final approval debits once without attendance session.

- [x] T066 [P] [US6] Write leave ledger, reservation and workday tests in `backend/tests/NaderGorge.Application.Tests/HR/LeavePolicyTests.cs`
- [x] T067 [P] [US6] Write delegation, SLA escalation and self-approval tests in `backend/tests/NaderGorge.Application.Tests/HR/ApprovalEngineTests.cs`
- [x] T068 [P] [US6] Add leave policy/balance/ledger/request entities in `backend/src/NaderGorge.Domain/Entities/HR/Leave/`
- [x] T069 [P] [US6] Add approval definition/instance/step/delegation entities in `backend/src/NaderGorge.Domain/Entities/HR/Approvals/`
- [x] T070 [US6] Implement durable ApprovalEngine resolver/decision rules in `backend/src/NaderGorge.Application/Features/HR/Approvals/ApprovalEngine.cs`
- [x] T071 [US6] Implement idempotent escalation hosted service and outbox in `backend/src/NaderGorge.Infrastructure/HostedServices/HrApprovalEscalationService.cs`
- [x] T072 [US6] Implement leave submit/withdraw/finalize ledger commands in `backend/src/NaderGorge.Application/Features/HR/Leave/`
- [x] T073 [US6] Configure leave/approval constraints and EF migration in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T074 [US6] Expose leave, approvals and delegation APIs in `backend/src/NaderGorge.API/Controllers/HrLeaveController.cs` and `backend/src/NaderGorge.API/Controllers/HrApprovalsController.cs`
- [x] T075 [P] [US6] Build self leave balance/request and reviewer inbox UI in `frontend/src/features/hr/leave/` and `frontend/src/app/admin/hr/approvals/`
- [x] T076 [US6] Add manager/HR/delegate/self-approval E2E in `frontend/e2e/hr-leave-approvals.spec.ts`

## Phase 9: User Story 7 — Configurable payroll (P1)

**Independent Test**: dated rules calculate explainable unique lines, finance reviews, GM approves, closed run cannot change and later settlement preserves original payslip.

- [x] T077 [P] [US7] Write payroll rule, rounding, snapshot, replay and transition tests in `backend/tests/NaderGorge.Application.Tests/Finance/HrPayrollEngineTests.cs`
- [x] T078 [P] [US7] Add component/rule/compensation/run/employee-line/payslip entities in `backend/src/NaderGorge.Domain/Entities/HR/Payroll/`
- [x] T079 [US7] Implement constrained formula validation and calculation engine in `backend/src/NaderGorge.Application/Features/HR/Payroll/PayrollCalculationEngine.cs`
- [x] T080 [US7] Implement run prepare/finance-review/GM-approve/pay/close/settlement commands in `backend/src/NaderGorge.Application/Features/HR/Payroll/Commands/`
- [x] T081 [US7] Configure immutable payroll constraints and EF migration in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T082 [US7] Split employee payroll authorization from teacher finance in `backend/src/NaderGorge.API/Controllers/HrPayrollController.cs` and `backend/src/NaderGorge.API/Controllers/AdminFinanceController.cs`
- [x] T083 [P] [US7] Add typed payroll/config services in `frontend/src/services/hr-payroll-service.ts`
- [x] T084 [US7] Build rule editor, cycle review, explanation and final approval UI in `frontend/src/app/admin/hr/payroll/`
- [x] T085 [US7] Build secured self payslip UI in `frontend/src/features/hr/payroll/`
- [x] T086 [US7] Add finance/GM/negative permission and teacher-finance regression E2E in `frontend/e2e/hr-payroll.spec.ts`

## Phase 10: User Story 8 — Advances, loans, expenses and commissions (P2)

**Independent Test**: approved requests schedule/deduct once, required evidence is enforced and replay never duplicates a payroll source.

- [x] T087 [P] [US8] Write installment, balance, attachment and duplicate-source tests in `backend/tests/NaderGorge.Application.Tests/Finance/HrFinancialRequestTests.cs`
- [x] T088 [P] [US8] Add financial request/installment/source entities in `backend/src/NaderGorge.Domain/Entities/HR/Payroll/`
- [x] T089 [US8] Implement request approval, schedules and payroll input adapters in `backend/src/NaderGorge.Application/Features/HR/Payroll/FinancialRequests/`
- [x] T090 [US8] Expose self/admin financial request APIs in `backend/src/NaderGorge.API/Controllers/HrPayrollController.cs`
- [x] T091 [P] [US8] Build request, schedule and balance UI in `frontend/src/features/hr/financial-requests/`
- [x] T092 [US8] Add financial request E2E in `frontend/e2e/hr-financial-requests.spec.ts`

## Phase 11: User Story 9 — Self-service, documents and assets (P2)

**Independent Test**: employee reads self only, authorized download is audited, expiry alerts fire and open asset custody blocks offboarding unless approved exception.

- [x] T093 [P] [US9] Write document access/version/retention and asset custody tests in `backend/tests/NaderGorge.Application.Tests/HR/DocumentAssetTests.cs`
- [x] T094 [P] [US9] Add document/version and asset/custody entities in `backend/src/NaderGorge.Domain/Entities/HR/Lifecycle/`
- [x] T095 [US9] Implement secured file authorization, versioning, expiry and custody services in `backend/src/NaderGorge.Application/Features/HR/Lifecycle/`
- [x] T096 [US9] Expose self/admin document and asset APIs in `backend/src/NaderGorge.API/Controllers/HrDocumentsAssetsController.cs`
- [x] T097 [P] [US9] Build responsive employee hub with loading/empty/error states in `frontend/src/app/employee/`
- [x] T098 [US9] Add cross-employee denial and asset-block E2E in `frontend/e2e/hr-self-service.spec.ts`

## Phase 12: User Story 10 — Performance and employee cases (P2)

**Independent Test**: weighted review completes with appeal, confidential case is invisible without permission and approved financial penalty reaches payroll exactly once.

- [x] T099 [P] [US10] Write review weighting/appeal and case confidentiality/payroll-link tests in `backend/tests/NaderGorge.Application.Tests/HR/PerformanceCaseTests.cs`
- [x] T100 [P] [US10] Add performance and confidential case entities in `backend/src/NaderGorge.Domain/Entities/HR/Performance/`
- [x] T101 [US10] Implement cycle/review/appeal and case/response/action handlers in `backend/src/NaderGorge.Application/Features/HR/Performance/`
- [x] T102 [US10] Expose scoped performance/case APIs in `backend/src/NaderGorge.API/Controllers/HrPerformanceCasesController.cs`
- [x] T103 [P] [US10] Build manager review and restricted case workspaces in `frontend/src/app/admin/hr/performance/` and `frontend/src/app/admin/hr/cases/`
- [x] T104 [US10] Add confidentiality and payroll-link E2E in `frontend/e2e/hr-performance-cases.spec.ts`

## Phase 13: User Story 11 — Recruitment, onboarding and offboarding (P3)

**Independent Test**: accepted candidate becomes one complete employee without re-entry, late onboarding alerts and completed offboarding disables access while retaining history.

- [x] T105 [P] [US11] Write candidate-to-hire atomicity and offboarding blocker tests in `backend/tests/NaderGorge.Application.Tests/HR/RecruitmentLifecycleTests.cs`
- [x] T106 [P] [US11] Add requisition/candidate/interview/offer/task/offboarding entities in `backend/src/NaderGorge.Domain/Entities/HR/Recruitment/`
- [x] T107 [US11] Implement recruitment pipeline and atomic hire adapter in `backend/src/NaderGorge.Application/Features/HR/Recruitment/`
- [x] T108 [US11] Implement onboarding/probation/offboarding orchestration and alerts in `backend/src/NaderGorge.Application/Features/HR/Lifecycle/`
- [x] T109 [US11] Expose recruitment/lifecycle APIs in `backend/src/NaderGorge.API/Controllers/HrRecruitmentLifecycleController.cs`
- [x] T110 [P] [US11] Build recruitment board and lifecycle checklists in `frontend/src/app/admin/hr/recruitment/` and `frontend/src/app/admin/hr/lifecycle/`
- [x] T111 [US11] Add hire-to-exit E2E in `frontend/e2e/hr-lifecycle.spec.ts`

## Phase 14: User Story 12 — Reports, permissions and safe migration (P1)

**Independent Test**: every role is server-scoped, exports contain allowed rows only, each module dry-runs/reconciles/activates/rolls back independently with one writer and exact counts/totals.

- [x] T112 [P] [US12] Write HTTP permission matrix tests for employee/manager/HR/finance/GM/outsider in `backend/tests/NaderGorge.Application.Tests/Authorization/HrAuthorizationTests.cs`
- [x] T113 [P] [US12] Write migration replay/conflict/reconciliation/rollback tests in `backend/tests/NaderGorge.Application.Tests/HR/HrMigrationTests.cs`
- [x] T114 [US12] Implement migration mapper/reconciler and rollout state machine in `backend/src/NaderGorge.Application/Features/HR/Migration/`
- [x] T115 [US12] Implement retention/legal-hold dry-run and archive/anonymization services in `backend/src/NaderGorge.Application/Features/HR/Retention/`
- [x] T116 [US12] Implement workforce reports with scoped filters/paging/export audit in `backend/src/NaderGorge.Application/Features/HR/Reporting/`
- [x] T117 [US12] Expose migration/rollout/retention/report APIs in `backend/src/NaderGorge.API/Controllers/HrGovernanceController.cs`
- [x] T118 [P] [US12] Build migration reconciliation/rollout console in `frontend/src/app/admin/hr/migration/`
- [x] T119 [P] [US12] Build scoped workforce reports in `frontend/src/app/admin/hr/reports/`
- [x] T120 [US12] Add migration rollback, export scope and direct-URL denial E2E in `frontend/e2e/hr-governance.spec.ts`

## Phase 15: Cross-cutting polish and verification

- [x] T121 Normalize HR query keys, realtime scopes and cache invalidation in `frontend/src/lib/query-keys.ts`, `frontend/src/lib/realtime-invalidation-map.ts`, and `frontend/src/lib/staff-realtime-scopes.ts`
- [x] T122 Verify Arabic RTL, WCAG AA, focus, 44px targets, reduced motion and no sensitive hydration flash across `frontend/src/app/admin/hr/` and `frontend/src/app/employee/`
- [x] T123 Add performance indexes/query projections for 3-second lists and 5-second reports in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [x] T124 Run all feature tests with `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj` and record expected pass totals in `specs/164-comprehensive-hr-platform/implementation-log.md`
- [x] T125 Run `cd frontend && npm run lint && npm run build` and all `frontend/e2e/hr-*.spec.ts`; record results in `specs/164-comprehensive-hr-platform/implementation-log.md`
- [x] T126 Run `make verify`, `docker compose config -q`, `make up`, `make migrate`, `make ps` and health checks; record results in `specs/164-comprehensive-hr-platform/implementation-log.md`
- [x] T127 Execute quickstart role matrix for HR, employee, support employee, support assistant, manager, delegate, finance, GM, teacher and student in `specs/164-comprehensive-hr-platform/verification-report.md`
- [x] T128 Execute each module dry-run→activate→rollback→reactivate and record count/total/hash evidence in `specs/164-comprehensive-hr-platform/verification-report.md`
- [x] T129 Run deep architectural/code/UI critique and record every finding and disposition in `specs/164-comprehensive-hr-platform/review-report.md`
- [x] T130 Run `clean-code-guard` against changed production code and resolve all blocking findings in `specs/164-comprehensive-hr-platform/review-report.md`
- [x] T131 Run `test-guard` against changed test code and resolve all blocking findings in `specs/164-comprehensive-hr-platform/review-report.md`
- [x] T132 Run final feature tests and write scope, results, Docker/manual QA, risks and go/no-go in `specs/164-comprehensive-hr-platform/verification-report.md`

## Dependencies & execution order

- Setup → Foundation → US1 → US2 → US3 → US4 → US5 → US6 → US7; these are the safe rollout spine.
- US8 and US9 start after US7 and US2 respectively; US10 starts after approval and payroll links; US11 starts after US1/US2/US9; US12 spans all completed modules.
- Tests in every story are written first and must demonstrate the expected failure before implementation, except characterized legacy behavior documented in the implementation log.
- Tasks marked `[P]` touch independent files and may run concurrently only after their phase dependencies are complete.
- No next wave starts until focused tests, full regression, Docker health, migration reconciliation and role-based manual QA pass or the owner records an explicit accepted risk.

## Parallel examples

- US1: T021 and T022 can run together; T027 can start after the API payload is fixed while T026 prepares the migration.
- US6: T066-T069 can run in parallel, then T070-T074 form the approval/leave integration spine, while T075 builds UI from the fixed contracts.
- US7: T077-T078 in parallel, then engine/state machine, while T083 prepares typed client contracts.
- US12: permission tests and migration tests run in parallel; report and migration UIs can proceed independently after endpoints stabilize.

## Implementation strategy

The first shippable increment is Wave 0 plus US1: corrected safety defects and atomic employee creation. Continue in the fixed migration order and retain compatibility adapters only until each module reaches `NewActive`. Never dual-write a module, never edit closed payroll, and never infer employment from a non-student role.
