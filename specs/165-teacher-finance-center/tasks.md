# Tasks: مركز حسابات المدرسين والمالية

**Input**: `specs/165-teacher-finance-center/{spec,plan,research,data-model,quickstart}.md` and `contracts/admin-teacher-finance-center-api.md`  
**Tests**: Mandatory: backend integration/unit tests for every financial transition, frontend contract/UI tests where available, then build and Docker gates.

## Phase 1: Setup and Baseline

- [ ] T001 Record the current ledger, payout, code and Bunny behavior in `specs/165-teacher-finance-center/research.md` before changing financial writes.
- [x] T002 [P] Add focused finance test fixtures and an `AdminFinanceCenterTests` suite in `backend/tests/NaderGorge.Application.Tests/Finance/AdminFinanceCenterTests.cs`.
- [x] T003 [P] Add typed API request/response contracts in `frontend/src/features/teacher-finance-center/types.ts`.
- [ ] T004 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Teacher"` and record the baseline result in `specs/165-teacher-finance-center/quickstart.md`.

## Phase 2: Foundational Ledger and Authorization

**Purpose**: Establish the immutable, idempotent financial foundation before any user story.

- [x] T005 Add agreement, allocation snapshot, settlement, settlement-line/payment, invoice, code-delivery and reversal/debt enums in `backend/src/NaderGorge.Domain/Enums/TeacherAccountingEnums.cs`.
- [x] T006 [P] Add `TeacherFinancialAgreement`, `TeacherSettlement`, `TeacherSettlementLine`, `TeacherSettlementPayment`, `FinancialInvoice`, `CodeGroupFinancialTerms` and `CodeGroupDeliveryConfirmation` entities in `backend/src/NaderGorge.Domain/Entities/TeacherFinanceCenterEntities.cs`.
- [x] T007 Extend immutable source/discount/agreement/settlement snapshot fields on `TeacherFinancialEvent` and `TeacherFinancialAllocation` in `backend/src/NaderGorge.Domain/Entities/TeacherFinancialEvent.cs`.
- [x] T008 Register all finance-center DbSets in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T009 Configure PostgreSQL constraints, indexes and uniqueness for agreement dates, source-trigger idempotency and live settlement-line reservations in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T010 Create and inspect the EF migration for finance-center schema and safe legacy-null columns in `backend/src/NaderGorge.Infrastructure/Migrations/`.
- [x] T011 Add `TeacherAgreementResolver` with effective-date and video → lesson → section → term → package → default precedence in `backend/src/NaderGorge.Application/Services/TeacherAgreementResolver.cs`.
- [x] T012 Extend `TeacherAccountingService` to create snapshot-based idempotent allocations and reversible adjustments in `backend/src/NaderGorge.Application/Services/TeacherAccountingService.cs`.
- [x] T013 Enforce Admin-only access and audit actor/reason checks in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [ ] T014 Prove duplicate keys, duplicate reservations, non-admin access and wallet-recharge isolation with tests in `backend/tests/NaderGorge.Application.Tests/Finance/AdminFinanceCenterTests.cs`.

**Checkpoint**: Every financial source has one immutable allocation and every new endpoint denies non-admin users.

## Phase 3: User Story 1 — Agreements and Content Sales (P1) 🎯 MVP

**Goal**: Admin can maintain dated teacher agreements and a content sale records the resolved terms.

**Independent Test**: A teacher with 30% default and a 60 EGP lesson override receives exactly 60 EGP after a 100 EGP lesson sale.

- [ ] T015 [P] [US1] Add agreement create/edit/list commands, validators and DTOs in `backend/src/NaderGorge.Application/Features/Admin/TeacherFinanceCenter/Agreements/`.
- [x] T016 [US1] Add agreement endpoints described by the contract in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [x] T017 [US1] Route lesson/package/term/section/video/exam sales through the resolver and snapshot writer in `backend/src/NaderGorge.Application/Features/Student/PurchaseContentCommand.cs`.
- [ ] T018 [P] [US1] Cover precedence, effective dates, invalid overlap and immutable historic snapshots in `backend/tests/NaderGorge.Application.Tests/Finance/TeacherAgreementResolverTests.cs`.
- [x] T019 [P] [US1] Add agreement API methods in `frontend/src/services/finance-service.ts`.
- [x] T020 [US1] Build the teacher agreement drawer and agreement history table in `frontend/src/features/teacher-finance-center/AgreementWorkspace.tsx`.
- [x] T021 [US1] Mount the agreement workspace in `frontend/src/app/admin/finance/AdminFinancePageClient.tsx` with loading, empty, validation and permission-denied states.

