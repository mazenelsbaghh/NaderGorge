# Tasks: Gifts and Free Access

**Input**: Design documents from `/specs/152-gifts-free-access/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/gifts-api.yaml`, `quickstart.md`

**Tests**: Backend behavioral tests, frontend build/lint, Playwright E2E, Docker health, and SQL invariants are mandatory. Manual QA remains `pending` until the product owner performs it.

**Organization**: Tasks are ordered by dependency and grouped by user story. Every completed story must produce a testable platform increment.

## Phase 1: Setup and Contract Alignment

**Purpose**: Establish feature-owned files and lock shared contracts before behavioral changes.

- [ ] T001 Create the Admin gift feature folders and shared request/response models in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Models/GiftModels.cs`
- [ ] T002 [P] Add gift enums with stable integer values in `backend/src/NaderGorge.Domain/Enums/GiftEnums.cs`
- [ ] T003 [P] Add strict frontend gift DTOs and target/status label maps in `frontend/src/services/admin-gifts-service.ts`
- [ ] T004 Add initial failing serialization/validation contract tests for all target discriminators and target-aware use limits in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`

**Checkpoint**: Gift target vocabulary is identical across spec, backend, and frontend.

---

## Phase 2: Foundational Data and Transaction Primitives

**Purpose**: Add shared persistence and service contracts that block all user stories.

- [ ] T005 Define `GiftIssuance`, `GiftRecipient`, `PromotionalBalanceAllocation`, and `PromotionalBalanceUsage` with required navigation properties in `backend/src/NaderGorge.Domain/Entities/GiftEntities.cs`
- [ ] T006 Extend `StudentAccessGrant` with nullable gift linkage and guarded use counters in `backend/src/NaderGorge.Domain/Entities/CodeEntities.cs`
- [ ] T007 Add gift DbSets to the application persistence contract in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [ ] T008 Add `IPromotionalBalanceService` and `IGiftUsageService` contracts, including funding preview/commit and atomic consume results, in `backend/src/NaderGorge.Domain/Interfaces/IPromotionalBalanceService.cs` and `backend/src/NaderGorge.Domain/Interfaces/IGiftUsageService.cs`
- [ ] T009 Configure all gift relationships, discriminator checks, monetary conservation checks, unique idempotency keys, recipient uniqueness, filtered indexes, and decimal precision in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [ ] T010 Scaffold and inspect the `AddGiftsAndPromotionalBalance` migration and model snapshot in `backend/src/NaderGorge.Infrastructure/Migrations/` so it includes Spec 151 state and only Spec 152 additions
- [ ] T011 Add failing database invariant tests for duplicate request ids, duplicate recipients, invalid target shapes, negative values, and broken monetary conservation in `backend/tests/NaderGorge.Integration.Tests/GiftPersistenceTests.cs`
- [ ] T012 Register feature services in dependency injection without changing worker or mobile registrations in `backend/src/NaderGorge.API/Program.cs`

**Checkpoint**: Migration applies on top of Spec 151 and database constraints reject impossible gift state.

---

## Phase 3: User Story 1 - Issue Direct Content Gifts (Priority: P1) MVP

**Goal**: Authorized staff can grant package, lesson, video, or exam access to 1-100 students without payment, with per-recipient outcomes and correct target-aware limits.

**Independent Test**: Issue each direct target to valid, duplicate, invalid, and already-entitled students; verify valid grants, no balance transactions, exact video isolation, expiration, and use counting.

### Tests for User Story 1

