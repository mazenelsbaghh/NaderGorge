# Tasks: Teacher Accounting Phase 3

**Input**: Design documents from `/specs/158-teacher-accounting-phase3/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/
**Tests**: Mandatory for this feature because it changes financial records, permissions, API contracts, database schema, and user-visible UI.
**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare shared files and inspect current contracts before schema work.

- [X] T001 Create a Phase 3 implementation notes section in `specs/158-teacher-accounting-phase3/quickstart.md`
- [X] T002 [P] Review existing finance enums and add implementation notes in `backend/src/NaderGorge.Domain/Enums/PayoutStatus.cs`
- [X] T003 [P] Review existing teacher finance DTO usage in `frontend/src/services/finance-service.ts`
- [X] T004 [P] Review existing teacher/profile/community routes in `frontend/src/app/student/teachers/StudentTeachersPageClient.tsx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the canonical accounting model and migration foundation required by all stories.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Add teacher accounting enums in `backend/src/NaderGorge.Domain/Enums/TeacherAccountingEnums.cs`
- [X] T006 Add `TeacherFinancialEvent`, `TeacherFinancialAllocation`, and `TeacherPayoutAdjustment` entities in `backend/src/NaderGorge.Domain/Entities/TeacherFinancialEvent.cs`
- [X] T007 Add shared package entities in `backend/src/NaderGorge.Domain/Entities/SharedTeacherPackage.cs`
- [X] T008 Extend `TeacherPayout` lifecycle fields in `backend/src/NaderGorge.Domain/Entities/TeacherPayout.cs`
- [X] T009 Extend `TeacherProfile` public profile fields in `backend/src/NaderGorge.Domain/Entities/TeacherProfile.cs`
- [X] T010 Extend `CommunityPost` with optional teacher scope in `backend/src/NaderGorge.Domain/Entities/CommunityPost.cs`
- [X] T011 Add new DbSets to `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [X] T012 Map teacher accounting entities, constraints, indexes, and delete behavior in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [X] T013 Map shared package entities and teacher-scoped community relationships in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [ ] T014 Update realtime change detector groups for finance/shared package/profile changes in `backend/src/NaderGorge.Infrastructure/Data/StaffRealtimeChangeDetector.cs`
- [X] T015 Create EF migration for Phase 3 teacher accounting schema in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [ ] T016 [P] Add EF model tests for ledger constraints and indexes in `backend/tests/NaderGorge.Application.Tests/FinancialDataIntegrityTests.cs`
- [ ] T017 [P] Add shared test fixture helpers for teacher accounting scenarios in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingTestHelpers.cs`

**Checkpoint**: Database model supports ledger, allocations, shared packages, payout lifecycle, and teacher profile/community scope.

---

## Phase 3: User Story 1 - Teacher Views Daily Income and Transactions (Priority: P1) MVP

**Goal**: Teachers can see today totals, balances, calendar buckets, and day-level transaction details for their own earnings only.

**Independent Test**: Record a teacher-owned purchase/code event, open teacher finance, select the day, and verify the teacher sees only their transaction with correct student/content/pricing/share details.

### Tests for User Story 1

- [X] T018 [P] [US1] Add backend tests for teacher ledger creation and day summaries in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`
- [ ] T019 [P] [US1] Add backend authorization tests blocking cross-teacher finance access in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`
- [X] T020 [P] [US1] Add frontend service type tests or compile coverage for teacher finance DTOs in `frontend/src/services/finance-service.ts`

### Implementation for User Story 1

