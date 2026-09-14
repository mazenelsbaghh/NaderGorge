# Tasks: Content Identity and Types

**Input**: `specs/151-content-identity-and-types/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/admin-content-identity-api.md`, and `quickstart.md`
**Target prompt**: create the tasks file so that a cheaper llm model can implement without problems
**Tests**: Mandatory because this feature changes persisted data, permissions, API contracts, and admin UI.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification - `speckit-specify` created and validated `specs/151-content-identity-and-types/spec.md` and `checklists/requirements.md`.
- [x] Phase 2: Arabic Clarification - `speckit-clarify` recorded global cross-kind internal-code uniqueness in `specs/151-content-identity-and-types/spec.md`.
- [x] Phase 3: Technical Planning - standalone `speckit-plan` produced `plan.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md` and updated `AGENTS.md`.
- [x] Phase 4: Detailed Task Breakdown - `speckit-tasks` used the fallback `.specify/templates/tasks-template.md` because `.specify/scripts/bash/setup-tasks.sh` is absent in this repository.

## Phase 1: Baseline And Test Scaffolding

**Purpose**: Establish feature-owned test files and protect unrelated dirty worktree changes before production edits.

- [x] T001 Record the pre-feature status and existing diffs for every planned production file with `git status --short` and `git diff -- backend/src/NaderGorge.Domain/Entities/ContentEntities.cs backend/src/NaderGorge.Domain/Entities/ExamEntities.cs backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonCockpitQuery.cs backend/src/NaderGorge.Application/Features/Admin/Queries/GetExamDashboardQuery.cs backend/src/NaderGorge.API/Controllers/AdminController.cs frontend/src/services/admin-service.ts frontend/src/components/admin/AddVideoForm.tsx frontend/src/components/admin/LessonVideoList.tsx frontend/src/app/admin/content/AdminContentPageClient.tsx frontend/src/app/admin/content/lessons/[id]/LessonProfilePageClient.tsx frontend/src/app/admin/content/exams/[id]/ExamProfilePageClient.tsx`; expected result: user-owned changes are identified and preserved.
- [x] T002 Create `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` with reusable `TestAppDbContextFactory.Create()` setup and failing test sections for internal codes, video type commands, video validation, and DTO projection; expected result before implementation: new tests compile only after planned contracts exist.
- [x] T003 Extend `frontend/tests/e2e/admin-content.spec.ts` with skipped-or-failing selectors for the video-type catalog, required type selection, and read-only `LES-`/`VID-` display; expected result before implementation: assertions identify missing UI without changing existing content setup.

---

## Phase 2: Foundational Data And Persistence

**Purpose**: Add shared entities, relationships, central code enforcement, and the transactional migration that block all user stories.

**Critical**: Do not edit API or frontend contracts before T004-T009 compile and the focused persistence tests pass.

- [x] T004 Add `InternalCode` to `Lesson` and `LessonVideo`, add `VideoTypeId`/`VideoType`, and define `VideoType` with `Name`, `NormalizedName`, `SortOrder`, `IsActive`, and `Videos` in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs`; keep legacy `VideoTag` unchanged and expected property max lengths documented in XML comments only where behavior is non-obvious.
- [x] T005 [P] Add required `InternalCode` to `Exam` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`; do not alter exam attempts, questions, or target relationships.
- [x] T006 Add `DbSet<VideoType> VideoTypes` to `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` so Application commands can query the catalog without Infrastructure references.
- [x] T007 Configure `video_types`, normalized-name unique index, required `LessonVideo.VideoTypeId` FK with `DeleteBehavior.Restrict`, max lengths, three unique `InternalCode` indexes, and code-field immutability metadata in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`; expected result: model creation rejects duplicate type normalization and code writes after insert.
- [x] T008 Implement one private pre-save routine called by `AppDbContext.SaveChangesAsync` in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` that assigns missing `LES-{Id:N}`, `VID-{Id:N}`, and `EXM-{Id:N}` values for Added entries and throws `InvalidOperationException` if a persisted code is modified; expected result: every production, test, Bunny, and E2E insert path receives a code.
- [x] T009 Create `backend/src/NaderGorge.Infrastructure/Migrations/<timestamp>_AddContentIdentityAndVideoTypes.cs`, its designer, and update `AppDbContextModelSnapshot.cs` by running `dotnet ef migrations add AddContentIdentityAndVideoTypes --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API`; edit the generated migration to seed four active Arabic defaults plus inactive `غير مصنف`, backfill prefixed codes and legacy `VideoTag` mappings, then make fields required and add indexes/FK; expected result: `dotnet ef migrations script` contains no destructive changes to existing IDs, access, or assessment tables.
- [x] T010 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~ContentIdentityAndVideoTypesTests"`; expected result: persistence tests for code assignment, global prefix separation, uniqueness, immutability, and required video type pass before story API work.