- [ ] T013 [P] [US1] Add failing tests for authorized direct issuance, deduplication, partial recipient success, already-entitled outcomes, and request replay idempotency in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`
- [ ] T014 [P] [US1] Add failing tests proving video-only access excludes siblings/resources and consumes only successful new sessions in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`
- [ ] T015 [P] [US1] Add failing tests proving exam gift use is consumed only for a fresh attempt and not an in-progress resume in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`
- [ ] T016 [P] [US1] Add failing API authorization tests proving missing `gifts.manage` creates no issuance or grant in `backend/tests/NaderGorge.Integration.Tests/AdminGiftsApiTests.cs`

### Implementation for User Story 1

- [ ] T017 [US1] Implement target/header validation and recipient-level validation/result creation in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs`
- [ ] T018 [US1] Implement request replay lookup and unique-conflict recovery so the original issuance is returned without duplicate value in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs`
- [ ] T019 [US1] Create content grants linked to successful recipients without paid balance or purchase records in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs`
- [ ] T020 [US1] Add video-specific entitlement resolution to `backend/src/NaderGorge.Domain/Interfaces/IAccessCheckService.cs` and `backend/src/NaderGorge.Application/Services/AccessCheckService.cs`
- [ ] T021 [US1] Implement atomic gift-source consumption and entitlement precedence in `backend/src/NaderGorge.Application/Services/GiftUsageService.cs`
- [ ] T022 [US1] Permit a direct video grant and consume one use only after successful fresh session creation in `backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs`
- [ ] T023 [US1] Consume one exam use only when creating a fresh attempt and preserve in-progress resume behavior in `backend/src/NaderGorge.Application/Features/Exams/Commands/StartExamAttemptCommand.cs`
- [ ] T024 [US1] Return a partial lesson view containing only directly granted videos and no sibling resources/homework when lesson access is absent in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs`
- [ ] T025 [US1] Add gift-specific active student, teacher, and direct-content target lookup queries under `backend/src/NaderGorge.Application/Features/Admin/Gifts/Queries/`
- [ ] T026 [US1] Expose issue and lookup endpoints under class-level `gifts.manage` protection in `backend/src/NaderGorge.API/Controllers/AdminGiftsController.cs`
- [ ] T027 [P] [US1] Implement the compact target picker, recipient multi-select, target-aware terms, and review summary in `frontend/src/components/admin/gifts/GiftIssueForm.tsx`
- [ ] T028 [US1] Implement `/admin/gifts/new` with loading, validation, partial-success, retry, and idempotent request-id handling in `frontend/src/app/admin/gifts/new/page.tsx`
- [ ] T029 [US1] Add issue/lookups methods and normalized API errors in `frontend/src/services/admin-gifts-service.ts`

**Checkpoint**: Direct gifts are usable end to end without unlocking unrelated content or touching paid balance.

---

## Phase 4: User Story 2 - Issue Promotional Balance Gifts (Priority: P1)

**Goal**: Authorized staff can issue general or teacher-restricted promotional balance that funds eligible purchases before paid balance and never creates revenue from the gifted portion.

**Independent Test**: Issue both scopes, purchase same-teacher and other-teacher content, test earliest-expiry and mixed funding, and prove monetary conservation during concurrent purchases.

### Tests for User Story 2

- [ ] T030 [P] [US2] Add failing tests for general/restricted issuance, expiry, purchase-count caps, and separation from paid balance in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`
- [ ] T031 [P] [US2] Add failing tests for teacher ownership resolution, earliest-expiry ordering, mixed promotional/paid funding, and paid-only fallback in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`
- [ ] T032 [P] [US2] Add concurrent integration tests proving no overspend, one entitlement, conserved allocations, and transaction rollback on conflict in `backend/tests/NaderGorge.Integration.Tests/PromotionalPurchaseConcurrencyTests.cs`
- [ ] T033 [P] [US2] Add event/accounting regression tests proving promotional portions create no teacher commission, payout, or platform sales revenue in `backend/tests/NaderGorge.Application.Tests/EventContractTests.cs`

### Implementation for User Story 2

- [ ] T034 [US2] Extend issuance to create one conserved general or teacher-restricted allocation per successful recipient in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs`
- [ ] T035 [US2] Implement lazy atomic expiration, eligible allocation queries, earliest-expiry preview, and conditional allocation consumption in `backend/src/NaderGorge.Application/Services/PromotionalBalanceService.cs`
- [ ] T036 [US2] Resolve authoritative teacher ownership for every supported purchasable content type in `backend/src/NaderGorge.Application/Services/PromotionalBalanceService.cs`
- [ ] T037 [US2] Wrap promotional usage, paid deduction, access grant, and purchase event in one serializable transaction in `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`
- [ ] T038 [US2] Extend purchase result/event contracts with promotional and paid portions and gate existing revenue side effects to paid value in `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`
- [ ] T039 [US2] Extend student balance projection with paid total, promotional totals, per-allocation scope/expiry/use data, and no administrative reason in `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentBalanceQuery.cs`
- [ ] T040 [P] [US2] Update balance DTOs and eligible funding preview/result handling in `frontend/src/services/balance-service.ts`
- [ ] T041 [P] [US2] Display paid and promotional totals plus teacher/expiry restrictions in `frontend/src/components/balance/BalanceDisplay.tsx`
- [ ] T042 [US2] Show estimated and final promotional/paid funding split and ineligible-restriction messaging in `frontend/src/components/balance/PurchaseContentModal.tsx`
- [ ] T043 [US2] Integrate allocation details, loading, empty, and error states in `frontend/src/app/student/balance/StudentBalancePageClient.tsx`

**Checkpoint**: Promotional value is spendable only where eligible, conserved under concurrency, and visibly separate from paid funds.

---

## Phase 5: User Story 3 - Track and Revoke Remaining Gifts (Priority: P2)

**Goal**: Gift managers can search complete issuance evidence and revoke only future/unspent value with an audited reason.

