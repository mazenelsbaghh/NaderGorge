# Tasks: Bunny Video Provider

**Input**: Design documents from `/specs/138-bunny-video-provider/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/endpoints.md`, `quickstart.md`

**Tests**: Mandatory for backend provider validation, Bunny upload authorization, cost reporting permissions, frontend provider/upload UI, and protected playback changes.

**Organization**: Tasks are grouped by user story to keep each increment independently testable.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare dependencies, configuration, and provider constants used by all stories.

- [x] T001 Add Bunny Stream configuration binding keys to `backend/src/NaderGorge.API/appsettings.Development.json` with empty placeholder values only.
- [x] T002 [P] Add Bunny provider constants and accepted-provider helper in `backend/src/NaderGorge.Application/Common/VideoProviders.cs`.
- [x] T003 [P] Add `tus-js-client` dependency to `frontend/package.json` and update the frontend lockfile.
- [ ] T004 [P] Add frontend Bunny provider type constants in `frontend/src/services/admin-service.ts`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared schema, interfaces, settings, and Bunny client infrastructure required by all Bunny stories.

- [x] T005 Add `BunnyVideoAsset` and `BunnyUsageSnapshot` entities to `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs`.
- [x] T006 Configure Bunny entity relationships, indexes, precision, and table names in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T007 Create EF migration `AddBunnyVideoProvider` under `backend/src/NaderGorge.Infrastructure/Migrations/`.
- [x] T008 [P] Add Bunny pricing setting keys and defaults in `backend/src/NaderGorge.Application/Common/Settings/PlatformSettingKeys.cs`.
- [x] T009 [P] Extend cached platform settings parsing for Bunny pricing rates in `backend/src/NaderGorge.Infrastructure/Cache/CachedPlatformSettingsReader.cs`.
- [x] T010 [P] Add `IBunnyStreamClient` and Bunny request/response DTOs in `backend/src/NaderGorge.Application/Interfaces/IBunnyStreamClient.cs`.
- [x] T011 Implement Bunny Stream HTTP client, TUS signature generation, create video, get video, list videos, fetch video, get storage, get library, and smart action calls in `backend/src/NaderGorge.Infrastructure/Services/BunnyStreamClient.cs`.
- [x] T012 Register `IBunnyStreamClient`, Bunny configuration validation, and Bunny provider services in `backend/src/NaderGorge.API/Program.cs`.
- [ ] T013 [P] Add provider validation tests for accepted YouTube/VK/Bunny strings in `backend/tests/NaderGorge.Application.Tests/VideoProviderValidationTests.cs`.

**Checkpoint**: Foundation ready. Schema, settings, configuration, and Bunny client are available.

---

## Phase 3: User Story 1 - Choose Bunny As A Video Source (Priority: P1)

**Goal**: Admins/teachers can choose Bunny beside YouTube and VK while legacy YouTube/VK videos still work.

**Independent Test**: Create and edit YouTube, VK, and Bunny video records; reload lesson video lists and verify provider labels and playback route behavior.

### Tests for User Story 1

- [ ] T014 [P] [US1] Add backend command tests for create/update accepting `bunny` and preserving `youtube`/`vk` in `backend/tests/NaderGorge.Application.Tests/AdminVideoProviderCommandTests.cs`.
- [ ] T015 [P] [US1] Add frontend provider selector tests for YouTube/VK/Bunny options in `frontend/src/components/admin/__tests__/AddVideoForm.test.tsx`.
- [ ] T016 [P] [US1] Add protected embed route unit tests for Bunny URL generation in `frontend/src/app/api/video/embed/__tests__/route.test.ts`.

### Implementation for User Story 1

