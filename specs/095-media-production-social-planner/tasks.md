# Tasks: Media Production Pipeline and Social Planner

**Input**: Design documents from `/specs/095-media-production-social-planner/`  
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

---

## Technical Implementation Checklist

### 1. Database & Foundational Setup (Backend)

- [x] T001 Create media enums in `backend/src/NaderGorge.Domain/Enums/`:
  - `MediaStage.cs`: `Preparation = 0`, `Filming = 1`, `Editing = 2`, `Uploading = 3`, `Review = 4`, `Approved = 5`, `Published = 6`
  - `SocialPlatform.cs`: `YouTube = 0`, `Facebook = 1`, `Instagram = 2`, `TikTok = 3`, `Telegram = 4`
  - `SocialPlanStatus.cs`: `Draft = 0`, `Scripting = 1`, `Scheduled = 2`, `Published = 3`
- [x] T002 Create `MediaProductionPipeline.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/` containing:
  - `Id` (Guid, PK), `Title` (string), `Description` (string?), `Stage` (`MediaStage`), `AssignedAgentId` (Guid?), `AssetFolderUrl` (string?), `EditingErrorCount` (int), `PublishedAt` (DateTime?), `CreatedAt` (DateTime), `UpdatedAt` (DateTime?).
- [x] T003 Create `SocialMediaPlan.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/` containing:
  - `Id` (Guid, PK), `Title` (string), `Description` (string?), `Script` (string?), `Platform` (`SocialPlatform`), `Status` (`SocialPlanStatus`), `ScheduledDate` (DateTime), `MediaProductionPipelineId` (Guid?), `CreatedAt` (DateTime).
- [x] T004 Modify `TaskItem.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/` to add nullable relationship:
  - `MediaPipelineId` (Guid?) and navigation property `MediaProductionPipeline? MediaPipeline`.