**Independent Test**: Partially consume direct and balance gifts, inspect recipient evidence, revoke twice, and verify prior sessions/attempts/usages remain while unused remainder is unavailable.

### Tests for User Story 3

- [ ] T044 [P] [US3] Add failing list/detail query tests for search, filters, paging, recipient outcomes, aggregate totals, and derived expired states in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`
- [ ] T045 [P] [US3] Add failing revocation tests for unused/partial/completed/expired/already-revoked gifts and concurrent purchase/revocation in `backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs`
- [ ] T046 [P] [US3] Add audit tests for issuance, outcome, use, expiry, revocation, replay, and denied destructive action evidence in `backend/tests/NaderGorge.Integration.Tests/AdminGiftsApiTests.cs`

### Implementation for User Story 3

- [ ] T047 [US3] Implement ordered paged ledger filtering and aggregate projection in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Queries/GetGiftsQuery.cs`
- [ ] T048 [US3] Implement details projection with recipient grant/allocation/use evidence and safe outcome messages in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Queries/GetGiftDetailsQuery.cs`
- [ ] T049 [US3] Implement reason-required idempotent revocation that deactivates future direct access or moves only available promotional value in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/RevokeGiftCommand.cs`
- [ ] T050 [US3] Write existing-format audit records for all successful and denied gift transitions in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs`, `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/RevokeGiftCommand.cs`, and gift consumption services
- [ ] T051 [US3] Expose list, detail, and revoke endpoints with stable status codes in `backend/src/NaderGorge.API/Controllers/AdminGiftsController.cs`
- [ ] T052 [P] [US3] Build searchable/filterable ledger table with compact status/value/recipient columns in `frontend/src/components/admin/gifts/GiftLedgerTable.tsx`
- [ ] T053 [US3] Implement the ledger route with query-state paging and explicit loading/empty/error states in `frontend/src/app/admin/gifts/page.tsx`
- [ ] T054 [P] [US3] Build recipient outcome/evidence table and reason-required revoke dialog in `frontend/src/components/admin/gifts/GiftDetailsPanel.tsx`
- [ ] T055 [US3] Implement gift detail route and idempotent revoke refresh behavior in `frontend/src/app/admin/gifts/[id]/page.tsx`
- [ ] T056 [US3] Add list/detail/revoke methods and typed paging/filter parameters in `frontend/src/services/admin-gifts-service.ts`

**Checkpoint**: Every granted or rejected recipient is explainable, and revocation changes no completed activity or paid funds.

---

## Phase 6: User Story 4 - Shell and Permission Discovery (Priority: P2)

**Goal**: Only Admin or delegated `gifts.manage` staff can discover/use gifts, while direct video-type management remains built-in Admin-only.

**Independent Test**: Compare shell links, direct routes, and APIs for Admin, gift manager, content manager, and unauthorized staff.

### Tests for User Story 4

- [ ] T057 [P] [US4] Add failing Playwright role tests for gift shell visibility, direct-route denial, permission assignment, and Admin bypass in `frontend/tests/e2e/admin-gifts.spec.ts`
- [ ] T058 [P] [US4] Extend Admin content E2E tests for direct video-types shell visibility and Admin-only route/mutation behavior in `frontend/tests/e2e/admin-content.spec.ts`

### Implementation for User Story 4

- [ ] T059 [US4] Add `gifts.manage` definition, Arabic description, eligible role assignment, and nav mapping in `frontend/src/app/admin/settings/AdminSettingsPageClient.tsx`
- [ ] T060 [US4] Add gifts navigation for Admin/permission holders and direct Admin-only video-types navigation in `frontend/src/components/admin/AdminShellChrome.tsx`
- [ ] T061 [US4] Add gifts and Admin-only video-types entries consistently to legacy package navigation in `frontend/src/packages/admin/navigation.tsx`
- [ ] T062 [US4] Add specific route protection for `/admin/gifts/**` and `/admin/content/video-types` before generic content matching in `frontend/src/app/admin/layout.tsx`
- [ ] T063 [US4] Verify `AdminVideoTypesController` keeps listing compatible with content forms while all catalog mutations remain Admin-only in `backend/src/NaderGorge.API/Controllers/AdminVideoTypesController.cs`

**Checkpoint**: Navigation and direct access agree with backend authorization for every required role.

---

## Phase 7: Cross-Cutting Quality and Regression Coverage

**Purpose**: Close security, accessibility, maintainability, and regression gaps across all stories.

- [ ] T064 [P] Add FluentValidation coverage for request size, duplicate recipients, target shape, amount, future expiry, max uses, and reasons in `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs` and `RevokeGiftCommand.cs`
- [ ] T065 [P] Add RTL responsive and keyboard/focus assertions for issue, ledger, details, and dialogs in `frontend/tests/e2e/admin-gifts.spec.ts`
- [ ] T066 Add full Playwright happy/partial/denied/revoke/student-balance journeys using deterministic fixtures in `frontend/tests/e2e/admin-gifts.spec.ts`
- [ ] T067 Run the `clean-code-guard` workflow on all changed production files and apply only behavior-preserving fixes documented in `specs/152-gifts-free-access/review-report.md`
- [ ] T068 Run the `test-guard` workflow on all changed test files; remove redundant/brittle coverage and document retained risk-focused cases in `specs/152-gifts-free-access/review-report.md`
- [ ] T069 Perform deep architecture and UI/UX critique against `PRODUCT.md`, `DESIGN.md`, the constitution, and gift invariants; resolve P0/P1 findings in `specs/152-gifts-free-access/review-report.md`

---

## Phase 8: End-of-Phase Verification, Docker Gate, and Owner QA Handoff

**Purpose**: Produce concrete evidence and a runnable feature before completion is reported.

- [ ] T070 Run backend feature tests in both application and integration test projects and record exact totals/failures in `specs/152-gifts-free-access/verification-report.md`
- [ ] T071 Run frontend lint, production build, and focused Chromium Playwright specs and record evidence in `specs/152-gifts-free-access/verification-report.md`
- [ ] T072 Run `docker compose config -q`, `make up`, `make migrate`, `make ps`, and `make verify-surfaces`; record container/migration/health evidence in `specs/152-gifts-free-access/verification-report.md`
- [ ] T073 Execute the conservation, duplicate, dangling-link, paid-balance, and revenue SQL checks from `quickstart.md`; record zero-row evidence in `specs/152-gifts-free-access/verification-report.md`
- [ ] T074 Verify the live Admin and Student surfaces at desktop and mobile viewports with Playwright screenshots, checking no overlaps and correct RTL states, and record paths/results in `specs/152-gifts-free-access/verification-report.md`
- [ ] T075 Publish the role-by-role owner checklist from `quickstart.md` with every unperformed item explicitly `pending` in `specs/152-gifts-free-access/verification-report.md`
- [ ] T076 Update `achievements.md` with completed phase evidence, unresolved risks, manual QA status, and explicit go/no-go; do not mark manual QA complete without owner execution

---

## Dependencies and Execution Order

### Phase Dependencies

- Phase 1 has no dependency.
- Phase 2 depends on Phase 1 and blocks all stories.
- US1 and US2 both depend on Phase 2. Implement US1 first because video/exam access and the issuance aggregate are the MVP.
- US2 depends on the shared issuance path from US1 but remains independently testable through promotional allocations and purchases.
- US3 depends on persisted direct and promotional gifts from US1/US2.
- US4 can start after Phase 2, but final role E2E depends on the Admin routes from US1/US3.
- Phases 7-8 depend on all selected stories.

### User Story Dependency Graph

```text
Setup -> Foundation -> US1 Direct Gifts -> US2 Promotional Balance -> US3 Ledger/Revoke -> Quality -> Verification
                         \---------------------------------> US4 Permissions/Shell --------/
```

### Parallel Opportunities

- T002 and T003 can run in parallel after T001.
- T013-T016 can be authored in parallel before US1 implementation.
- T027 can proceed in parallel with backend US1 work once DTOs are stable.
- T030-T033 can be authored in parallel before US2 implementation.
- T040-T041 can proceed in parallel after the balance response contract is fixed.
- T044-T046 and T052/T054 can be developed in parallel against the locked contracts.
- T057-T058 can be authored in parallel before shell changes.
- T067-T069 are independent review passes but their fixes must be reconciled before verification.

## Implementation Strategy

### MVP First

1. Complete Phases 1-2.
2. Complete US1 direct gifts.
3. Run US1 application/API tests and demonstrate one package, lesson, video, and exam gift.
4. Continue to US2 only after direct grants do not affect paid balance and video isolation passes.

### Incremental Outputs

- After US1: `/admin/gifts/new` issues direct gifts that students can use.
- After US2: students can spend general/restricted promotional balances with a visible funding split.
- After US3: `/admin/gifts` and `/admin/gifts/[id]` provide ledger and safe revocation.
- After US4: roles, shell, direct routes, and video-type management are consistent.
- After Phase 8: Docker is migrated and running, automated evidence is complete, and owner manual QA is ready as `pending`.

## Task Format Validation

- All tasks use `- [ ] T###` in execution order.
- Story tasks include exactly one `[US#]` label.
- `[P]` appears only where work can proceed in different files or as independent test authoring.
- Every task names one or more concrete repository paths or commands with a report path.