**Checkpoint**: Schema and centralized persistence rules are ready; all three stories can build on stable fields.

---

## Phase 3: User Story 1 - Stable Internal Content Identity (Priority: P1)

**Goal**: Every lesson, video, and exam exposes a stable globally unique read-only code in the existing admin details.

**Independent Test**: Create all three kinds, edit mutable fields, and confirm `LES-`, `VID-`, and `EXM-` values remain unchanged and visible.

### Tests For User Story 1

- [x] T011 [P] [US1] Add xUnit tests in `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` that save lesson/video/exam rows, assert exact prefix plus 32 hex GUID format, edit titles, and assert unchanged codes; include a direct modified-code save that must throw.
- [x] T012 [P] [US1] Add DTO projection tests in `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` for `GetLessonCockpitQueryHandler` and `GetExamDashboardQueryHandler`; expected result: lesson/video/type and exam codes are returned from authoritative persisted fields.

### Backend Implementation For User Story 1

- [x] T013 [US1] Extend `LessonCockpitDto`, `LessonCockpitVideoDto`, and the mapping in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonCockpitQuery.cs` with lesson code, video code, and a typed `LessonCockpitVideoTypeDto`; include `ThenInclude(v => v.VideoType)` and preserve current exams/chapters behavior.
- [x] T014 [P] [US1] Extend `ExamDashboardDto` and its mapping in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetExamDashboardQuery.cs` with `InternalCode`; do not alter score/attempt calculations.

### Frontend Implementation For User Story 1

- [x] T015 [US1] Replace `videos: any[]`, `resources: any[]`, and `homework: any[]` where touched by this feature with explicit cockpit DTOs and add `internalCode`/`videoType`/exam code fields in `frontend/src/services/admin-service.ts`; expected result: strict TypeScript catches missing code/type contract data.
- [x] T016 [P] [US1] Add a reusable icon copy control with accessible Arabic label and fixed dimensions in `frontend/src/components/admin/ContentInternalCode.tsx`, using Lucide `Copy`, `navigator.clipboard`, existing tokens, and toast feedback; expected result: long codes wrap or truncate without resizing surrounding layout.
- [x] T017 [US1] Render `ContentInternalCode` for the lesson and each video in `frontend/src/app/admin/content/lessons/[id]/LessonProfilePageClient.tsx` and `frontend/src/components/admin/LessonVideoList.tsx`; preserve existing video actions and show the type name beside operational metadata.
- [x] T018 [P] [US1] Render the exam `ContentInternalCode` near the exam title in `frontend/src/app/admin/content/exams/[id]/ExamProfilePageClient.tsx`; expected result: code is read-only and keyboard-copyable.
- [x] T019 [US1] Complete the identity assertions in `frontend/tests/e2e/admin-content.spec.ts` so the admin creates/navigates to representative content and observes `LES-` and `VID-` labels; expected result: no request or input permits editing an internal code.
- [x] T020 [US1] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~ContentIdentityAndVideoTypesTests"` and `cd frontend && npx playwright test tests/e2e/admin-content.spec.ts --project=chromium --grep "internal code"`; expected result: all US1 tests pass or Docker-dependent E2E is recorded as pending until the Phase 9 stack gate.

**Checkpoint**: Internal identity works independently even before catalog management UI is complete.

---

## Phase 4: User Story 2 - Manage Video Types (Priority: P1)

**Goal**: Built-in admins can create, rename, reorder, activate, deactivate, and safely delete unused video types.

**Independent Test**: Exercise the catalog lifecycle, verify normalized duplicate prevention, retained inactive assignments, assigned deletion conflict, audit rows, and non-admin denial.

### Tests For User Story 2

- [x] T021 [P] [US2] Add command-handler tests in `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` for list ordering/filtering, trimmed case-insensitive uniqueness, create, rename/reorder, status changes, unused deletion, assigned deletion denial, and audit payloads.
- [x] T022 [P] [US2] Add API metadata/authorization tests in `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` that reflect `AdminVideoTypesController` actions and assert reads use `content.manage` while mutation actions require `Authorize(Roles = "Admin")`.