- [x] T017 [P] [US1] Implement `BunnyVideoProvider` in `backend/src/NaderGorge.Infrastructure/Providers/BunnyVideoProvider.cs`.
- [x] T018 [US1] Replace hard-coded YouTube/VK validation with shared provider validation in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`.
- [x] T019 [US1] Register `BunnyVideoProvider` beside existing YouTube/VK providers in `backend/src/NaderGorge.API/Program.cs`.
- [x] T020 [US1] Update student video session provider handling to allow Bunny in `backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs`.
- [x] T021 [US1] Add Bunny branch to protected embed generation in `frontend/src/app/api/video/embed/route.ts`.
- [x] T022 [US1] Add Bunny option and provider-specific labels to `frontend/src/components/admin/AddVideoForm.tsx`.
- [x] T023 [US1] Add Bunny option and provider display in edit/list UI in `frontend/src/components/admin/LessonVideoList.tsx`.
- [x] T024 [US1] Update admin service video create/update payload typing for Bunny in `frontend/src/services/admin-service.ts`.

**Checkpoint**: Provider selection MVP works without upload/cost reporting.

---

## Phase 4: User Story 2 - Upload Bunny Videos From The Platform (Priority: P1)

**Goal**: Teachers and admins upload Bunny videos from file or remote URL and link them to teacher/package/lesson.

**Independent Test**: Teacher uploads to owned lesson; admin uploads file and URL for selected teacher/package/lesson; resulting Bunny assets are attributed correctly.

### Tests for User Story 2

- [ ] T025 [P] [US2] Add backend authorization tests for teacher-owned and admin-selected Bunny upload flows in `backend/tests/NaderGorge.Application.Tests/BunnyUploadAuthorizationTests.cs`.
- [ ] T026 [P] [US2] Add mocked Bunny client tests for TUS session creation and API-key exclusion in `backend/tests/NaderGorge.Application.Tests/BunnyTusUploadTests.cs`.
- [ ] T027 [P] [US2] Add mocked Bunny client tests for URL fetch success/failure behavior in `backend/tests/NaderGorge.Application.Tests/BunnyUrlFetchTests.cs`.
- [ ] T028 [P] [US2] Add frontend upload component tests for file progress/retry and admin teacher/package/lesson selection in `frontend/src/components/admin/__tests__/BunnyUploadPanel.test.tsx`.

### Implementation for User Story 2

- [x] T029 [P] [US2] Add Bunny upload commands and DTOs in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs`.
- [x] T030 [US2] Implement teacher/package/lesson ownership validation for Bunny uploads in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs`.
- [x] T031 [US2] Implement TUS upload session command creating Bunny video, `LessonVideo`, and `BunnyVideoAsset` in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs`.
- [x] T032 [US2] Implement URL fetch command with non-playable failure handling in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs`.
- [x] T033 [US2] Implement upload completion/status refresh command in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs`.
- [x] T034 [US2] Add Bunny upload endpoints to `backend/src/NaderGorge.API/Controllers/AdminController.cs`.
- [x] T035 [P] [US2] Add frontend Bunny upload API methods in `frontend/src/services/admin-service.ts`.
- [x] T036 [US2] Build Bunny upload controls with TUS progress/retry in `frontend/src/components/admin/AddVideoForm.tsx`.
- [x] T037 [US2] Integrate Bunny upload panel into add-video flow in `frontend/src/components/admin/AddVideoForm.tsx`.
- [ ] T038 [US2] Add admin teacher/package/lesson selectors for Bunny upload in `frontend/src/components/admin/BunnyUploadPanel.tsx`.
- [ ] T039 [US2] Add upload status/error rendering for Bunny videos in `frontend/src/components/admin/LessonVideoList.tsx`.

**Checkpoint**: Uploads are linked and attributable, independent of reporting.

---

## Phase 5: User Story 3 - Track Bunny Costs For Admins Only (Priority: P2)

**Goal**: Admins can sync and view monthly Bunny storage, bandwidth, and cost snapshots by video, teacher, package, and platform; teachers see no cost data.

**Independent Test**: Seed mocked snapshots for multiple teachers/packages and verify admin filters/totals while teacher routes omit or deny cost data.

### Tests for User Story 3

- [ ] T040 [P] [US3] Add backend snapshot calculation tests for storage, bandwidth, preserved rates, and totals in `backend/tests/NaderGorge.Application.Tests/BunnyCostSnapshotTests.cs`.
- [ ] T041 [P] [US3] Add admin-only authorization tests for Bunny cost endpoints in `backend/tests/NaderGorge.Application.Tests/BunnyCostAuthorizationTests.cs`.
- [ ] T042 [P] [US3] Add frontend report filter/render tests in `frontend/src/components/admin/__tests__/BunnyCostReports.test.tsx`.

### Implementation for User Story 3

- [x] T043 [P] [US3] Add Bunny cost sync command and DTOs in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyCostSyncCommands.cs`.
- [x] T044 [US3] Implement Bunny storage/bandwidth snapshot creation with saved pricing rates in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyCostSyncCommands.cs`.
- [x] T045 [P] [US3] Add Bunny cost report query and DTOs in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetBunnyCostReportQuery.cs`.
- [x] T046 [US3] Add admin-only cost sync and report endpoints in `backend/src/NaderGorge.API/Controllers/AdminController.cs`.
- [x] T047 [US3] Add Bunny pricing settings fields to admin settings command/query in `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdatePlatformSettingsCommand.cs`.
- [x] T048 [US3] Add Bunny pricing settings controls to `frontend/src/components/admin/AdminSettingsPageClient.tsx`.
- [x] T049 [P] [US3] Add Bunny cost report API methods and types in `frontend/src/services/admin-service.ts`.
- [x] T050 [US3] Build monthly Bunny cost reports UI in `frontend/src/components/admin/BunnyCostReports.tsx`.
- [ ] T051 [US3] Add admin navigation entry or dashboard placement for Bunny reports in the existing admin route/component that owns video/report navigation.
- [ ] T052 [US3] Audit teacher-facing DTOs/components and remove any Bunny cost/storage/bandwidth leakage in `frontend/src/components/teacher/` and related backend teacher queries.

**Checkpoint**: Admin cost reporting is functional and teachers cannot access cost data.

---

## Phase 6: User Story 4 - Align AI Video Workflows With Bunny Capabilities (Priority: P3)

**Goal**: Bunny videos interact with existing AI analysis safely: processing videos block cleanly, ready videos proceed when media access exists, unsupported states are explicit.

**Independent Test**: Trigger AI for Bunny processing/ready/unavailable states and verify no crash, no broken lesson video, and current YouTube/VK AI behavior remains unchanged.

### Tests for User Story 4

- [ ] T053 [P] [US4] Add backend AI eligibility tests for Bunny statuses in `backend/tests/NaderGorge.Application.Tests/BunnyAiEligibilityTests.cs`.
- [ ] T054 [P] [US4] Add worker/provider metadata tests for Bunny AI job payloads in `worker/tests/bunny-video-provider.test.ts`.

### Implementation for User Story 4

- [ ] T055 [US4] Add Bunny readiness/media-access checks before AI enqueue in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminVideoAiCommands.cs`.
- [ ] T056 [US4] Extend AI job payload/provider mapping for Bunny in `backend/src/NaderGorge.Infrastructure/Services/RedisJobEnqueuer.cs`.
- [ ] T057 [US4] Add Bunny media resolution support or explicit unsupported handling in the worker video analysis entrypoint under `worker/src/`.
- [ ] T058 [US4] Surface Bunny AI processing/unsupported messages in `frontend/src/components/admin/LessonVideoList.tsx`.
- [ ] T059 [US4] Add optional Bunny smart actions trigger helper to `backend/src/NaderGorge.Infrastructure/Services/BunnyStreamClient.cs` without replacing existing platform AI outputs.

