# Tasks: Platform Financial Center

**Input**: Design documents from `/specs/168-platform-financial-center/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/platform-finance-api.md`, `quickstart.md`
**Tests**: Mandatory for every financial behavior, permission, API, migration, and UI change. Write failing tests first unless a documented legacy constraint prevents it.

## Phase 1: Setup and Financial Evidence Baseline

**Purpose**: Freeze current financial evidence and establish feature boundaries before schema or behavior changes.

- [X] T001 Inventory recharge, balance, sales, entitlement, teacher-finance, settlement, payout, cancellation, payroll, wallet, and invoice source tables in `specs/168-platform-financial-center/source-inventory.md`
- [X] T002 Record current source statuses, ownership rules, timestamps, and money fields in `specs/168-platform-financial-center/source-posting-matrix.md`
- [X] T003 [P] Add month/source count and amount baseline SQL as read-only queries in `backend/scripts/finance/platform-finance-baseline.sql`
- [X] T004 [P] Document the seeded EGP chart of accounts and control-account mapping in `specs/168-platform-financial-center/chart-of-accounts.md`
- [X] T005 Define feature flags for shadow posting, read-only cockpit, and finance mutations in `backend/src/NaderGorge.Application/Common/Configuration/PlatformFinanceOptions.cs`
- [X] T006 Bind and validate platform-finance feature flags in `backend/src/NaderGorge.API/Configuration/PlatformFinanceConfiguration.cs`
- [X] T007 Add the admin platform-finance navigation shell behind view permission and feature flag in `frontend/src/app/admin/platform-finance/layout.tsx`
- [X] T008 Publish Phase 1 source totals, known ambiguities, command results, and go/no-go in `specs/168-platform-financial-center/reports/phase-1-baseline.md`

**Checkpoint**: No schema work starts until all selected historical sources are classified as postable, ignored with reason, or ambiguous.

---

## Phase 2: Foundational Ledger and Security

**Purpose**: Build the immutable accounting foundation that blocks every user story.

- [X] T009 [P] Add financial account, journal, period, treasury, and shared finance enums in `backend/src/NaderGorge.Domain/Enums/PlatformFinanceEnums.cs`
- [X] T010 [P] Add `FinancialAccount`, `JournalEntry`, and `JournalLine` entities with reversal and source identity rules in `backend/src/NaderGorge.Domain/Entities/Finance/LedgerEntities.cs`
- [X] T011 [P] Add `AccountingPeriod`, `TreasuryAccount`, and `FinancialProjectionCheckpoint` entities in `backend/src/NaderGorge.Domain/Entities/Finance/ControlEntities.cs`
- [X] T012 Define posting commands, line dimensions, and canonical posting result in `backend/src/NaderGorge.Application/Interfaces/Finance/IFinancialPostingService.cs`
- [X] T013 [P] Add EF configurations, numeric precision, immutable-source indexes, and check constraints in `backend/src/NaderGorge.Infrastructure/Data/Configurations/PlatformFinanceConfiguration.cs`
- [X] T014 Register finance DbSets and configurations in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [X] T015 Add granular dashboard, ledger, expense, refund, treasury, budget, export, migration, close, and reopen permission constants in `backend/src/NaderGorge.Domain/Enums/Permission.cs`
- [X] T016 Wire finance permissions into existing role/claim authorization policies in `backend/src/NaderGorge.API/Authorization/AuthorizationPolicies.cs`
- [X] T017 Seed chart-of-account control roles, admin permissions, existing wallet mappings, and one cashbox in `backend/src/NaderGorge.Infrastructure/Data/PlatformFinanceSeeder.cs`
- [X] T018 Implement transactional balancing, decimal validation, source idempotency, period checks, and linked reversal in `backend/src/NaderGorge.Infrastructure/Services/Finance/FinancialPostingService.cs`
- [X] T019 Register finance services and option validation in `backend/src/NaderGorge.Infrastructure/DependencyInjection.cs`
- [X] T020 Generate the additive `AddPlatformFinancialCenterFoundation` EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [X] T021 [P] Add posting-engine unit tests for equality, precision, malformed lines, and reversal construction in `backend/tests/NaderGorge.Application.Tests/Finance/FinancialPostingServiceTests.cs`
- [X] T022 [P] Add integration tests for idempotent retries, concurrent duplicate posting, closed periods, and database constraints in `backend/tests/NaderGorge.Integration.Tests/Finance/FinancialLedgerFoundationTests.cs`
- [X] T023 [P] Add permission-boundary tests for admin and explicitly permitted/denied staff in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformFinanceAuthorizationTests.cs`
- [ ] T024 Run foundation tests and migration on disposable PostgreSQL and record zero unbalanced/duplicate entries in `specs/168-platform-financial-center/reports/phase-2-foundation.md`

**Checkpoint**: Foundation is blocking. No story implementation starts until migration, balance, idempotency, period, and permission tests pass.

---

## Phase 3: User Story 1 — Platform Financial Position (Priority: P1) 🎯 MVP

**Goal**: Show cash, unused balances, liabilities, revenue, expenses, refunds, and profit with source/journal drill-down.

**Independent Test**: Post a teacher recharge, partial purchase, teacher payment, expense, and refund fixture; dashboard and all control accounts reconcile to journal lines.

### Tests for User Story 1

- [X] T025 [P] [US1] Add dashboard and ledger API contract tests including pagination and filters in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformFinanceDashboardContractTests.cs`
- [ ] T026 [P] [US1] Add projection reconciliation and production-like p95 query tests in `backend/tests/NaderGorge.Integration.Tests/Performance/PlatformFinanceDashboardPerformanceTests.cs`
- [X] T027 [P] [US1] Add admin cockpit drill-down Playwright journey in `frontend/tests/e2e/admin-platform-finance-dashboard.spec.ts`