## Phase 4: User Story 2 — Discounts and Code Batches (P1)

**Goal**: Sales display platform/teacher/split discount burden and code batches create dues only at their chosen trigger.

**Independent Test**: Wallet recharge creates no teacher due; a delivery-confirmed batch creates one due; later activation does not duplicate it.

- [ ] T022 [US2] Add discount-burden request models, validation and allocation calculation to `backend/src/NaderGorge.Application/Services/TeacherAccountingService.cs`.
- [x] T023 [US2] Persist code-group financial terms and a one-time audited delivery confirmation in `backend/src/NaderGorge.Application/Services/CodeGroupFinancialAccountingService.cs` and `backend/src/NaderGorge.API/Controllers/AdminTeacherCodeFinanceController.cs`.
- [x] T024 [US2] Apply delivery versus activation triggering in `backend/src/NaderGorge.Application/Features/Codes/ActivateCodeCommand.cs` and `backend/src/NaderGorge.Application/Features/Admin/Commands/BulkGenerateCodesCommand.cs`.
- [x] T025 [US2] Add financial-term and delivery-confirmation endpoints in `backend/src/NaderGorge.API/Controllers/AdminTeacherCodeFinanceController.cs`.
- [ ] T026 [P] [US2] Test discount carrier allocation, recharge isolation, delivery idempotency and activation idempotency in `backend/tests/NaderGorge.Application.Tests/Finance/CodeBatchFinanceTests.cs`.
- [x] T027 [P] [US2] Add code-batch financial API methods in `frontend/src/services/finance-service.ts`.
- [x] T028 [US2] Add code-batch terms form and delivery-confirmation dialog in `frontend/src/features/teacher-finance-center/TeacherFinanceOperationsWorkspace.tsx`. Immutable delivery evidence is retained by the API and is not yet queryable for a separate history view.

## Phase 5: User Story 3 — Shared Packages and Settlements (P1)

**Goal**: Shared package allocations are transparent, loss requires explicit acknowledgement, and settlements reserve exact payable lines.

**Independent Test**: Two teachers’ allocations appear once in their own settlements; a duplicated settlement cannot pay either allocation twice.

- [X] T029 [US3] Build shared-package allocation preview and loss-acknowledgement calculation in `backend/src/NaderGorge.Application/Features/Admin/TeacherFinanceCenter/SharedPackages/`.
- [X] T030 [US3] Require audited `confirmLoss` after a 409 preview response in `backend/src/NaderGorge.API/Controllers/StudentSharedPackagesController.cs`.
- [x] T031 [US3] Add atomic settlement preview/create/review/approve/pay/cancel commands in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [x] T032 [US3] Add settlement/invoice endpoints and payment attachment handling in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [ ] T033 [P] [US3] Test shared-package positive/negative platform share, 409 acknowledgement, reservation uniqueness and all valid transitions in `backend/tests/NaderGorge.Application.Tests/Finance/TeacherSettlementTests.cs`.
- [x] T034 [P] [US3] Add shared-preview and settlement API methods in `frontend/src/services/finance-service.ts`.
- [x] T035 [US3] Build settlement preview/detail lines, state controls, payment reference and invoice attachment UI in `frontend/src/features/teacher-finance-center/TeacherFinanceOperationsWorkspace.tsx`. A historical settlement-list endpoint is not available yet.

## Phase 6: User Story 4 — Reversals, Debt and Invoices (P2)

**Goal**: Admin reverses selected paid/unpaid allocation lines without deleting history and produces reconciled teacher documents.

**Independent Test**: A paid allocation selected for refund becomes a documented teacher debt or a next-settlement deduction while the original sale remains visible.

- [x] T036 [US4] Add selected-line reversal and debt-disposition commands with amount/remaining-value checks in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [x] T037 [US4] Extend adjustment mapping and immutable original-allocation links in `backend/src/NaderGorge.Domain/Entities/TeacherFinancialEvent.cs`.
- [x] T038 [US4] Add reversal, teacher summary and ledger queries/endpoints in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [ ] T039 [P] [US4] Test partial selected-line reversal, paid debt, future deduction, over-reversal rejection and invoice state/attachment history in `backend/tests/NaderGorge.Application.Tests/Finance/TeacherReversalAndInvoiceTests.cs`.
- [x] T040 [P] [US4] Add ledger, reversal and invoice-payment methods in `frontend/src/services/finance-service.ts`.
- [x] T041 [US4] Build teacher ledger, selected-allocation reversal dialog, debt/disposition UI and settlement invoice-payment timeline in `frontend/src/features/teacher-finance-center/TeacherFinanceOperationsWorkspace.tsx`.