- [X] T021 [US1] Implement `TeacherAccountingService` for idempotent event/allocation creation in `backend/src/NaderGorge.Application/Services/TeacherAccountingService.cs`
- [X] T022 [US1] Register teacher accounting service in dependency injection in `backend/src/NaderGorge.Application/DependencyInjection.cs`
- [X] T023 [US1] Add teacher account summary fields for today, reserved, available, and debt in `backend/src/NaderGorge.Application/Features/Teacher/Finance/Queries/GetTeacherAccountQuery.cs`
- [X] T024 [US1] Add calendar query handler in `backend/src/NaderGorge.Application/Features/Teacher/Finance/Queries/GetTeacherFinanceCalendarQuery.cs`
- [X] T025 [US1] Replace code-log-only transaction query with ledger-backed paginated query in `backend/src/NaderGorge.Application/Features/Teacher/Finance/Queries/GetTeacherTransactionsQuery.cs`
- [X] T026 [US1] Add teacher calendar endpoint in `backend/src/NaderGorge.Api/Controllers/TeacherFinanceController.cs`
- [X] T027 [US1] Update teacher finance service types and methods in `frontend/src/services/finance-service.ts`
- [X] T028 [US1] Redesign teacher finance summary/calendar/transaction UI in `frontend/src/app/teacher/finance/TeacherFinancePageClient.tsx`
- [X] T029 [US1] Add empty/loading/error states for teacher finance calendar and transactions in `frontend/src/app/teacher/finance/TeacherFinancePageClient.tsx`

**Checkpoint**: User Story 1 is fully functional and independently testable.

---

## Phase 4: User Story 2 - Admin Reviews Teacher Dues Before Payout (Priority: P1)

**Goal**: Admins can review suspicious/payable teacher dues, approve them as ready for transfer, reject/hold them, and separately mark payouts as paid after real transfer.

**Independent Test**: Create valid and suspicious events, approve/reject/hold them, request a payout, approve it to ready, mark it paid, and verify balances and audit trail.

### Tests for User Story 2

- [X] T030 [P] [US2] Add payout lifecycle tests for Pending to Approved to Paid and Rejected in `backend/tests/NaderGorge.Application.Tests/Finance/CommissionTests.cs`
- [ ] T031 [P] [US2] Add admin review queue tests for suspicious and rejected events in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`
- [X] T032 [P] [US2] Add refund/cancel adjustment tests for pre-payout reversal and post-payout debt in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`

### Implementation for User Story 2

- [X] T033 [US2] Update `PayoutStatus` enum with ready-for-transfer state in `backend/src/NaderGorge.Domain/Enums/PayoutStatus.cs`
- [X] T034 [US2] Update payout request reservation response fields in `backend/src/NaderGorge.Application/Features/Teacher/Finance/Commands/RequestPayoutCommand.cs`
- [X] T035 [US2] Replace direct paid resolution with approve/mark-paid/reject transitions in `backend/src/NaderGorge.Application/Features/Admin/Finance/Commands/ResolvePayoutCommand.cs`
- [ ] T036 [US2] Add admin teacher financial event review query in `backend/src/NaderGorge.Application/Features/Admin/Finance/Queries/GetTeacherFinancialEventsQuery.cs`
- [ ] T037 [US2] Add admin teacher financial event review command in `backend/src/NaderGorge.Application/Features/Admin/Finance/Commands/ReviewTeacherFinancialEventCommand.cs`
- [X] T038 [US2] Update admin payout query with approval/payment actor fields in `backend/src/NaderGorge.Application/Features/Admin/Finance/Queries/GetPayoutsQuery.cs`
- [X] T039 [US2] Add admin finance review and payout lifecycle endpoints in `backend/src/NaderGorge.Api/Controllers/AdminFinanceController.cs`
- [X] T040 [US2] Update finance service payout statuses and review APIs in `frontend/src/services/finance-service.ts`
- [X] T041 [US2] Update admin finance payout tab actions for approve, reject, and mark paid in `frontend/src/app/admin/finance/AdminFinancePageClient.tsx`
- [X] T042 [US2] Add admin teacher-event review tab with filters in `frontend/src/app/admin/finance/AdminFinancePageClient.tsx`
- [X] T043 [US2] Prevent inactive admin finance tabs from fetching unneeded teacher/package datasets in `frontend/src/app/admin/finance/AdminFinancePageClient.tsx`

**Checkpoint**: User Stories 1 and 2 work independently and payout lifecycle matches user clarification.

---