- [x] T005 Register DbSet properties in `IAppDbContext.cs` and configure Fluent mappings in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` (setup Keys, Foreign Key relations with Users, Cascade deletes, and index fields).
- [x] T006 Run database migrations setup commands:
  - `dotnet ef migrations add AddMediaEntities --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API`
  - `dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API`

### 2. User Story 1 - Media Production Pipeline Tracking

- [x] T007 [P] [US1] Implement MediatR command `CreateMediaPipelineCommand.cs` in `backend/src/NaderGorge.Application/Features/Admin/Media/Commands/`:
  - Validates parameters (Title is required, AssignedAgentId must not be a Student). Creates pipeline item with stage `Preparation`.
- [x] T008 [P] [US1] Implement MediatR command `UpdateMediaPipelineCommand.cs` in `backend/src/NaderGorge.Application/Features/Admin/Media/Commands/`:
  - Updates metadata, assignee, error counts, and stage. Transitions to `Published` sets `PublishedAt = UtcNow`. Ensures transition to `Published` blocks if stage is not `Approved`.
- [x] T009 [P] [US1] Implement MediatR query `GetMediaPipelinesQuery.cs` in `backend/src/NaderGorge.Application/Features/Admin/Media/Queries/`:
  - Returns paginated list of media items. Supports searching, stage filtering, assignee filtering.
- [x] T010 [US1] Implement API controller endpoints in `backend/src/NaderGorge.API/Controllers/AdminMediaController.cs`:
  - `GET /api/admin/media/pipelines` mapped to `GetMediaPipelinesQuery`.
  - `POST /api/admin/media/pipelines` mapped to `CreateMediaPipelineCommand`.
  - `PUT /api/admin/media/pipelines/{id}` mapped to `UpdateMediaPipelineCommand`.
  - Protects endpoints with `[Authorize]` and permission claim check `media.manage`.
- [x] T011 [P] [US1] Create frontend REST client service `media-service.ts` in `frontend/src/services/media-service.ts` with API call methods:
  - `getPipelines(params)`, `createPipeline(payload)`, `updatePipeline(id, payload)`.
- [x] T012 [US1] Create frontend page `frontend/src/app/admin/media/page.tsx` rendering media pipeline board:
  - Displays glassmorphic Kanban columns for each stage: Preparation, Filming, Editing, Uploading, Review, Approved, Published.
  - Allows editors to update details, error counts, and drag-and-drop or click to transition stages.

### 3. User Story 2 - Integrated Content Approval Workflow

- [x] T013 [P] [US2] Update `UpdateMediaPipelineCommand.cs` to automatically create a `TaskItem` (reusing the operations task system) when transitioning to the `Review` stage:
  - Task Title: `مراجعة محتوى: [Media Title]`.
  - Task Status: `TaskStatus.Review`.
  - AssigneeId: The supervisor selected during the transition (passed in the update command payload).
  - MediaPipelineId: The ID of this media item.
- [x] T014 [P] [US2] Update `AdminResolveApprovalCommandHandler.cs` (in Operations tasks module) to intercept task resolutions:
  - If approved and task has `MediaPipelineId` set, automatically update the linked `MediaProductionPipeline` stage to `Approved`.
  - If rejected and task has `MediaPipelineId` set, automatically revert the linked `MediaProductionPipeline` stage to `Editing`.
- [x] T015 [US2] Update frontend board in `frontend/src/app/admin/media/page.tsx`:
  - Add dialog/form when transitioning to `Review` to choose a Supervisor assignee.
  - Add visual indicator on cards showing linked review task state (Pending, Approved, Rejected).

### 4. User Story 3 - Social Media Planner and Scheduling

- [x] T016 [P] [US3] Implement MediatR command `CreateSocialPlanCommand.cs` in `backend/src/NaderGorge.Application/Features/Admin/Media/Commands/`:
  - Creates social planner entry with title, copy/script, platform network, scheduled date, and optional `MediaProductionPipelineId`.
- [x] T017 [P] [US3] Implement MediatR query `GetSocialPlansQuery.cs` in `backend/src/NaderGorge.Application/Features/Admin/Media/Queries/`:
  - Fetches social plans, left-joining linked pipeline items to show their current stage.
- [x] T018 [US3] Expose API endpoints in `AdminMediaController.cs`:
  - `GET /api/admin/media/social-plans` mapped to `GetSocialPlansQuery`.
  - `POST /api/admin/media/social-plans` mapped to `CreateSocialPlanCommand`.
- [x] T019 [P] [US3] Update `media-service.ts` in `frontend/src/services/media-service.ts` with social planner methods:
  - `getSocialPlans(params)`, `createSocialPlan(payload)`.
- [x] T020 [US3] Create social planner dashboard view `SocialPlannerView.tsx` inside `frontend/src/components/media/`:
  - Renders scheduled posts on a calendar or monthly calendar view, displaying platform icons and linked video production status.

### 5. User Story 4 - KPIs and Production Analytics

- [x] T021 [P] [US4] Implement MediatR query `GetMediaKpisQuery.cs` in `backend/src/NaderGorge.Application/Features/Admin/Media/Queries/`:
  - Aggregates stats: total published, average editing days, and editor-wise metrics (total error counts and assets produced).
- [x] T022 [US4] Expose endpoint `GET /api/admin/media/reports/kpis` in `AdminMediaController.cs`.
- [x] T023 [P] [US4] Update `media-service.ts` in `frontend/src/services/media-service.ts` with `getMediaKpis()` method.
- [x] T024 [US4] Create reports dashboard panel `MediaKpiDashboard.tsx` in `frontend/src/components/media/`:
  - Displays metrics summaries, average processing time, and editor performance leaderboard tables.

### 6. Verification & Quality Gates

- [x] T025 [P] Write backend pipeline transitions and validation unit tests in `backend/tests/NaderGorge.Application.Tests/Media/`:
  - `MediaPipelineTests.cs`: Asserts that transition to `Published` blocks if stage is not `Approved`. Tests automatic task creation when moving to `Review`. Tests automatic stage updates on task resolution.
- [x] T026 Run `clean-code-guard` against all modified/created files.
- [x] T027 Run `test-guard` against all test files.
- [x] T028 Run full compilation and validation checks:
  - `dotnet build backend/` (verify 0 warnings/errors)
  - `npm run lint` inside `frontend/` (verify 0 warnings/errors)
  - `npm run build` inside `frontend/` (verify successful Next.js build)

---

## Dependencies & Execution Order

1. **Phase 1: Setup & Db Mappings (T001-T006)**: Blocks all other tasks.
2. **Phase 2: Core Pipeline (T007-T012)**: Foundation for media tracking.
3. **Phase 3: Approval Integration (T013-T015)**: Depends on T008 (pipeline updates).
4. **Phase 4: Social Planner (T016-T020)**: Depends on Setup phase.
5. **Phase 5: KPIs and Dashboard (T021-T024)**: Depends on core pipeline database schemas.
6. **Phase 6: Quality Gates & Tests (T025-T028)**: Runs after all features are implemented.