## Phase 7: User Story 5 — Bunny Cost and Profitability (P2)

**Goal**: Admin sees USD Bunny costs and their provenance beside, but never mixed with, EGP revenue and teacher dues.

**Independent Test**: A synced Bunny snapshot rolls up once to its linked video/lesson/package/teacher and explicitly labels actual, estimated or unavailable data.

- [x] T042 [US5] Add Bunny cost provenance and non-duplicating rollup queries in `backend/src/NaderGorge.Application/Features/Admin/TeacherFinanceCenter/Bunny/`.
- [x] T043 [US5] Reuse snapshot sync failure retention in `backend/src/NaderGorge.Application/Features/Admin/Finance/GetBunnyCostReportQuery.cs`.
- [x] T044 [US5] Add Bunny sync and cost-report endpoints in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [x] T045 [P] [US5] Test actual/estimated/missing labels, one-video-per-rollup aggregation and USD/EGP separation in `backend/tests/NaderGorge.Application.Tests/Finance/BunnyFinanceReportTests.cs`.
- [ ] T046 [P] [US5] Add Bunny finance query methods in `frontend/src/services/finance-service.ts`.
- [ ] T047 [US5] Extend the report with USD provenance, missing-data state and teacher/package/lesson drill-down in `frontend/src/components/admin/BunnyCostReports.tsx`.

## Phase 8: Finance Center Dashboard and Cross-Cutting Completion

- [ ] T048 Build the admin-only dashboard, date/teacher/content filters and EGP/USD-separated KPI cards in `frontend/src/features/teacher-finance-center/TeacherFinanceCenterDashboard.tsx`.
- [x] T049 Integrate dashboard routes, navigation and composed workspaces in `frontend/src/app/admin/finance/AdminFinancePageClient.tsx`.
- [x] T050 Add paginated dashboard/ledger queries that avoid N+1 aggregation in `backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs`.
- [ ] T051 Add admin authorization, audit and pagination regression coverage in `backend/tests/NaderGorge.Application.Tests/Finance/AdminFinanceCenterTests.cs`.
- [ ] T052 Add responsive RTL/accessibility states and keyboard-visible focus behavior in `frontend/src/features/teacher-finance-center/`.
- [ ] T053 Reconcile legacy events and aggregate payouts with the documented cutover strategy in `backend/src/NaderGorge.Infrastructure/Migrations/` and `specs/165-teacher-finance-center/quickstart.md`.

## Phase 9: Quality Gates, Feature Tests and Final Verification

- [x] T054 Run the feature tests in `backend/tests/NaderGorge.Application.Tests/Finance/` and record expected passing outcomes in `specs/165-teacher-finance-center/quickstart.md`.
- [x] T055 Run `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj -c Release --no-restore` and `cd frontend && npm run lint && npm run build`; record results in `specs/165-teacher-finance-center/quickstart.md`.
- [ ] T056 Run `docker compose config -q`, `make up`, `make migrate`, `make ps` and surface health checks; record unavailable Bunny credentials as an external dependency in `specs/165-teacher-finance-center/quickstart.md`.
- [ ] T057 Conduct the roles and flows in `specs/165-teacher-finance-center/spec.md` Manual QA section with expected result/pass-fail evidence in `specs/165-teacher-finance-center/quickstart.md`.
- [ ] T058 Run deep architectural and UI/UX critique against `specs/165-teacher-finance-center/{spec,plan,tasks}.md` and apply accepted findings to implementation files.
- [ ] T059 Run clean-code-guard over changed backend/frontend production files and resolve findings before handoff.
- [ ] T060 Run test-guard over changed finance tests and resolve reliability findings before handoff.

## Dependencies and Execution Order

`Setup → Foundation → US1 → US2/US3 → US4 → US5 → Dashboard → Quality Gates`.

- US1 is the MVP and establishes agreement snapshots.
- US2 and US3 depend on the foundational ledger; US3 also consumes agreement resolution.
- US4 depends on settlement-line allocation from US3.
- US5 is independent of EGP settlement arithmetic after the foundation, but the unified dashboard consumes it last.

## Parallel Opportunities

- T006, T007 and T008 can be prepared in parallel before the mapping/migration integration.
- Within each story, `[P]` API-client and test tasks can proceed beside backend command work once contracts are stable.
- Bunny reporting (US5) may be developed beside reversals (US4) after foundation completion.

## Implementation Strategy

Deliver the agreement and content-sale path first, prove its exact allocation result, then attach code and shared-package sources. Introduce settlement reservation before any payment UI, then add reversals and Bunny profitability. The final finance dashboard only consumes the already tested source-of-truth queries.
