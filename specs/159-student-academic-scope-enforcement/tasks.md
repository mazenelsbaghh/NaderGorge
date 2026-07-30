# Tasks: Student Academic Scope Enforcement

**Input**: Design documents from `specs/159-student-academic-scope-enforcement/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/academic-scope-api.md`, `quickstart.md`
**Target prompt**: create the tasks file so that a cheaper llm model can implement without problems

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Format: `[ID] [P?] [Story] Description`

- `[P]`: Parallelizable after dependencies are complete.
- `[US1]`: Filter every student surface.
- `[US2]`: Block invalid purchase, code, coupon, and gift flows.
- `[US3]`: Require academic targeting at admin creation time.
- `[US4]`: Keep clear student empty/denial states.

## Phase 1: Setup and Discovery

- [x] T001 Record current branch, dirty files, and active feature directory in `achievements.md`; expected result: feature remains `159-student-academic-scope-enforcement` and no unrelated files are reverted.
- [x] T002 [P] Read `backend/src/NaderGorge.Domain/Enums/EducationStage.cs`, `backend/src/NaderGorge.Domain/Enums/GradeLevel.cs`, and `backend/src/NaderGorge.Application/Services/AcademicValidationService.cs`; document valid stage/grade pairs in `achievements.md`.
- [x] T003 [P] Read `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs`, `backend/src/NaderGorge.Domain/Entities/SalesEntities.cs`, `backend/src/NaderGorge.Domain/Entities/CodeEntities.cs`, `backend/src/NaderGorge.Domain/Entities/GiftEntities.cs`, and `backend/src/NaderGorge.Domain/Entities/SharedTeacherPackage.cs`; document every legacy scope field in `achievements.md`.
- [x] T004 [P] Read `backend/src/NaderGorge.Application/Services/AccessCheckService.cs`, `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`, `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs`, and `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs`; document grant side-effect order in `achievements.md`.
- [x] T005 [P] Read frontend scope label and service files `frontend/src/lib/academic-labels.ts`, `frontend/src/services/content-service.ts`, `frontend/src/services/code-service.ts`, `frontend/src/services/admin-gifts-service.ts`, `frontend/src/services/public-exams-service.ts`, and `frontend/src/services/shared-package-service.ts`; document required DTO changes in `achievements.md`.

## Phase 2: Foundational Backend Scope Model

- [x] T006 Create `AcademicScopeLevel` and `StudentFacingScopeOwnerType` enums in `backend/src/NaderGorge.Domain/Enums/AcademicScopeEnums.cs` with values from `specs/159-student-academic-scope-enforcement/data-model.md`.
- [x] T007 Create `AcademicSubjectEligibility` and `StudentFacingAcademicScope` entities in `backend/src/NaderGorge.Domain/Entities/AcademicScopeEntities.cs` using `BaseEntity`, nullable fields, and navigation properties described in `data-model.md`.
- [x] T008 Add `DbSet<AcademicSubjectEligibility>` and `DbSet<StudentFacingAcademicScope>` to `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`.
- [x] T009 Configure DbSets, required fields, enum conversions if used locally, indexes, uniqueness, and delete behavior for the two new entities in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T010 Create EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/` for `AcademicSubjectEligibility` and `StudentFacingAcademicScope`; expected result: migration creates indexed tables and does not mark unscoped records platform-wide.
- [x] T011 Implement legacy grade alias normalization helper in `backend/src/NaderGorge.Application/Services/AcademicScopeService.cs` covering at least `FirstSecondary`, `1st Secondary`, `SecondSecondary`, and canonical enum names.
- [x] T012 Define `IAcademicScopeService` in `backend/src/NaderGorge.Domain/Interfaces/IAcademicScopeService.cs` with methods from `plan.md`: student profile lookup, allowed subjects, owner eligibility, target scope validation, student target validation, and effective scope resolution.
- [x] T013 Implement `AcademicScopeService` in `backend/src/NaderGorge.Application/Services/AcademicScopeService.cs`; expected result: missing profile, missing scope, inactive subject mapping, and invalid stage/grade all return ineligible.
- [x] T014 Register `IAcademicScopeService` in `backend/src/NaderGorge.API/Program.cs` as scoped dependency.
- [x] T015 [P] Extend `backend/tests/NaderGorge.Application.Tests/TestAppDbContextFactory.cs` to expose new DbSets for in-memory tests.
- [x] T016 [P] Create `backend/tests/NaderGorge.Application.Tests/AcademicScopeServiceTests.cs` covering platform-wide, stage-wide, grade-all-subjects, exact subject, multiple scopes any-match, missing profile fail-closed, invalid subject rejection, and legacy alias normalization.
- [x] T017 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AcademicScopeServiceTests"`; expected result: all foundational service tests pass.