## Phase 5: User Story 3 - Admin Creates and Sells Multi-Teacher Package (Priority: P1)

**Goal**: Admins create a separate shared package with multiple teachers/subjects and dynamic percentage/fixed allocations; students buy it; each teacher gets only their share.

**Independent Test**: Create a shared package with two teachers, buy it as a student, verify access grants and per-teacher allocations plus platform remainder.

### Tests for User Story 3

- [ ] T044 [P] [US3] Add shared package validation and allocation tests in `backend/tests/NaderGorge.Application.Tests/SharedPackageAccountingTests.cs`
- [ ] T045 [P] [US3] Add shared package purchase/access grant tests in `backend/tests/NaderGorge.Application.Tests/SharedPackageAccountingTests.cs`
- [X] T046 [P] [US3] Add shared package frontend service compile coverage in `frontend/src/services/shared-package-service.ts`

### Implementation for User Story 3

- [ ] T047 [US3] Implement shared package validation service in `backend/src/NaderGorge.Application/Services/SharedTeacherPackageValidationService.cs`
- [ ] T048 [US3] Add admin shared package create/update/publish commands in `backend/src/NaderGorge.Application/Features/Admin/SharedPackages/Commands/`
- [ ] T049 [US3] Add admin shared package list/detail queries in `backend/src/NaderGorge.Application/Features/Admin/SharedPackages/Queries/`
- [ ] T050 [US3] Add student shared package list/detail queries in `backend/src/NaderGorge.Application/Features/Student/SharedPackages/Queries/`
- [ ] T051 [US3] Add shared package purchase command with access grants and teacher ledger allocations in `backend/src/NaderGorge.Application/Features/Student/SharedPackages/Commands/PurchaseSharedPackageCommand.cs`
- [X] T052 [US3] Add admin shared package API controller in `backend/src/NaderGorge.Api/Controllers/AdminSharedPackagesController.cs`
- [X] T053 [US3] Add student shared package API controller in `backend/src/NaderGorge.Api/Controllers/StudentSharedPackagesController.cs`
- [X] T054 [US3] Create shared package frontend service in `frontend/src/services/shared-package-service.ts`
- [X] T055 [US3] Create admin shared package list page in `frontend/src/app/admin/shared-packages/page.tsx`
- [X] T056 [US3] Create admin shared package editor client in `frontend/src/app/admin/shared-packages/SharedPackageEditorClient.tsx`
- [X] T057 [US3] Create student shared package listing/detail purchase UI in `frontend/src/app/student/shared-packages/`
- [X] T058 [US3] Integrate shared package purchase events into teacher finance reads in `backend/src/NaderGorge.Application/Features/Teacher/Finance/Queries/GetTeacherTransactionsQuery.cs`

**Checkpoint**: Shared package purchase is independently testable and teacher shares reconcile to paid amount.

---

## Phase 6: User Story 4 - Student Browses Teacher Public Profile and Community (Priority: P2)

**Goal**: Students can browse public teacher profiles with subjects, packages, lessons/previews, intro video, ratings, and teacher-scoped moderated community.

**Independent Test**: Open a teacher profile before and after purchase and verify public/gated content and moderated teacher community visibility.

### Tests for User Story 4

- [ ] T059 [P] [US4] Add public teacher profile query tests in `backend/tests/NaderGorge.Application.Tests/PublicTeacherProfileTests.cs`
- [ ] T060 [P] [US4] Add teacher-scoped community moderation tests in `backend/tests/NaderGorge.Application.Tests/CommunityCommentModerationTests.cs`
- [X] T061 [P] [US4] Add frontend compile coverage for teacher profile DTO changes in `frontend/src/services/teacher-service.ts`

### Implementation for User Story 4