### Implementation for User Story 1

- [X] T028 [P] [US1] Define dashboard, ledger, journal-detail, and bootstrap DTOs in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Dashboard/PlatformFinanceDashboardDtos.cs`
- [X] T029 [US1] Implement bounded dashboard aggregates and control-account reconciliation query in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Dashboard/GetPlatformFinanceDashboardQuery.cs`
- [X] T030 [US1] Implement cursor-paginated ledger and journal/source drill-down queries in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Ledger/GetFinancialLedgerQuery.cs`
- [X] T031 [US1] Expose `/bootstrap`, `/dashboard`, `/ledger`, and `/journals/{id}` with permissions in `backend/src/NaderGorge.API/Controllers/Admin/PlatformFinanceController.cs`
- [X] T032 [P] [US1] Add typed dashboard/ledger models and API calls in `frontend/src/services/platformFinanceService.ts`
- [X] T033 [P] [US1] Build RTL metric cards, liability separation, date filters, and loading/error states in `frontend/src/components/admin/platform-finance/FinanceCockpit.tsx`
- [X] T034 [P] [US1] Build bounded ledger table and source drill-down drawer in `frontend/src/components/admin/platform-finance/FinancialLedgerTable.tsx`
- [X] T035 [US1] Compose the financial cockpit and drill-down flow in `frontend/src/app/admin/platform-finance/page.tsx`
- [X] T036 [US1] Record reconciliation totals, p95 evidence, frontend checks, and MVP go/no-go in `specs/168-platform-financial-center/reports/phase-3-us1.md`

**Checkpoint**: Owner can independently prove every displayed amount through the journal and original source.

---

## Phase 4: User Story 2 — Platform Expenses (Priority: P1)

**Goal**: Authorized staff can manage paid/unpaid platform expenses without changing teacher dues.

**Independent Test**: Post one paid cash expense and one supplier payable, pay the latter, reverse the former, and verify treasury, AP, expense, audit, and denied staff access.

### Tests for User Story 2

- [X] T037 [P] [US2] Add expense draft/post/payment/reversal and overpayment integration tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformExpenseWorkflowTests.cs`
- [X] T038 [P] [US2] Add expense permission and immutable-posted-document contract tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformExpenseContractTests.cs`
- [X] T039 [P] [US2] Add paid/unpaid expense admin Playwright journey in `frontend/tests/e2e/admin-platform-expenses.spec.ts`

### Implementation for User Story 2

- [X] T040 [P] [US2] Add expense category, cost center, vendor, expense, and payment entities in `backend/src/NaderGorge.Domain/Entities/Finance/ExpenseEntities.cs`
- [X] T041 [P] [US2] Add expense EF mappings, constraints, indexes, and concurrency token in `backend/src/NaderGorge.Infrastructure/Data/Configurations/PlatformExpenseConfiguration.cs`
- [X] T042 [US2] Add expense tables through the `AddPlatformFinancialCenterExpenses` EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [X] T043 [US2] Implement expense draft/edit/post/pay/reverse commands and validators in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Expenses/PlatformExpenseCommands.cs`
- [X] T044 [US2] Implement paid-expense and supplier-payable posting templates in `backend/src/NaderGorge.Infrastructure/Services/Finance/ExpensePostingService.cs`
- [X] T045 [US2] Add expense/category/cost-center/vendor endpoints to `backend/src/NaderGorge.API/Controllers/Admin/PlatformFinanceController.cs`
- [X] T046 [P] [US2] Build expense list/editor/payment/reversal UI in `frontend/src/components/admin/platform-finance/ExpenseManager.tsx`
- [X] T047 [US2] Add expenses route with receipt uploads and permission-aware actions in `frontend/src/app/admin/platform-finance/expenses/page.tsx`
- [X] T048 [US2] Record expense/AP reconciliation and permission evidence in `specs/168-platform-financial-center/reports/phase-4-us2.md`