### Backend Implementation For User Story 2

- [x] T023 [P] [US2] Create `backend/src/NaderGorge.Application/Features/Admin/VideoTypes/VideoTypeContracts.cs` with `VideoTypeDto`, normalized-name helper, request-independent validation constants, and one projection expression shared by commands/queries.
- [x] T024 [US2] Create `CreateVideoTypeCommand`, validator, and handler in `backend/src/NaderGorge.Application/Features/Admin/VideoTypes/Commands/CreateVideoTypeCommand.cs`; trim/normalize name, validate 2-80 characters and order 0-10000, catch normalized duplicates before save, add `AuditLog`, and return the DTO.
- [x] T025 [P] [US2] Create `UpdateVideoTypeCommand`, validator, and handler in `backend/src/NaderGorge.Application/Features/Admin/VideoTypes/Commands/UpdateVideoTypeCommand.cs`; persist name/order plus before/after audit and return not-found or duplicate errors without partial changes.
- [x] T026 [P] [US2] Create `SetVideoTypeStatusCommand` and handler in `backend/src/NaderGorge.Application/Features/Admin/VideoTypes/Commands/SetVideoTypeStatusCommand.cs`; allow deactivation with assignments, update timestamp, and audit old/new state.
- [x] T027 [P] [US2] Create `DeleteVideoTypeCommand` and handler in `backend/src/NaderGorge.Application/Features/Admin/VideoTypes/Commands/DeleteVideoTypeCommand.cs`; count assignments, write a blocked-attempt audit and return `VIDEO_TYPE_IN_USE` when assigned, otherwise delete and audit in one save.
- [x] T028 [P] [US2] Create `GetVideoTypesQuery` and handler in `backend/src/NaderGorge.Application/Features/Admin/VideoTypes/Queries/GetVideoTypesQuery.cs`; support `IncludeInactive`, compute assignment counts in SQL, and order by `SortOrder` then `Name`.
- [x] T029 [US2] Create `backend/src/NaderGorge.API/Controllers/AdminVideoTypesController.cs` at `/api/admin/video-types`: GET uses `[HasPermission("content.manage")]`; POST/PUT/PATCH/DELETE use `[Authorize(Roles = "Admin")]`; map success to 200/201/204 and missing/in-use outcomes to 404/409 according to `contracts/admin-content-identity-api.md`.

### Frontend Implementation For User Story 2

- [x] T030 [US2] Add exact `VideoTypeDto`, create/update payloads, and `listVideoTypes`, `createVideoType`, `updateVideoType`, `setVideoTypeStatus`, and `deleteVideoType` methods to `frontend/src/services/admin-service.ts`; all requests must use the existing `apiClient`.
- [x] T031 [P] [US2] Create `frontend/src/hooks/useVideoTypes.ts` with `includeInactive`, initial skeleton state, error text, retry callback, ordered result, and request-cancellation guard; expected result: unmounted components are not updated.
- [x] T032 [P] [US2] Create `frontend/src/app/admin/content/video-types/page.tsx` as a thin App Router entry that renders `VideoTypesPageClient`.
- [x] T033 [US2] Create `frontend/src/app/admin/content/video-types/VideoTypesPageClient.tsx` using `AdminShellChrome`: redirect non-Admin roles to `/admin/unauthorized`, provide labeled inline create fields, compact ordered rows, assigned count, status text plus icon, edit/save/cancel controls, delete confirmation, loading skeleton, actionable empty state, retryable error, disabled submission, and Arabic toast errors from API responses.
- [x] T034 [US2] Add an Admin-role-only navigation action to `/admin/content/video-types` in `frontend/src/app/admin/content/AdminContentPageClient.tsx`; preserve existing teacher/content flows and use a Lucide icon with tooltip or visible command label.
- [x] T035 [US2] Complete Playwright catalog lifecycle and direct non-admin denial coverage in `frontend/tests/e2e/admin-content.spec.ts`; expected result: duplicate names fail, inactive rows remain visible, and assigned deletion shows deactivation guidance.
- [x] T036 [US2] Run focused xUnit and E2E commands from T020 with grep `video types`; expected result: catalog behavior, authorization metadata, audit, and UI lifecycle pass or Docker E2E remains explicitly pending for Phase 9.