## Phase 3: User Story 1 - Filter Every Student Surface (Priority: P1)

**Goal**: Students see only matching or applicable general-scope records across lists and details.
**Independent Test**: Seed two students, two grades, three subjects, matching/non-matching/general records, then verify every student list returns only eligible items.

### Tests for User Story 1

- [x] T018 [P] [US1] Create `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAccessTests.cs` test fixtures for two students, three subjects, `AcademicSubjectEligibility`, and scoped package hierarchy.
- [x] T019 [P] [US1] In `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAccessTests.cs`, write tests for `GetPackagesQuery` filtering matching, platform-wide, stage-wide, grade-all-subjects, and non-matching packages.
- [x] T020 [P] [US1] In `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAccessTests.cs`, write tests for inherited scope on term, section, lesson, video, and exam; expected result: child without scope inherits, child with non-matching explicit scope is denied.
- [x] T021 [P] [US1] In `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAccessTests.cs`, write tests for `GetCommunityPostsQuery`, public teacher query path, public exams, shared packages, and notifications returning only eligible records.

### Implementation for User Story 1

- [x] T022 [US1] Update `backend/src/NaderGorge.Application/Services/AccessCheckService.cs` to require `IAcademicScopeService.IsOwnerEligibleForStudentAsync` for package, lesson, video, and exam access after role bypass and active grant checks.
- [x] T023 [US1] Update `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackagesQuery.cs` to filter student package results by academic eligibility before enrollment projection.
- [x] T024 [US1] Update `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackageByIdQuery.cs` so direct package detail requests by students deny non-matching targets with `ACADEMIC_SCOPE_DENIED`.
- [x] T025 [US1] Update content hierarchy queries `backend/src/NaderGorge.Application/Features/Content/Queries/GetTermsQuery.cs`, `GetSectionsQuery.cs`, `GetLessonsQuery.cs`, and `GetLessonDetailQuery.cs` to apply inherited/effective scope before returning child records.
- [x] T026 [US1] Update lesson resource/comment queries in `backend/src/NaderGorge.Application/Features/Content/Queries/` and comment command `backend/src/NaderGorge.Application/Features/Content/Commands/CreateLessonCommentCommand.cs` to deny non-matching lessons using the same access service.
- [x] T027 [US1] Update `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs`, `GetQuickAccessQuery.cs`, `GetProgressQuery.cs`, `GetMistakesQuery.cs`, and `GetStudentNotificationsQuery.cs` to exclude grants or notifications whose current academic target is not eligible.
- [x] T028 [US1] Update `backend/src/NaderGorge.Application/Features/Community/Queries/GetCommunityPostsQuery.cs`, `CreateCommunityPostCommentCommand.cs`, `ToggleCommunityPostLikeCommand.cs`, and `ToggleCommunityPostVoteCommand.cs` to filter/deny community posts outside the student's scope.
- [x] T029 [US1] Update public teacher query implementation behind `backend/src/NaderGorge.API/Controllers/PublicTeachersController.cs` so a teacher appears to a student only when at least one teacher subject is allowed or teacher scope is general for that student.
- [x] T030 [US1] Update public exam query handlers behind `backend/src/NaderGorge.API/Controllers/PublicExamsController.cs` and `backend/src/NaderGorge.Application/Features/Admin/Sales/SalesQueries.cs` so student public-exam lists/details apply academic scope before payment status.
- [x] T031 [US1] Update shared package query handlers behind `backend/src/NaderGorge.API/Controllers/StudentSharedPackagesController.cs` so student shared packages and included items apply package and item eligibility.
- [x] T032 [US1] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~StudentAcademicScopeAccessTests"`; expected result: all US1 visibility and direct-access tests pass.

## Phase 4: User Story 2 - Block Invalid Purchase, Code, Coupon, and Gift Flows (Priority: P1)

**Goal**: Students cannot buy, redeem, receive, or use non-matching targets; no grant, discount, balance deduction, or financial side effect is created.
**Independent Test**: Attempt purchase, code activation, coupon application, printable code redemption, and gift delivery for non-matching targets and verify denial before side effects.

### Tests for User Story 2