**Checkpoint**: Posted expenses are immutable, corrections reverse, AP clears exactly on payment, and teacher dues remain unchanged.

---

## Phase 5: User Story 3 — Cash and Balance Refunds (Priority: P1)

**Goal**: Refund purchases to student balance or cash while correctly reversing platform and teacher effects.

**Independent Test**: Refund an unpaid-teacher sale to balance and an already-paid-teacher sale in cash; verify access, liability/treasury, revenue reversal, teacher debt, and partial limits.

### Tests for User Story 3

- [X] T049 [P] [US3] Add cash/balance, partial/full, duplicate, and over-refund integration tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformRefundWorkflowTests.cs`
- [X] T050 [P] [US3] Add paid/unpaid teacher allocation reversal and debt tests in `backend/tests/NaderGorge.Application.Tests/Finance/RefundTeacherAccountingTests.cs`
- [X] T051 [P] [US3] Add refund permission and required evidence contract tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformRefundContractTests.cs`
- [X] T052 [P] [US3] Add cash and balance refund Playwright journey in `frontend/tests/e2e/admin-platform-refunds.spec.ts`

### Implementation for User Story 3

- [X] T053 [P] [US3] Add refund request/payment entities and remaining-refundable invariants in `backend/src/NaderGorge.Domain/Entities/Finance/RefundEntities.cs`
- [X] T054 [P] [US3] Add refund EF mapping, source uniqueness, and query indexes in `backend/src/NaderGorge.Infrastructure/Data/Configurations/PlatformRefundConfiguration.cs`
- [X] T055 [US3] Add refund tables through the `AddPlatformFinancialCenterRefunds` EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [X] T056 [US3] Implement transactional refund eligibility, access cancellation, and idempotent commands in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Refunds/PlatformRefundCommands.cs`
- [X] T057 [US3] Implement cash/balance posting and paid-teacher debt integration in `backend/src/NaderGorge.Infrastructure/Services/Finance/RefundPostingService.cs`
- [X] T058 [US3] Add refund list/create/post/reverse endpoints to `backend/src/NaderGorge.API/Controllers/Admin/PlatformFinanceController.cs`
- [X] T059 [P] [US3] Build refund source lookup, method-specific form, history, and totals in `frontend/src/components/admin/platform-finance/RefundManager.tsx`
- [X] T060 [US3] Add the permission-aware refunds route in `frontend/src/app/admin/platform-finance/refunds/page.tsx`
- [X] T061 [US3] Record entitlement, treasury/liability, revenue, and teacher reconciliation in `specs/168-platform-financial-center/reports/phase-5-us3.md`

**Checkpoint**: Every refund has one source, cannot exceed the refundable remainder, and reconciles across student, teacher, platform, and treasury.

---

## Phase 6: User Story 4 — Complete Teacher Financial Relationship (Priority: P1)

**Goal**: Mirror every supported sale and teacher settlement effect into the GL and expose one reconciled teacher summary.

**Independent Test**: Use direct balance, code, public exam, and shared package sales; reverse one, settle/pay another, and reconcile teacher payable and platform share.

### Tests for User Story 4

- [X] T062 [P] [US4] Add posting adapter tests for recharge, direct sale, code, public exam, shared package, payroll, cancellation, and teacher payment in `backend/tests/NaderGorge.Integration.Tests/Finance/LiveFinancialSourceAdapterTests.cs`
- [X] T063 [P] [US4] Add general-versus-teacher recharge scope and immutability tests in `backend/tests/NaderGorge.Integration.Tests/Finance/RechargeFinancialScopeTests.cs`
- [X] T064 [P] [US4] Add teacher control-account reconciliation tests in `backend/tests/NaderGorge.Integration.Tests/Finance/TeacherPayableReconciliationTests.cs`
- [X] T065 [P] [US4] Add teacher summary drill-down Playwright journey in `frontend/tests/e2e/admin-platform-teacher-finance-summary.spec.ts`

### Implementation for User Story 4

- [X] T066 [US4] Restore explicit `General|Teacher` recharge selection and immutable scope validation in `backend/src/NaderGorge.Application/Features/Admin/RechargeRequests/`
- [X] T067 [P] [US4] Implement recharge and student-liability posting adapters in `backend/src/NaderGorge.Infrastructure/Services/Finance/Adapters/RechargeFinancialAdapter.cs`
- [X] T068 [P] [US4] Implement direct, code, public-exam, and shared-package sale adapters in `backend/src/NaderGorge.Infrastructure/Services/Finance/Adapters/SalesFinancialAdapter.cs`
- [X] T069 [P] [US4] Implement teacher allocation, reversal, settlement, and payout adapters in `backend/src/NaderGorge.Infrastructure/Services/Finance/Adapters/TeacherFinancialAdapter.cs`
- [X] T070 [P] [US4] Implement payroll-payment posting adapter without changing payroll authority in `backend/src/NaderGorge.Infrastructure/Services/Finance/Adapters/PayrollFinancialAdapter.cs`
- [X] T071 [US4] Attach adapters transactionally to existing source handlers and shadow-posting comparisons in `backend/src/NaderGorge.Application/Services/Finance/LiveFinancialProjectionCoordinator.cs`
- [X] T072 [US4] Implement teacher gross sales/share/refund/debt/paid/outstanding query in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Teachers/GetTeacherFinancialSummaryQuery.cs`
- [X] T073 [US4] Add teacher summary endpoint to `backend/src/NaderGorge.API/Controllers/Admin/PlatformFinanceController.cs`
- [X] T074 [P] [US4] Build teacher summary and source drill-down UI in `frontend/src/components/admin/platform-finance/TeacherFinancialSummary.tsx`
- [X] T075 [US4] Add teacher finance detail route in `frontend/src/app/admin/platform-finance/teachers/[teacherId]/page.tsx`
- [X] T076 [US4] Record source-by-source and teacher-control reconciliation evidence in `specs/168-platform-financial-center/reports/phase-6-us4.md`