**Checkpoint**: The catalog is independently manageable and protected before video forms are switched to required references.

---

## Phase 5: User Story 3 - Require A Valid Type During Video Management (Priority: P2)

**Goal**: Every new video and every explicit type replacement uses an active type, including Bunny flows, while legacy inactive assignments survive unrelated edits.

**Independent Test**: Create and edit manual/Bunny videos with active types, then verify missing, unknown, inactive replacement, and type-load failure paths.

### Tests For User Story 3

- [x] T037 [P] [US3] Add handler tests in `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` for manual `CreateVideoCommand` and `UpdateVideoCommand`: active success, missing/unknown/inactive rejection, unchanged inactive assignment success, active replacement success, and unchanged internal code.
- [x] T038 [P] [US3] Add Bunny command tests in `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` for TUS and remote-fetch creation requiring an active `VideoTypeId`; expected result: Bunny assets are not created when type validation fails.

### Backend Implementation For User Story 3

- [x] T039 [US3] Add required `VideoTypeId` to `CreateVideoCommand`, validate active existence before provider extraction, assign it to `LessonVideo`, and add `VideoTypeId` to `UpdateVideoCommand` with unchanged-inactive versus replacement-active rules in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`.
- [x] T040 [US3] Add `VideoTypeId` to `UpdateVideoRequest` and pass it through create/update actions in `backend/src/NaderGorge.API/Controllers/AdminController.cs`; do not add `InternalCode` to any request DTO.
- [x] T041 [US3] Add required `VideoTypeId` to TUS and fetch request/command records, validate active existence before creating video/asset rows, and assign the FK in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs` plus matching request records in `backend/src/NaderGorge.API/Controllers/AdminController.cs`.
- [x] T042 [US3] Ensure `CreateInlineExamCommandHandler` and standard lesson creation rely on centralized code assignment without changing academic behavior in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs` and `AdminContentCommands.cs`; expected result: no duplicated code-generation logic is introduced.

### Frontend Implementation For User Story 3

- [x] T043 [US3] Create `frontend/src/components/admin/VideoTypeSelect.tsx` using `useVideoTypes`: render a labeled Dropdown, skeleton/disabled state, retry button on failure, empty-catalog guidance, active choices for creation, and current inactive choice for edit without offering it as a replacement.
- [x] T044 [US3] Add `videoTypeId` to `CreateVideoPayload`, `UpdateVideoPayload`, Bunny TUS/fetch payloads, and typed cockpit video contracts in `frontend/src/services/admin-service.ts`; expected result: strict compilation finds every caller that omits the field.
- [x] T045 [US3] Integrate `VideoTypeSelect` into `frontend/src/components/admin/AddVideoForm.tsx`, require a selected active type for manual and file-upload modes, pass it to standard/Bunny service calls, reset predictably after success, and disable submit with visible retry/empty guidance when types are unavailable.
- [x] T046 [US3] Integrate `VideoTypeSelect` into edit state in `frontend/src/components/admin/LessonVideoList.tsx`; initialize from `video.videoType.id`, preserve current inactive assignment when unchanged, require active replacement, pass `videoTypeId`, and keep the read-only code/type metadata visible outside edit mode.
- [x] T047 [US3] Complete Playwright required-selection, inactive-type, and successful create/edit assertions in `frontend/tests/e2e/admin-content.spec.ts`; expected result: the UI blocks invalid submission and saved videos show the selected type without code changes.
- [x] T048 [US3] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~ContentIdentityAndVideoTypesTests"` and `cd frontend && npx playwright test tests/e2e/admin-content.spec.ts --project=chromium --grep "video type"`; expected result: manual and Bunny validation paths pass or Docker-dependent E2E is pending only until Phase 9.

**Checkpoint**: All accepted user stories and provider creation paths are implemented.

---

## Phase 6: Migration, Regression, And Documentation Alignment