- [ ] T062 [US4] Add public teacher profile query handler in `backend/src/NaderGorge.Application/Features/Public/Teachers/GetPublicTeacherProfileQuery.cs`
- [ ] T063 [US4] Add public teacher listing query handler in `backend/src/NaderGorge.Application/Features/Public/Teachers/GetPublicTeachersQuery.cs`
- [ ] T064 [US4] Add teacher-scoped community post query/command handlers in `backend/src/NaderGorge.Application/Features/Community/`
- [X] T065 [US4] Add public teacher API controller in `backend/src/NaderGorge.Api/Controllers/PublicTeachersController.cs`
- [X] T066 [US4] Update teacher service DTOs and profile methods in `frontend/src/services/teacher-service.ts`
- [X] T067 [US4] Update student teacher list/profile UI in `frontend/src/app/student/teachers/StudentTeachersPageClient.tsx`
- [X] T068 [US4] Add teacher profile detail page in `frontend/src/app/student/teachers/[teacherId]/page.tsx`
- [X] T069 [US4] Add teacher community panel component in `frontend/src/app/student/teachers/TeacherCommunityPanel.tsx`

**Checkpoint**: Teacher profile/community works independently and preserves moderation.

---

## Phase 7: User Story 5 - Financial Ownership Across Codes, Purchases, and Public Exams (Priority: P2)

**Goal**: All monetization paths create consistent teacher/platform records, failed operations do not create final earnings, and free/100% discount events remain zero-value tracking unless compensated.

**Independent Test**: Run code activation, direct lesson/package purchase, public exam purchase, failure, duplicate retry, and 100% discount scenarios and verify ledger behavior.

### Tests for User Story 5

- [ ] T070 [P] [US5] Add code activation ledger tests in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`
- [ ] T071 [P] [US5] Add direct purchase and public exam ledger tests in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`
- [ ] T072 [P] [US5] Add free/100% discount zero-value event tests in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`
- [ ] T073 [P] [US5] Add duplicate/failure no-final-earning tests in `backend/tests/NaderGorge.Application.Tests/TeacherAccountingPhase3Tests.cs`

### Implementation for User Story 5

- [X] T074 [US5] Wire `TeacherAccountingService` into code activation flow in `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs`
- [X] T075 [US5] Wire `TeacherAccountingService` into direct purchase flow in `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`
- [X] T076 [US5] Update `SalesFinancialEffect` teacher/platform impact fields from ledger decisions in `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`
- [ ] T077 [US5] Add product ownership resolver for lesson/package/public exam targets in `backend/src/NaderGorge.Application/Services/TeacherFinancialProductResolver.cs`
- [X] T078 [US5] Add idempotency keys for purchase/code ledger writes in `backend/src/NaderGorge.Application/Services/TeacherAccountingService.cs`
- [ ] T079 [US5] Add manual compensation command for zero-value operations in `backend/src/NaderGorge.Application/Features/Admin/Finance/Commands/AddTeacherCompensationCommand.cs`
- [X] T080 [US5] Expose manual compensation endpoint in `backend/src/NaderGorge.Api/Controllers/AdminFinanceController.cs`
- [X] T081 [US5] Add admin UI action for explicit teacher compensation in `frontend/src/app/admin/finance/AdminFinancePageClient.tsx`

**Checkpoint**: All financial sources use consistent ledger semantics.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Finish integration quality, performance, UX states, and documentation.

- [ ] T082 [P] Add database index regression assertions for teacher calendar and admin review queries in `backend/tests/NaderGorge.Application.Tests/FinancialDataIntegrityTests.cs`
- [ ] T083 [P] Add Arabic UX copy for payout statuses and review states in `frontend/src/services/finance-service.ts`
- [X] T084 [P] Add route/nav entries for shared packages and teacher finance/profile surfaces in `frontend/src/config/navigation.ts`
- [X] T085 Update roadmap Phase 3 checkboxes with evidence in `docs/platform-change-roadmap.md`
- [X] T086 Update AGENTS.md Spec Kit marker with `158-teacher-accounting-phase3` in `AGENTS.md`
- [X] T087 Update achievements with implementation evidence in `achievements.md`
- [X] T088 Run focused code cleanup across Phase 3 backend application services in `backend/src/NaderGorge.Application/Services/`
- [X] T089 Run focused UI polish for finance/profile pages in `frontend/src/app/teacher/finance/TeacherFinancePageClient.tsx`

---

## Phase 9: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment before starting the next phase.

- [X] T090 Run focused backend tests and record results in `achievements.md`
- [X] T091 Run `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj` and record results in `achievements.md`
- [X] T092 Run `cd frontend && npm run lint && npm run build` and record results in `achievements.md`
- [X] T093 Run `docker compose config -q` and record results in `achievements.md`
- [X] T094 Run migration/startup verification or document blocker in `achievements.md`
- [ ] T095 Complete manual QA checklist from `specs/158-teacher-accounting-phase3/quickstart.md`
- [X] T096 Write end-of-phase summary with implemented scope, tests, Docker status, manual QA status, risks, and Phase 4 go/no-go in `achievements.md`
- [X] T097 Run `clean-code-guard` review on Phase 3 production code and record findings/fixes in `achievements.md`
- [X] T098 Run `test-guard` review on Phase 3 tests and record findings/fixes in `achievements.md`
- [X] T099 Run feature tests command `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TeacherAccountingPhase3Tests|FullyQualifiedName~SharedPackageAccountingTests|FullyQualifiedName~CommissionTests|FullyQualifiedName~FinancialDataIntegrityTests|FullyQualifiedName~PublicTeacherProfileTests"` and record expected pass/fail result in `achievements.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup and blocks all user stories.
- **US1 (Phase 3)**: Depends on Foundational; MVP for teacher-visible finance.
- **US2 (Phase 4)**: Depends on Foundational and integrates with US1 ledger/account reads.
- **US3 (Phase 5)**: Depends on Foundational and `TeacherAccountingService`.
- **US4 (Phase 6)**: Depends on Foundational profile/community schema; can run after schema even while finance stories continue.
- **US5 (Phase 7)**: Depends on `TeacherAccountingService` and validates all source integrations.
- **Polish and Verification**: Depend on selected user stories being complete.