- [x] T033 [P] [US2] Create `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopePurchaseTests.cs` with tests for `PurchaseContentCommand` denying non-matching package, lesson, public exam, and inherited child target before balance deduction.
- [x] T034 [P] [US2] In `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopePurchaseTests.cs`, write tests proving coupon and printable-code discounts are not committed when academic eligibility fails.
- [x] T035 [P] [US2] Create `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeCodeTests.cs` with tests for `ValidateCodeQuery` and `ActivateCodeCommand` denying non-matching targets before `AccessCode.IsConsumed`.
- [x] T036 [P] [US2] Create `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeGiftTests.cs` with tests for `IssueGiftCommand` returning recipient `ACADEMIC_SCOPE_DENIED` and creating no `StudentAccessGrant`.
- [x] T037 [P] [US2] In `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAccessTests.cs`, write regression test proving an old active grant stops authorizing after the student's grade or subject eligibility mapping changes.

### Implementation for User Story 2

- [x] T038 [US2] Update `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs` to call `IAcademicScopeService.ValidateStudentCanUseTargetAsync` before discount, promotional balance, balance deduction, grant creation, outbox, and teacher accounting.
- [x] T039 [US2] Update `backend/src/NaderGorge.Application/Features/Student/Queries/GetPurchaseFundingPreviewQuery.cs` to return `ACADEMIC_SCOPE_DENIED` for non-matching targets before showing discount/funding previews.
- [x] T040 [US2] Update `backend/src/NaderGorge.Application/Services/DiscountEngine.cs` so coupon and printable-code application re-checks actual student academic eligibility before recording usage or discount lines.
- [x] T041 [US2] Update `backend/src/NaderGorge.Application/Features/Codes/Queries/ValidateCodeQuery.cs` to become student-aware through authenticated user context or a new request field wired from `backend/src/NaderGorge.API/Controllers/CodesController.cs`.
- [x] T042 [US2] Update `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs` to validate academic eligibility inside the serializable transaction before marking the code consumed or creating any grant.
- [x] T043 [US2] Update code group generation command in `backend/src/NaderGorge.Application/Features/Admin/Commands/BulkGenerateCodesCommand.cs` or the actual bulk command file to reject unscoped targets at creation with `ACADEMIC_SCOPE_TARGET_UNSCOPED`.
- [x] T044 [US2] Update `backend/src/NaderGorge.Application/Features/Admin/Sales/SalesCommands.cs` to validate `SalesCouponRequest`, `PrintableBatchRequest`, and `SalesRuleRequest` targets have valid scope at create/update time.
- [x] T045 [US2] Update `backend/src/NaderGorge.Application/Features/Admin/Gifts/Commands/IssueGiftCommand.cs` so target scope is validated at issuance and every concrete recipient is checked before grant or promotional balance allocation.
- [x] T046 [US2] Update `backend/src/NaderGorge.Application/Services/GiftUsageService.cs` to re-check current academic eligibility when consuming gift-backed grants or balances.
- [x] T047 [US2] Update audit writes in purchase/code/gift denial paths to include action names `AcademicScopeDeniedPurchase`, `AcademicScopeDeniedCodeActivation`, and `AcademicScopeDeniedGiftRecipient` in `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`, `ActivateCodeCommand.cs`, and `IssueGiftCommand.cs`.
- [x] T048 [US2] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~StudentAcademicScopePurchaseTests|FullyQualifiedName~StudentAcademicScopeCodeTests|FullyQualifiedName~StudentAcademicScopeGiftTests"`; expected result: all side-effect prevention tests pass.

## Phase 5: User Story 3 - Require Academic Targeting at Admin Creation Time (Priority: P2)

**Goal**: Admins cannot save or publish new student-facing records without valid exact or general scope.
**Independent Test**: Use admin command handlers for content, public exams, sales, gifts, code groups, shared packages, community, and notifications; verify missing or invalid scope fails and valid scopes save.

### Tests for User Story 3

- [x] T049 [P] [US3] Create `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAdminValidationTests.cs` with tests for missing scope, invalid stage/grade, invalid subject, and each valid scope level.
- [x] T050 [P] [US3] In `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAdminValidationTests.cs`, write tests for public exam product save/create and sales coupon/printable batch validation.
- [x] T051 [P] [US3] In `backend/tests/NaderGorge.Application.Tests/StudentAcademicScopeAdminValidationTests.cs`, write tests for code group, gift issuance, shared package, community post, and notification scope validation.

### Implementation for User Story 3