- [x] T049 Verify the generated migration and `backend/src/NaderGorge.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` match `specs/151-content-identity-and-types/data-model.md`; expected result: five seed rows, deterministic full backfill, required FK, three unique indexes, and reversible `Down` with no unrelated schema churn.
- [x] T050 [P] Update `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs` setup rows only where compilation/runtime requires explicit video-type seed/reference, preserving existing deterministic IDs and current E2E response contract.
- [x] T051 [P] Update `backend/src/NaderGorge.Application/Features/Admin/Commands/SeedTestCourseCommand.cs` only where the required relation needs a type, selecting a seeded active type rather than creating duplicate catalog rows.
- [x] T052 [P] Update `backend/src/NaderGorge.Application/Features/Internal/Commands/AiAnalysisCompletedCommand.cs` only if its detached `LessonVideo` reconstruction is classified as Added; expected result: it must not overwrite internal code or video type on an existing row.
- [x] T053 Run focused regression tests for content, teacher isolation, playback, parent academic details, and outbox with `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~ContentIdentityAndVideoTypesTests|FullyQualifiedName~TeacherIsolationTests|FullyQualifiedName~VideoWatchProgressTests|FullyQualifiedName~GetDetailsTests|FullyQualifiedName~LessonOutboxTests"`; expected result: no existing access, provider, exam, or event behavior regresses.

---

## Phase 7: Deep Critique And Fixes

- [x] F001 Replace the assigned-delete E2E assertion that could match dialog copy with the exact API conflict message, and assert the captured video code matches `VID-{32 hex}` before checking immutability in `frontend/tests/e2e/admin-content.spec.ts`.
- [x] F002 Restrict duplicate-name recovery in VideoType create/update handlers to the normalized-name unique constraint so unrelated database failures are not mislabeled as validation conflicts.
- [x] F003 Make `InternalCode` domain properties non-publicly-settable and keep the persistence-level mutation test through EF property metadata; expected result: application callers cannot assign operational codes directly and tracked mutation still fails.
- [x] F004 Show a field-level required message in `VideoTypeSelect` while no type is selected; expected result: disabled create/edit submission has visible guidance matching FR-014.

- [x] T054 Run the deep critique prompt against all feature-owned diffs and compare them line-by-line with `specs/151-content-identity-and-types/spec.md`, `plan.md`, `tasks.md`, and `contracts/admin-content-identity-api.md`; record every finding as a new unchecked item in both `specs/151-content-identity-and-types/tasks.md` and `achievements.md` before fixing it.
- [x] T055 Resolve every Phase 7 finding across the exact affected production/test files, rerun its smallest proving command, and check the dynamic finding only after the expected result is observed.

---

## Phase 8: Mandatory Quality Guards

- [x] F005 Remove the fixed login timeout and cross-test `seededLessonId` state from `frontend/tests/e2e/admin-content.spec.ts`; use behavior-based URL waiting and per-test setup instead.
- [x] F006 Separate the non-admin authorization check from the catalog/content lifecycle E2E so each Playwright test proves one user scenario.

- [x] T056 Run `clean-code-guard` in guard-pass mode over every changed production-code file under `backend/src/` and `frontend/src/`; record and resolve findings for SOLID boundaries, duplicate normalization, oversized React components, unsafe `any`, authorization gaps, migration churn, and hidden behavior changes before proceeding.
- [x] T057 Run `test-guard` after clean-code-guard over `backend/tests/NaderGorge.Application.Tests/ContentIdentityAndVideoTypesTests.cs` and `frontend/tests/e2e/admin-content.spec.ts`; remove brittle implementation assertions, shared-state leaks, unjustified waits, and duplicate low-value cases, then rerun focused tests.

---

## Phase 9: Feature Tests, Builds, Docker Gate, And Report

- [x] F007 Replace stale `admin.localhost:3000` E2E URLs with an `E2E_ADMIN_URL` base defaulting to the Compose admin surface at `http://localhost:8740`; expected result: Playwright reaches the healthy Docker admin container.
- [x] F008 Use the exact lesson-video tab selector and authenticate the teacher through the Compose teacher surface before testing direct admin-route denial; expected result: E2E no longer fails on ambiguous buttons or cross-surface login redirect.
- [x] F009 Scope the assigned-delete conflict assertion to the first matching live-status toast because the runtime can render duplicate identical toast nodes; expected result: the assertion still proves the exact API guidance without Playwright strict-mode ambiguity.
- [x] F010 Scope the inactive-current-type combobox assertion to the edited video row so it cannot match the separate add-video selector.
- [x] F011 Align the four Spec Kit preparation labels with the exact phase names required by `validate_run.py`; expected result: final workflow validation recognizes completed preparation.

