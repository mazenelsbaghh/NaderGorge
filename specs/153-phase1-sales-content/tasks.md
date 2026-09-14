# Tasks: Phase 1 Sales and Content Completion

**Input**: Design documents from `/specs/153-phase1-sales-content/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/phase1-sales-api.yaml, quickstart.md
**Tests**: Mandatory for backend financial/access behavior, API permissions, frontend Admin/Student workflows, and regression of legacy codes/gifts.
**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Phase 1: Setup and Baseline

**Purpose**: Capture current state and create empty feature surfaces without changing behavior.

- [ ] T001 Record current worktree state with `git status --short` and review diffs for files listed in `specs/153-phase1-sales-content/plan.md`.
- [ ] T002 [P] Create backend test skeleton `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` with shared setup helpers and failing placeholders for sales rules, coupons, printable codes, stacking, and checkout.
- [ ] T003 [P] Create backend test skeleton `backend/tests/NaderGorge.Application.Tests/PublicExamProductTests.cs` with failing placeholders for public exam create, access, disable, and report separation.
- [ ] T004 [P] Create frontend E2E skeleton `frontend/tests/e2e/admin-sales.spec.ts` covering admin coupon/template/batch mocked routes.
- [ ] T005 [P] Create frontend E2E skeleton `frontend/tests/e2e/public-exams.spec.ts` covering student public exam list, purchase, start, and disabled state with mocked API.

## Phase 2: Foundational Domain, Persistence, and Services

**Purpose**: Shared sales/public-exam primitives that block all user stories.

- [ ] T006 [P] Add sales enums `SalesTargetType`, `DiscountType`, `SalesOwnerType`, `SalesStatus`, `StackingMode`, and `PrintableCodeBehavior` in `backend/src/NaderGorge.Domain/Enums/SalesEnums.cs`.
- [ ] T007 [P] Add sales entities from `data-model.md` in `backend/src/NaderGorge.Domain/Entities/SalesEntities.cs`.
- [ ] T008 Add `PublicExamProductId` optional linkage if needed and navigation changes in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs` and `backend/src/NaderGorge.Domain/Entities/CodeEntities.cs`.
- [ ] T009 Add DbSets for sales/public-exam entities and service interfaces in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`.
- [ ] T010 [P] Add `IDiscountEngine`, `ISalesTargetResolver`, and `ISalesRedemptionService` interfaces in `backend/src/NaderGorge.Domain/Interfaces/`.
- [ ] T011 Configure EF tables, indexes, unique constraints, check constraints, relationships, and JSON fields in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [ ] T012 Create migration `AddPhase1SalesContent` under `backend/src/NaderGorge.Infrastructure/Migrations/` and seed default `SingleOnly` stacking policy.
- [ ] T013 Register Sales services in `backend/src/NaderGorge.API/Program.cs`.
- [ ] T014 Add permissions `sales.manage`, `sales.templates.manage`, and `public_exams.manage` to `frontend/src/app/admin/settings/AdminSettingsPageClient.tsx`, `frontend/src/app/admin/layout.tsx`, `frontend/src/components/admin/AdminShellChrome.tsx`, and `frontend/src/packages/admin/navigation.tsx`.
- [ ] T015 Implement `SalesTargetResolver` in `backend/src/NaderGorge.Application/Services/SalesTargetResolver.cs` to resolve price, teacher, subject, grade, sale eligibility, and public-exam metadata server-side.
- [ ] T016 Implement `DiscountEngine` in `backend/src/NaderGorge.Application/Services/DiscountEngine.cs` to validate coupons/printable code discounts, apply stacking policy, cap discounts, and produce preview/final calculation records.
- [ ] T017 Implement `SalesFinancialEffectService` in `backend/src/NaderGorge.Application/Services/SalesFinancialEffectService.cs` to persist gross/discount/promotional/paid teacher-platform effect rows without updating payout balances.
- [ ] T018 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Phase1SalesContentTests|FullyQualifiedName~PublicExamProductTests"` and confirm current foundational tests fail for missing implementation before story work.

## Phase 3: User Story 1 - Sell by Content Type Rules (Priority: P1)

**Goal**: Sales and code eligibility cannot target ambiguous content.

**Independent Test**: Admin creates a video-type sales rule, eligible video matches, ineligible/missing teacher/type content is rejected.