**Checkpoint**: Teacher subledger outstanding equals the GL teacher-payable control account to EGP 0.01.

---

## Phase 7: User Story 6 — Treasury Reconciliation and Historical Reconstruction (Priority: P1)

**Goal**: Reconcile wallets/cashboxes and reconstruct all trustworthy historical movements without duplication or silent assumptions.

**Independent Test**: Dry-run history, resolve exceptions, post a repeat-safe batch, rerun it, then reconcile one wallet and cashbox with explained evidence.

### Tests for User Story 6

- [X] T077 [P] [US6] Add migration dry-run, exception, checksum, atomic post, and replay tests in `backend/tests/NaderGorge.Integration.Tests/Migrations/PlatformFinanceHistoricalMigrationTests.cs`
- [X] T078 [P] [US6] Add treasury transfer and reconciliation adjustment tests in `backend/tests/NaderGorge.Integration.Tests/Finance/TreasuryReconciliationTests.cs`
- [X] T079 [P] [US6] Add migration/treasury permission contract tests in `backend/tests/NaderGorge.Integration.Tests/Migrations/FinanceMigrationAuthorizationTests.cs`
- [X] T080 [P] [US6] Add dry-run exception resolution and cashbox reconciliation Playwright journey in `frontend/tests/e2e/admin-platform-finance-migration.spec.ts`