- [x] T052 [US3] Add reusable request/response records `AcademicScopeDto`, `AcademicScopeValidationResult`, and scope summary DTOs in `backend/src/NaderGorge.Application/Common/AcademicScopeDtos.cs`.
- [x] T053 [US3] Add validator helpers in `backend/src/NaderGorge.Application/Services/AcademicScopeService.cs` for saving scope arrays and syncing rows for an owner.
- [x] T054 [US3] Update content create/update commands in `backend/src/NaderGorge.Application/Features/Admin/Content/` to accept `IReadOnlyList<AcademicScopeDto> AcademicScopes` and reject empty scope lists.
- [x] T055 [US3] Update `backend/src/NaderGorge.Application/Features/Admin/Sales/SalesContracts.cs` and `SalesCommands.cs` so `PublicExamProductRequest`, `CreatePublicExamRequest`, `SalesCouponRequest`, and `PrintableBatchRequest` expose and persist `academicScopes`.
- [x] T056 [US3] Update code group request/command DTOs in `backend/src/NaderGorge.Application/Features/Admin/Commands/` and `frontend/src/services/code-service.ts` to include `academicScopes` for broad or deferred-student targets.
- [x] T057 [US3] Update `backend/src/NaderGorge.Application/Features/Admin/Gifts/Models/GiftModels.cs` and `IssueGiftCommand.cs` so gift target lookup and issuance responses include scope summary and academic denial outcomes.
- [x] T058 [US3] Update shared package commands and DTOs behind `backend/src/NaderGorge.API/Controllers/AdminSharedPackagesController.cs` to persist `academicScopes` and bridge existing `EducationStage`/`GradeLevel` fields.
- [x] T059 [US3] Update community/admin moderation commands behind `backend/src/NaderGorge.API/Controllers/AdminCommunityController.cs` so platform/community posts that appear to students require scopes unless they are personal/private drafts.
- [x] T060 [US3] Update notification or offer creation handlers behind `backend/src/NaderGorge.API/Controllers/AssistantController.cs` and `backend/src/NaderGorge.Domain/Entities/Notifications/NotificationEvent.cs` to prevent broad student-facing notifications without scopes.
- [x] T061 [US3] Update frontend DTOs and admin forms in `frontend/src/services/admin-service.ts`, `frontend/src/services/admin-sales-service.ts`, `frontend/src/services/admin-gifts-service.ts`, `frontend/src/services/public-exams-service.ts`, `frontend/src/services/shared-package-service.ts`, and `frontend/src/lib/academic-labels.ts` to send/display `academicScopes`.
- [x] T062 [US3] Add or update admin scope selector components under `frontend/src/components/admin/` with controls for `PlatformWide`, `StageWide`, `GradeAllSubjects`, and `Exact` scope; expected result: no text overflows and Arabic labels use existing admin design tokens.
- [x] T063 [US3] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~StudentAcademicScopeAdminValidationTests"` and `cd frontend && npm run lint`; expected result: backend validation tests pass and frontend lint passes.

## Phase 6: User Story 4 - Clear Empty and Denial States (Priority: P3)

**Goal**: Strict filtering produces clear Arabic empty states and denial messages instead of unrelated content or broken pages.
**Independent Test**: Use a student with no matching content; every student page loads and shows an empty state, while direct forbidden targets show a denial message.

### Tests for User Story 4

- [x] T064 [P] [US4] Create or update Playwright smoke specs in `frontend/tests/student-academic-scope.spec.ts` for empty packages, teachers, community, public exams, shared packages, and notifications pages.
- [x] T065 [P] [US4] Add frontend component/unit coverage if available for empty states in `frontend/src/app/student/packages/PackagesPageClient.tsx`, `StudentTeachersPageClient.tsx`, `StudentCommunityPageClient.tsx`, `StudentPublicExamsPageClient.tsx`, and `StudentSharedPackagesPageClient.tsx`.

### Implementation for User Story 4

- [x] T066 [US4] Update `frontend/src/app/student/packages/PackagesPageClient.tsx` and `frontend/src/components/student-dashboard/PackageGrid.tsx` to show Arabic empty state when backend returns zero eligible packages.
- [x] T067 [US4] Update `frontend/src/app/student/teachers/StudentTeachersPageClient.tsx` to show Arabic empty state when no eligible teachers exist.
- [x] T068 [US4] Update `frontend/src/app/student/community/StudentCommunityPageClient.tsx` and `frontend/src/components/student/CommunityFeed.tsx` to show Arabic empty state when no eligible posts exist.
- [x] T069 [US4] Update `frontend/src/app/student/public-exams/StudentPublicExamsPageClient.tsx`, `frontend/src/app/student/shared-packages/StudentSharedPackagesPageClient.tsx`, and `frontend/src/app/student/notifications/StudentNotificationsPageClient.tsx` to show clear empty states.
- [x] T070 [US4] Update API error handling in `frontend/src/services/api-client.ts` or page-level consumers to render `ACADEMIC_SCOPE_DENIED` as Arabic unavailable messages without exposing target details.
- [x] T071 [US4] Run `cd frontend && npm run lint && npm run build`; expected result: strict TypeScript, lint, and production build pass.