### User Story Dependencies

- **US1**: First MVP, unlocks teacher finance read model.
- **US2**: Uses ledger/account model from US1 but has independent admin review/payout tests.
- **US3**: Uses ledger service and shared package schema; independently validates shared package purchase.
- **US4**: Can be implemented in parallel after foundational schema.
- **US5**: Best after US1/US2 service stabilizes because it wires every monetization source.

### Parallel Opportunities

- T002-T004 can run in parallel during setup.
- T016-T017 can run in parallel after foundational entity shape is drafted.
- Test tasks within each user story marked `[P]` can run before implementation in that story.
- US3 admin/student UI tasks can run in parallel with backend commands after contracts are stable.
- US4 frontend profile work can run in parallel with backend profile queries after DTOs are agreed.

## Parallel Example: User Story 3

```bash
# Backend tests can be drafted together:
Task: "T044 [P] [US3] Add shared package validation and allocation tests in backend/tests/NaderGorge.Application.Tests/SharedPackageAccountingTests.cs"
Task: "T045 [P] [US3] Add shared package purchase/access grant tests in backend/tests/NaderGorge.Application.Tests/SharedPackageAccountingTests.cs"

# Frontend service and page work can begin after API contracts stabilize:
Task: "T054 [US3] Create shared package frontend service in frontend/src/services/shared-package-service.ts"
Task: "T055 [US3] Create admin shared package list page in frontend/src/app/admin/shared-packages/page.tsx"
```

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete US1 so teacher finance has a canonical ledger-backed read surface.
3. Validate US1 with backend tests and teacher finance UI build.
4. Continue with US2 payout review before enabling shared-package payouts.

### Incremental Delivery

1. US1: teacher daily finance.
2. US2: admin review and payout lifecycle.
3. US3: shared multi-teacher package.
4. US5: source integration hardening for all purchases/codes/public exams.
5. US4: teacher profile/community can ship when profile UX is ready, provided moderation tests pass.

### Final Gate

No roadmap checkbox should be marked complete until the associated automated tests pass and quickstart manual QA has a pass/blocker note.