### Tests for US1

- [ ] T019 [P] [US1] Add target resolver tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` for package, lesson, specific video, video type, teacher, platform, and unresolved teacher failure.
- [ ] T020 [P] [US1] Add sales-rule command/query tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` for activation, duplicate rule rejection, and missing classification rejection.

### Implementation for US1

- [ ] T021 [US1] Add Admin Sales rule models in `backend/src/NaderGorge.Application/Features/Admin/Sales/Models/SalesRuleModels.cs`.
- [ ] T022 [US1] Add `CreateOrUpdateSalesRuleCommand` and handler in `backend/src/NaderGorge.Application/Features/Admin/Sales/Commands/CreateOrUpdateSalesRuleCommand.cs`.
- [ ] T023 [US1] Add `GetSalesRulesQuery` and target lookup query in `backend/src/NaderGorge.Application/Features/Admin/Sales/Queries/`.
- [ ] T024 [US1] Add `AdminSalesController` rule endpoints from `contracts/phase1-sales-api.yaml` in `backend/src/NaderGorge.API/Controllers/AdminSalesController.cs` protected by `sales.manage`.
- [ ] T025 [P] [US1] Add frontend sales service rule DTOs/functions in `frontend/src/services/admin-sales-service.ts`.
- [ ] T026 [P] [US1] Add Admin sales rule page at `frontend/src/app/admin/sales/rules/page.tsx` and client `frontend/src/app/admin/sales/rules/SalesRulesPageClient.tsx`.
- [ ] T027 [US1] Add reusable target selector component in `frontend/src/components/admin/sales/SalesTargetSelector.tsx` using existing package/teacher/video-type lookup patterns.
- [ ] T028 [US1] Verify US1 with `dotnet test ... --filter "FullyQualifiedName~Phase1SalesContentTests"` and mocked admin E2E route segment in `frontend/tests/e2e/admin-sales.spec.ts`.

## Phase 4: User Story 2 - Manage Digital Discount Coupons (Priority: P1)

**Goal**: Admin creates scoped digital coupons and checkout applies them atomically.

**Independent Test**: Create percentage/fixed coupons, preview/apply valid purchase, reject expired/out-of-scope/over-limit attempts with no state changes.

### Tests for US2