## Phase 7: Cross-Cutting Backfill, Migration, and Performance

- [x] T072 Create backfill SQL inside the EF migration in `backend/src/NaderGorge.Infrastructure/Migrations/` for `Package.TargetGrade`, `PublicExamProduct.IsPlatformWide`, `PublicExamProduct.GradeLevel`, `PublicExamProduct.SubjectId`, `SharedTeacherPackage.EducationStage`, `SharedTeacherPackage.GradeLevel`, and teacher subject mappings.
- [x] T073 Create `backend/tests/NaderGorge.Application.Tests/AcademicScopeMigrationBackfillTests.cs` or migration-adjacent test coverage proving unscoped legacy records remain hidden and known aliases map correctly.
- [x] T074 Review all student-facing queries with `rg "IRequest<ApiResponse" backend/src/NaderGorge.Application/Features` and record any intentionally out-of-scope non-student endpoints in `achievements.md`.
- [x] T075 Add indexes or query projections in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` so list filtering avoids N+1 owner eligibility checks for packages, community posts, public exams, and shared packages.
- [x] T076 Run `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AcademicScope|FullyQualifiedName~AccessCheck|FullyQualifiedName~Purchase|FullyQualifiedName~Gift|FullyQualifiedName~Code|FullyQualifiedName~Sales"`; expected result: all feature-related backend tests pass.

## Phase 8: Required Guard and Verification Tail

- [x] T077 Perform deep critique fixes comparing changed code against `specs/159-student-academic-scope-enforcement/spec.md`, `plan.md`, and this `tasks.md`; record each finding and fix in `achievements.md`.
- [x] T078 Run `clean-code-guard` on changed production files after critique fixes; expected result: every finding resolved or explicitly recorded and fixed before continuing.
- [x] T079 Run `test-guard` on changed test files after `clean-code-guard`; expected result: every test-quality finding resolved before feature tests.
- [x] T080 Run feature tests from `specs/159-student-academic-scope-enforcement/quickstart.md`, including `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AcademicScope"` and relevant student E2E/smoke tests; expected result: feature tests pass.
- [x] T081 Run final backend and frontend verification commands: `dotnet test backend/NaderGorge.sln`, `cd frontend && npm run lint && npm run build`, and `make verify`; expected result: no introduced compile, lint, or test failures.
- [x] T082 Run Docker verification: `docker compose config -q`, `make up`, `make migrate`, `curl -f http://localhost:5245/api/health`, `curl -f http://localhost:8738`, `curl -f http://localhost:3001/ui`, and `make ps`; expected result: stack starts, migrations apply, and health checks pass or blocked external secrets are documented.
- [x] T083 Record feature test evidence, Docker results, manual QA checklist, clean-code-guard result, test-guard result, and final readiness in `achievements.md`.

## Dependencies & Execution Order

- Phase 1 must finish before Phase 2.
- Phase 2 blocks all user stories.
- US1 and US2 are both P1; implement US1 first because US2 depends on target eligibility and access checks.
- US3 depends on foundational scope persistence but can proceed after T006-T017.
- US4 depends on backend denial/empty responses from US1 and US2.
- Phase 7 depends on all story implementation.
- Phase 8 must run in this exact order: deep critique fixes, `clean-code-guard`, `test-guard`, feature tests, final build verification.

## Parallel Opportunities

- T002-T005 can run in parallel.
- T015-T016 can run after T006-T014 are complete.
- T018-T021 can run in parallel.
- T033-T037 can run in parallel.
- T049-T051 can run in parallel.
- T064-T065 can run in parallel.
- Different frontend empty-state tasks T066-T070 can run in parallel after API contracts are stable.

## MVP Scope

MVP is Phase 1, Phase 2, and User Story 1. It proves backend list/detail filtering and fail-closed behavior before financial and admin creation workflows are layered on.

## Observable Acceptance Outcomes

- Matching, platform-wide, stage-wide, and grade-all-subjects records appear to eligible students.
- Non-matching records are absent from list APIs and denied on direct detail requests.
- Non-matching purchase/code/coupon/gift attempts create no grants, discounts, balance transactions, or financial effects.
- Existing grants remain historical but stop authorizing when current academic eligibility no longer matches.
- Admin saves fail when student-facing records have no valid exact or general scope.