### Implementation for User Story 6

- [X] T081 [P] [US6] Add treasury transfer/reconciliation entities in `backend/src/NaderGorge.Domain/Entities/Finance/TreasuryEntities.cs`
- [X] T082 [P] [US6] Add migration batch/item/exception entities in `backend/src/NaderGorge.Domain/Entities/Finance/MigrationEntities.cs`
- [X] T083 [US6] Add treasury and migration schema through the `AddPlatformFinanceMigrationAndReconciliation` EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [X] T084 [US6] Implement asset-to-asset transfer and statement/count reconciliation commands in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Treasury/TreasuryCommands.cs`
- [X] T085 [US6] Implement source inventory adapters, deterministic checksums, dry-run, exception resolution, and atomic posting in `backend/src/NaderGorge.Infrastructure/Services/Finance/Migration/HistoricalFinanceMigrationService.cs`
- [X] T086 [US6] Implement source/month/control-account reconciliation reports in `backend/src/NaderGorge.Infrastructure/Services/Finance/Migration/FinancialReconciliationService.cs`
- [X] T087 [US6] Add treasury, reconciliation, and migration endpoints to `backend/src/NaderGorge.API/Controllers/Admin/PlatformFinanceController.cs`
- [X] T088 [P] [US6] Build treasury account, transfer, and reconciliation UI in `frontend/src/components/admin/platform-finance/TreasuryManager.tsx`
- [X] T089 [P] [US6] Build migration dry-run totals and exception-resolution UI in `frontend/src/components/admin/platform-finance/HistoricalMigrationManager.tsx`
- [X] T090 [US6] Add treasury and migration routes in `frontend/src/app/admin/platform-finance/treasury/page.tsx`
- [X] T091 [US6] Export final historical counts, checksums, exceptions, and zero-duplicate replay evidence in `specs/168-platform-financial-center/reports/phase-7-us6.md`

**Checkpoint**: Every selected source row is posted, ignored with reason, or an explicit exception; replay adds zero entries.

---

## Phase 8: User Story 5 — Flexible Budgets (Priority: P2)

**Goal**: Create versioned weekly, monthly, yearly, and custom plans and compare them to posted actuals.

**Independent Test**: Activate overlapping weekly and annual plans, post revenue/expenses, and verify actual, remaining, variance, and forecast from journals only.

### Tests for User Story 5

- [X] T092 [P] [US5] Add budget period/version/actual/variance integration tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformBudgetTests.cs`
- [X] T093 [P] [US5] Add budget permission and API contract tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformBudgetContractTests.cs`
- [X] T094 [P] [US5] Add overlapping budget comparison Playwright journey in `frontend/tests/e2e/admin-platform-finance-budgets.spec.ts`

### Implementation for User Story 5

- [X] T095 [P] [US5] Add budget plan/line entities and version rules in `backend/src/NaderGorge.Domain/Entities/Finance/BudgetEntities.cs`
- [X] T096 [US5] Add budget schema through the `AddPlatformFinanceBudgets` EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [X] T097 [US5] Implement budget CRUD, activation/archive, and journal-derived actual queries in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Budgets/PlatformBudgetHandlers.cs`
- [X] T098 [US5] Add budget endpoints to `backend/src/NaderGorge.API/Controllers/Admin/PlatformFinanceController.cs`
- [X] T099 [P] [US5] Build budget editor and variance/forecast table in `frontend/src/components/admin/platform-finance/BudgetManager.tsx`
- [X] T100 [US5] Add budget route in `frontend/src/app/admin/platform-finance/budgets/page.tsx`
- [X] T101 [US5] Record journal-to-budget actual reconciliation in `specs/168-platform-financial-center/reports/phase-8-us5.md`