- [ ] T029 [P] [US2] Add coupon command tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` for create/update/disable, unique normalized code, invalid values, and permission denial.
- [ ] T030 [P] [US2] Add discount engine tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` for fixed/percentage caps, admin stacking policy, per-student limit, global limit, and out-of-scope rejection.
- [ ] T031 [P] [US2] Add checkout tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` proving coupon usage commits only on successful purchase and rolls back on insufficient balance.

### Implementation for US2

- [ ] T032 [US2] Add coupon models in `backend/src/NaderGorge.Application/Features/Admin/Sales/Models/CouponModels.cs`.
- [ ] T033 [US2] Add `CreateCouponCommand`, `UpdateCouponCommand`, and `DisableCouponCommand` in `backend/src/NaderGorge.Application/Features/Admin/Sales/Commands/`.
- [ ] T034 [US2] Add `GetCouponsQuery` and `GetCouponDetailsQuery` in `backend/src/NaderGorge.Application/Features/Admin/Sales/Queries/`.
- [ ] T035 [US2] Add stacking policy commands/queries in `backend/src/NaderGorge.Application/Features/Admin/Sales/Commands/SaveStackingPolicyCommand.cs` and `backend/src/NaderGorge.Application/Features/Admin/Sales/Queries/GetStackingPoliciesQuery.cs`.
- [ ] T036 [US2] Extend `backend/src/NaderGorge.API/Controllers/AdminSalesController.cs` with coupon and stacking policy endpoints protected by `sales.manage`.
- [ ] T037 [US2] Extend `backend/src/NaderGorge.Application/Features/Student/Queries/GetPurchaseFundingPreviewQuery.cs` to accept coupon/printable-code inputs and return gross, discounts, promotional, paid, and rejection reasons.
- [ ] T038 [US2] Extend `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs` to validate/apply discounts inside the serializable purchase transaction and persist `SalesCouponUsage` and `SalesFinancialEffect`.
- [ ] T039 [US2] Extend `backend/src/NaderGorge.API/Controllers/BalanceController.cs` or add purchase endpoints to support POST preview and purchase requests with `requestId`, `couponCodes`, and `printableCodes`.
- [ ] T040 [P] [US2] Add coupon UI service DTOs in `frontend/src/services/admin-sales-service.ts`.
- [ ] T041 [P] [US2] Add Admin coupon list/create/detail UI in `frontend/src/app/admin/sales/coupons/`.
- [ ] T042 [P] [US2] Add stacking policy UI in `frontend/src/app/admin/sales/settings/StackingPolicyPanel.tsx`.
- [ ] T043 [US2] Update `frontend/src/components/balance/PurchaseContentModal.tsx` and `frontend/src/services/balance-service.ts` to submit coupon inputs, show previewed discount rows, and show rejection messages.
- [ ] T044 [US2] Verify US2 with focused backend tests, `npm run lint`, and coupon flow in `frontend/tests/e2e/admin-sales.spec.ts`.

## Phase 5: User Story 5 - Sell Standalone Public Exams (Priority: P1)

**Goal**: Public exams are independent free/paid products with separate access and reporting.

**Independent Test**: Admin publishes free/paid public exams, student enters/buys/submits, disabled exam blocks new starts, prior results remain visible.

### Tests for US5

- [ ] T045 [P] [US5] Add public exam lifecycle tests in `backend/tests/NaderGorge.Application.Tests/PublicExamProductTests.cs` for create, publish, paid/free access, disabled behavior, and previous result preservation.
- [ ] T046 [P] [US5] Add public exam checkout tests in `backend/tests/NaderGorge.Application.Tests/PublicExamProductTests.cs` for paid exam purchase and exam-only access grant.
- [ ] T047 [P] [US5] Add report separation tests in `backend/tests/NaderGorge.Application.Tests/PublicExamProductTests.cs` for public exam attempts not mixing with lesson/video reports.

### Implementation for US5

- [ ] T048 [US5] Add PublicExam application models in `backend/src/NaderGorge.Application/Features/Admin/PublicExams/Models/PublicExamModels.cs`.
- [ ] T049 [US5] Add `CreatePublicExamCommand`, `UpdatePublicExamCommand`, `PublishPublicExamCommand`, and `DisablePublicExamCommand` in `backend/src/NaderGorge.Application/Features/Admin/PublicExams/Commands/`.
- [ ] T050 [US5] Add Admin public exam list/detail/report queries in `backend/src/NaderGorge.Application/Features/Admin/PublicExams/Queries/`.
- [ ] T051 [US5] Add Student public exam list/detail queries in `backend/src/NaderGorge.Application/Features/Student/PublicExams/Queries/`.
- [ ] T052 [US5] Add `AdminPublicExamsController` in `backend/src/NaderGorge.API/Controllers/AdminPublicExamsController.cs` protected by `public_exams.manage`.
- [ ] T053 [US5] Add `PublicExamsController` in `backend/src/NaderGorge.API/Controllers/PublicExamsController.cs` for student list/detail and start routing.
- [ ] T054 [US5] Extend `backend/src/NaderGorge.Application/Features/Exams/Commands/StartExamAttemptCommand.cs` to enforce public-exam lifecycle and access rules before existing lesson progression checks.
- [ ] T055 [US5] Extend `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs` and `backend/src/NaderGorge.Application/Features/Admin/Queries/GetExamDashboardQuery.cs` to expose public-exam report markers.
- [ ] T056 [US5] Extend `PurchaseContentCommand.cs`, `GetPurchaseFundingPreviewQuery.cs`, and purchase DTOs to support `PublicExam` content type.
- [ ] T057 [P] [US5] Add frontend Admin public exam service in `frontend/src/services/admin-public-exams-service.ts`.
- [ ] T058 [P] [US5] Add frontend Student public exam service in `frontend/src/services/public-exams-service.ts`.
- [ ] T059 [US5] Add Admin public exam routes under `frontend/src/app/admin/public-exams/`.
- [ ] T060 [US5] Add Student public exam routes under `frontend/src/app/student/public-exams/`.
- [ ] T061 [US5] Verify US5 with focused backend tests and `frontend/tests/e2e/public-exams.spec.ts`.

## Phase 6: User Story 3 - Create Printable QR/Serial Sales Codes (Priority: P2)

**Goal**: Admin generates printable code batches and student redeems codes safely.

**Independent Test**: Generate batch, preview QR/serial data, redeem valid code, reject duplicate/expired/disabled code, preserve legacy CodeGroup behavior.

### Tests for US3

- [ ] T062 [P] [US3] Add printable batch tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` for quantity limits, unique serials, unique hashes, expiry, and disabled state.
- [ ] T063 [P] [US3] Add printable redemption tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` for idempotency, single-use concurrency guard, direct access, discount behavior, and audit.
- [ ] T064 [P] [US3] Add legacy `CodeGroup` regression tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` proving old `/codes/activate` package/lesson/video/exam/balance behavior still works.