- [x] T058 Run feature tests: `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~ContentIdentityAndVideoTypesTests"`; expected result: identity, catalog, permission, audit, and video validation matrix passes.
- [x] T059 Run full backend verification: `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj` and `dotnet build backend/NaderGorge.sln`; expected result: zero feature-introduced failures or warnings.
- [x] T060 Run frontend static verification from `frontend/`: `npm run lint` then `npm run build`; expected result: strict TypeScript and ESLint pass with no feature-introduced warning.
- [x] T061 Run `docker compose config -q`; expected result: compose configuration is valid before starting services.
- [x] T062 Run `make up`, `make migrate`, and `make ps`; expected result: migration applies to PostgreSQL and backend/frontend/worker/db/redis services are healthy or any environmental blocker is recorded exactly.
- [x] T063 Run health checks `curl -f http://localhost:5245/api/health`, `curl -f http://localhost:3001/ui`, and `curl -f http://localhost:8738`; expected result: all reachable surfaces return success.
- [x] T064 Run Docker-backed E2E feature tests from `frontend/` with `npx playwright test tests/e2e/admin-content.spec.ts --project=chromium`; expected result: catalog, identity display, required type, and existing content management journey pass.
- [x] T065 Run the SQL null/duplicate queries from `specs/151-content-identity-and-types/quickstart.md`; expected result: three null/empty counts are zero and the cross-kind duplicate query returns no rows.
- [x] T066 Keep every manual flow in `specs/151-content-identity-and-types/quickstart.md` and `spec.md` marked `pending` unless a human actually performs it; record role, URL, action, expected result, and observed status in `achievements.md` without treating automated evidence as manual completion.
- [x] T067 Update `achievements.md` with exact feature test matrix, commands, results, failures fixed, Docker evidence, migration evidence, guard ordering, manual QA pending items, residual risks, and go/no-go for spec 152.
- [x] T068 Run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/151-content-identity-and-types`; expected result: all phases, task checkboxes, artifacts, guards, tests, and AGENTS reference validate before final reporting.

---

## Dependencies And Execution Order

- Phase 1 establishes feature-owned tests and dirty-worktree boundaries.
- Phase 2 blocks every story because codes, `VideoType`, FK, central save enforcement, and migration are shared.
- US1 can complete once Phase 2 is stable.
- US2 can complete after Phase 2 and is required before US3 frontend integration can load catalog data.
- US3 depends on US2 list contracts and Phase 2 FK rules.
- Migration/regression alignment follows all stories.
- Deep critique must precede `clean-code-guard`; `clean-code-guard` must precede `test-guard`; feature tests and builds run after both guards.
- Failed Docker, migration, build, or test gates block spec 152 unless the owner explicitly accepts a documented external risk.

## Parallel Opportunities

- T005 can run while T004 is being prepared; T006-T009 become sequential once both entity changes exist.
- T011 and T012 target independent test sections; T013 and T014 target separate query files.
- T016 and T018 target separate frontend components after T015 contracts compile.
- T023, T025-T028 target separate VideoTypes files once their shared contracts are agreed; T029 waits for them.
- T031 and T032 can run independently after T030 service contracts.
- T037 and T038 cover standard versus Bunny paths in separate test sections; T039 and T041 modify separate command files.
- T050-T052 are independent compatibility audits and must be changed only when evidence requires it.

## Independent Story Outcomes

- **US1**: Staff can identify lesson/video/exam content by immutable global code and observe it in existing admin details.
- **US2**: Admins can manage one ordered type catalog with safe retirement, audit, and role protection.
- **US3**: Every new or explicitly reclassified manual/Bunny video references an active type, with clear UI failure states.

## Implementation Strategy

1. Preserve all user-owned dirty changes identified by T001.
2. Write focused failing tests before each story implementation.
3. Complete the central data foundation and migration before API/UI expansion.
4. Deliver US1 identity visibility, then US2 catalog management, then US3 required form integration.
5. Verify each checkpoint with its focused commands instead of waiting for final builds.
6. Finish with regression, deep critique, quality guards, feature tests, full builds, Docker migration/health, and an evidence report.

## Notes

- Do not remove or reinterpret legacy `VideoTag` in this spec.
- Do not add gifts, discounts, public exams, teacher revenue rules, or printable code templates.
- Do not expose `InternalCode` in create/update request DTOs.
- Do not mark manual QA complete from automated tests or screenshots.
- Do not revert unrelated worktree changes.