**Checkpoint**: AI requests for Bunny videos are safe and explicit.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Security hardening, UX consistency, docs, and regressions across stories.

- [ ] T060 [P] Add security regression test ensuring Bunny API key is never present in upload/session responses in `backend/tests/NaderGorge.Application.Tests/BunnySecretLeakTests.cs`.
- [ ] T061 [P] Add Playwright E2E smoke for admin provider selection and teacher no-cost visibility in `frontend/tests/e2e/bunny-video-provider.spec.ts`.
- [ ] T062 Harden Bunny error messages and audit logging in `backend/src/NaderGorge.Application/Features/Admin/Commands/BunnyUploadCommands.cs`.
- [ ] T063 Polish Bunny upload/report UI spacing, Arabic copy, loading, empty, and error states in `frontend/src/components/admin/BunnyUploadPanel.tsx` and `frontend/src/components/admin/BunnyCostReports.tsx`.
- [ ] T064 Update operational quickstart with real environment variable names and no secret values in `specs/138-bunny-video-provider/quickstart.md`.

---

## Phase 8: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the feature in the project environment.

- [ ] T065 Run `dotnet test backend/NaderGorge.sln` and record results in the phase report.
- [ ] T066 Run `npm --prefix frontend run lint` and record results in the phase report.
- [ ] T067 Run `npm --prefix frontend run typecheck` and record results in the phase report.
- [ ] T068 Run relevant frontend tests including `frontend/tests/e2e/bunny-video-provider.spec.ts` when the app is runnable.
- [ ] T069 Run `docker compose config -q` and record results in the phase report.
- [ ] T070 Apply migrations in the project's Docker workflow and record result.
- [ ] T071 Run API/frontend/worker/PostgreSQL/Redis health checks and record result.
- [ ] T072 Complete manual QA checklist from `specs/138-bunny-video-provider/quickstart.md`.
- [ ] T073 Run clean-code-guard review on changed production code and fix accepted findings.
- [ ] T074 Run test-guard review on changed tests and fix accepted findings.
- [ ] T075 Run feature tests summary review and write final feature report with implemented scope, commands run, test results, Docker result, manual QA status, Bunny live/mock validation status, and residual risks.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: No dependencies.
- **Phase 2 Foundational**: Depends on Phase 1 and blocks all stories.
- **US1 Provider Choice**: Depends on Phase 2 and is MVP.
- **US2 Uploads**: Depends on Phase 2; integrates best after US1 provider support.
- **US3 Cost Reports**: Depends on Phase 2 and benefits from US2 assets.
- **US4 AI Alignment**: Depends on US1 provider support and US2 Bunny asset status.
- **Polish/Verification**: Depends on implemented target stories.

### User Story Dependencies

- **US1** can be implemented after foundational work and shipped as provider-only MVP.
- **US2** can be implemented after foundational work, but student playback is cleaner after US1.
- **US3** requires Bunny asset records from foundational/US2.
- **US4** requires Bunny provider/session support and asset status fields.

### Parallel Opportunities

- T002, T003, T004 can run in parallel.
- T008, T009, T010, T013 can run in parallel after entity shape is agreed.
- Tests within each user story marked `[P]` can be written in parallel.
- Frontend UI tasks and backend command tasks in US2/US3 can run in parallel after API contracts are stable.

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete US1 so Bunny is a valid provider beside YouTube/VK.
3. Validate legacy YouTube/VK create/edit/playback.

### Incremental Delivery

1. Add upload flows (US2) and verify teacher/admin attribution.
2. Add cost snapshots/reports (US3) with admin-only visibility.
3. Add AI alignment (US4) with safe processing/unsupported states.
4. Run polish, security, Docker, and manual QA gates.