### Implementation for US3

- [ ] T065 [US3] Add printable batch models in `backend/src/NaderGorge.Application/Features/Admin/Sales/Models/PrintableCodeModels.cs`.
- [ ] T066 [US3] Add `CreatePrintableBatchCommand`, `DisablePrintableBatchCommand`, and `PreviewPrintableBatchQuery` in `backend/src/NaderGorge.Application/Features/Admin/Sales/`.
- [ ] T067 [US3] Implement `SalesRedemptionService` in `backend/src/NaderGorge.Application/Services/SalesRedemptionService.cs` for request-id idempotency, access grant creation, discount redemption, and audit.
- [ ] T068 [US3] Extend `backend/src/NaderGorge.API/Controllers/AdminSalesController.cs` with printable batch endpoints protected by `sales.manage`.
- [ ] T069 [US3] Extend `backend/src/NaderGorge.API/Controllers/CodesController.cs` with `/api/codes/redeem-printable` while preserving `/api/codes/activate`.
- [ ] T070 [P] [US3] Add Admin printable batch UI in `frontend/src/app/admin/sales/printable-codes/`.
- [ ] T071 [P] [US3] Update student code redemption UI in `frontend/src/app/student/code-redemption/StudentCodeRedemptionPageClient.tsx` to support advanced printable redemption response.
- [ ] T072 [US3] Verify US3 with focused backend tests and printable-code flow in `frontend/tests/e2e/admin-sales.spec.ts`.

## Phase 7: User Story 4 - Design Simple Code Templates (Priority: P2)

**Goal**: Admin builds reusable printable code templates with fixed draggable fields.

**Independent Test**: Save template with QR/code/serial, preview batch cards, reject missing redemption identifier or out-of-bounds field.

### Tests for US4

- [ ] T073 [P] [US4] Add template validation tests in `backend/tests/NaderGorge.Application.Tests/Phase1SalesContentTests.cs` for required QR/code, bounds, allowed fields, and disabled templates.
- [ ] T074 [P] [US4] Add Playwright coverage in `frontend/tests/e2e/admin-sales.spec.ts` for drag/drop or keyboard/numeric positioning, save, and preview.

### Implementation for US4

- [ ] T075 [US4] Add template models and validators in `backend/src/NaderGorge.Application/Features/Admin/Sales/Models/PrintableTemplateModels.cs`.
- [ ] T076 [US4] Add `SavePrintableTemplateCommand`, `DisablePrintableTemplateCommand`, and `GetPrintableTemplatesQuery` in `backend/src/NaderGorge.Application/Features/Admin/Sales/`.
- [ ] T077 [US4] Extend `backend/src/NaderGorge.API/Controllers/AdminSalesController.cs` with template endpoints protected by `sales.templates.manage`.
- [ ] T078 [P] [US4] Add template designer service methods in `frontend/src/services/admin-sales-service.ts`.
- [ ] T079 [US4] Add bounded designer component `frontend/src/components/admin/sales/PrintableTemplateDesigner.tsx` with fixed fields QR/code/serial/owner/target/value/expiry and stable card dimensions.
- [ ] T080 [US4] Add template list/designer routes under `frontend/src/app/admin/sales/templates/`.
- [ ] T081 [US4] Connect printable batch preview to saved templates in `frontend/src/app/admin/sales/printable-codes/`.
- [ ] T082 [US4] Verify US4 with backend template tests, `npm run lint`, and `frontend/tests/e2e/admin-sales.spec.ts`.