**Checkpoint**: Budget edits never alter journal history and actuals equal filtered posted lines.

---

## Phase 9: User Story 7 — Reports, Exports, and Period Close (Priority: P2)

**Goal**: Export accountant-ready Excel/PDF reports and prevent closed historical periods from drifting.

**Independent Test**: Export one filtered period to both formats, compare totals, close it, reject a backdated post, reopen with permission/reason, and inspect audit history.

### Tests for User Story 7

- [X] T102 [P] [US7] Add profit/loss, cash-flow, financial-position, refund, expense, and teacher report query tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformFinancialReportTests.cs`
- [X] T103 [P] [US7] Add XLSX/PDF parity and privacy-redaction tests in `backend/tests/NaderGorge.Integration.Tests/Finance/PlatformFinanceExportTests.cs`
- [X] T104 [P] [US7] Add close/reopen/backdated-post and audit integration tests in `backend/tests/NaderGorge.Integration.Tests/Finance/AccountingPeriodCloseTests.cs`
- [X] T105 [P] [US7] Add reports/export/period-close Playwright journey in `frontend/tests/e2e/admin-platform-finance-reports.spec.ts`

### Implementation for User Story 7

- [X] T106 [P] [US7] Implement shared filtered report datasets in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Reports/PlatformFinancialReportQueries.cs`
- [X] T107 [P] [US7] Implement XLSX and PDF renderers using the same dataset in `backend/src/NaderGorge.Infrastructure/Services/Finance/PlatformFinanceExportService.cs`
- [X] T108 [US7] Implement close/reopen commands, reason/audit capture, and backdated guard in `backend/src/NaderGorge.Application/Features/Admin/PlatformFinance/Periods/AccountingPeriodCommands.cs`
- [X] T109 [US7] Add report/export and period endpoints to `backend/src/NaderGorge.API/Controllers/Admin/PlatformFinanceController.cs`
- [X] T110 [P] [US7] Build report filters, totals, export actions, and privacy states in `frontend/src/components/admin/platform-finance/FinancialReports.tsx`
- [X] T111 [P] [US7] Build period close/reopen history UI in `frontend/src/components/admin/platform-finance/AccountingPeriodManager.tsx`
- [X] T112 [US7] Add reports and periods routes in `frontend/src/app/admin/platform-finance/reports/page.tsx`
- [X] T113 [US7] Record XLSX/PDF parity, close enforcement, and audit evidence in `specs/168-platform-financial-center/reports/phase-9-us7.md`

**Checkpoint**: Both export formats equal on-screen filtered totals and closed periods reject financial mutations.

---

## Phase 10: Polish, Hardening, and Production Rollout

**Purpose**: Seal performance, accessibility, observability, migration safety, and rolling deployment.

- [X] T114 [P] Add privacy-safe finance metrics for posting latency, duplicate retries, reconciliation variance, and query p95 in `backend/src/NaderGorge.Infrastructure/Observability/PlatformFinanceMetrics.cs`
- [X] T115 [P] Add Arabic RTL accessibility, keyboard, responsive, empty, loading, and error-state tests in `frontend/tests/components/platform-finance-accessibility.test.tsx`
- [ ] T116 Tune bounded report/dashboard queries and confirm supporting indexes with production-like `EXPLAIN ANALYZE` evidence in `specs/168-platform-financial-center/reports/query-performance.md`
- [X] T117 Run `dotnet test` and record backend results in `specs/168-platform-financial-center/reports/final-verification.md`
- [X] T118 Run `cd frontend && npm run lint && npm run typecheck && npm run build` and append results to `specs/168-platform-financial-center/reports/final-verification.md`
- [X] T119 Run `make verify-e2e` and append finance journey results to `specs/168-platform-financial-center/reports/final-verification.md`
- [ ] T120 Run `docker compose config -q`, `make up`, `make migrate`, `make ps`, and `make health` and append results to `specs/168-platform-financial-center/reports/final-verification.md`
- [ ] T121 Execute owner/admin/permitted-staff/denied-staff manual QA from `specs/168-platform-financial-center/quickstart.md` and record pass/fail in `specs/168-platform-financial-center/reports/manual-qa.md`
- [ ] T122 Enable shadow posting, compare live source totals, and record every mismatch/resolution in `specs/168-platform-financial-center/reports/shadow-posting.md`
- [ ] T123 Enable read-only cockpit after zero unexplained variance and record cutover checkpoint in `specs/168-platform-financial-center/reports/cutover.md`
- [X] T124 Execute mutation feature-flag rollout through the existing three-node rolling release procedure and record health/rollback evidence in `specs/168-platform-financial-center/reports/production-rollout.md`
- [X] T125 Run clean-code and test review across changed finance files and record accepted findings in `specs/168-platform-financial-center/reports/final-quality-review.md`
- [X] T126 Publish final scope, migrations, reconciliation, tests, Docker, manual QA, risks, and explicit production go/no-go in `specs/168-platform-financial-center/reports/final-go-no-go.md`

---

## Dependencies & Execution Order

### Phase dependencies

- Phase 1 baseline has no dependency and blocks schema design if evidence is incomplete.
- Phase 2 depends on Phase 1 and blocks every user story.
- US1, US2, and US3 can begin after Phase 2, but their combined independent-test fixture is only complete when live adapters in US4 exist.
- US4 depends on Phase 2 and should finish before historical reconstruction in US6.
- US6 depends on US4 source adapters and all schema-producing P1 stories; it blocks production cutover.
- US5 depends on Phase 2 and posted journal data; it can run in parallel with US6 after core live adapters exist.
- US7 depends on Phase 2; full report parity depends on the desired report-producing stories, and close enforcement must pass before production mutations.
- Phase 10 depends on all selected stories. Failed tests, Docker gates, reconciliation, or manual QA block rollout unless owner-approved risk is documented.

### User story dependency graph

```text
Setup → Foundation ─┬→ US1 Dashboard ───────────────┐
                    ├→ US2 Expenses ───────────────┤
                    ├→ US3 Refunds ────────────────┤
                    └→ US4 Live Sources → US6 History/Reconciliation
                                      ├→ US5 Budgets
                                      └→ US7 Reports/Close
All selected stories → Hardening → Shadow → Read-only → Mutations
```

### Parallel opportunities

- Foundation entities, EF mapping, permissions, and tests marked `[P]` can be developed in separate files.
- After foundation, US1 dashboard, US2 expenses, and US3 refund models/tests can proceed concurrently.
- Within US4, recharge, sales, teacher, and payroll adapters are parallel until coordinator integration.
- US6 treasury UI and migration UI are parallel; US7 report queries/export renderers and period UI are parallel.
- All Playwright specs can be authored in parallel with backend implementation and executed after endpoints exist.

## Parallel Execution Examples

### P1 workstreams after Foundation

```text
Workstream A: T025-T036 (US1 dashboard)
Workstream B: T037-T048 (US2 expenses)
Workstream C: T049-T061 (US3 refunds)
Workstream D: T062-T076 (US4 live source adapters)
```

### Historical reconstruction

```text
Backend migration: T077, T079, T082, T085, T086
Treasury reconciliation: T078, T081, T084, T088
Frontend exception flow: T080, T089, T090
```

## Implementation Strategy

### MVP first

1. Complete Phase 1 evidence baseline.
2. Complete Phase 2 ledger/security foundation.
3. Complete US1 as a read-only cockpit over controlled fixtures/shadow journals.
4. Stop and validate journal drill-down, p95, permissions, and zero discrepancy before adding mutations.

### Incremental delivery

1. Add expenses and refunds behind disabled mutation flags.
2. Add all live source adapters and reconcile teacher/student control accounts.
3. Reconstruct history and reconcile treasury; do not cut over with unexplained variance.
4. Add budgets, exports, and period controls.
5. Run full gates, shadow production, read-only cutover, then authorized mutations.

## Task Format Validation

All implementation checklist rows use `- [ ] T### [P?] [US?] Description with exact file path`. Setup/foundation/polish tasks intentionally omit story labels; story phases include their matching label.