## Phase 8: Polish, Documentation, and Roadmap Status

**Purpose**: Cross-story hardening and documentation.

- [ ] T083 Update `docs/platform-change-roadmap.md` Phase 1 checkboxes only for items with implemented evidence; keep manual QA unchecked until owner runs it.
- [ ] T084 Update `specs/153-phase1-sales-content/quickstart.md` if implementation changes endpoint paths, commands, or SQL table names.
- [ ] T085 [P] Add `specs/153-phase1-sales-content/review-report.md` with architecture/UI/security findings and fixes.
- [ ] T086 [P] Add `specs/153-phase1-sales-content/verification-report.md` with command results, Docker status, and manual QA pending status.
- [ ] T087 Run deep critique and fix every finding in production code before guard phases.
- [ ] T088 Run `clean-code-guard` against changed production-code files and fix every finding.
- [ ] T089 Run `test-guard` against changed test files and fix every finding.

## Phase 9: End-of-Phase Verification, Docker Gate, and Final Report

**Purpose**: Prove the feature is complete before claiming Phase 1 is done.

- [ ] T090 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Phase1SalesContentTests|FullyQualifiedName~PublicExamProductTests"` and record result in `achievements.md`.
- [ ] T091 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-restore` and record result in `achievements.md`.
- [ ] T092 Run `dotnet ef migrations has-pending-model-changes --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API --no-build` and record result.
- [ ] T093 Run `(cd frontend && npm run lint)` and record result.
- [ ] T094 Run `(cd frontend && npx tsc --noEmit)` and record result.
- [ ] T095 Run `(cd frontend && npm run build)` and record result.
- [ ] T096 Run feature tests with `(cd frontend && npx playwright test tests/e2e/admin-sales.spec.ts tests/e2e/public-exams.spec.ts --project=chromium)` and record result.
- [ ] T097 Run `docker compose config -q`, `make up`, `make migrate`, `make ps`, and `make verify-surfaces` when Docker daemon is available; if unavailable, record exact blocker without marking Docker QA complete.
- [ ] T098 Run SQL invariants from `specs/153-phase1-sales-content/quickstart.md` against PostgreSQL when Docker/PostgreSQL is available.
- [ ] T099 Run `python3 .agents/skills/speckit-all/scripts/extract_test_commands.py --spec-dir specs/153-phase1-sales-content` and include extracted commands in final verification.
- [ ] T100 Run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/153-phase1-sales-content` and fix every failure.
- [ ] T101 Mark product-owner manual QA as `pending` in reports unless the owner explicitly records pass/fail evidence.
- [ ] T102 Write final summary in `achievements.md` with implemented files, tests, guard results, Docker/manual status, and readiness.

## Dependencies & Execution Order

- Phase 1 Setup must complete before Foundational.
- Phase 2 Foundational blocks all user stories.
- P1 stories should be delivered in this order: US1 sales rules, US2 coupons, US5 public exams.
- P2 stories follow: US3 printable codes, US4 templates.
- US2 depends on T015-T017 and benefits from US1 target resolution.
- US3 depends on US2 discount engine for discount behavior and on US5 for public-exam direct-access behavior.
- US4 can start after template entities from Phase 2 exist, but batch preview integration depends on US3.
- Phase 8 and Phase 9 depend on all selected implementation stories.

## Parallel Opportunities

- T002-T005 can run in parallel.
- T006, T007, and T010 can run in parallel.
- T019 and T020 can run in parallel.
- T029-T031 can run in parallel.
- T045-T047 can run in parallel.
- T062-T064 can run in parallel.
- T073 and T074 can run in parallel.
- Frontend route/service tasks marked [P] can run after matching backend contracts are stable.

## Implementation Strategy

### MVP First

1. Complete Setup and Foundational tasks T001-T018.
2. Complete US1 T019-T028 to make sale target resolution reliable.
3. Complete US2 T029-T044 to enable digital coupons and checkout discounting.
4. Stop and validate coupon checkout before adding printable/public-exam complexity.

### Full Phase 1 Completion

1. Add US5 public exams after coupon checkout supports public-exam content type.
2. Add US3 printable codes with direct access and discount behavior.
3. Add US4 template designer and batch preview.
4. Complete critique, guards, automated tests, Docker gate, and reports.
