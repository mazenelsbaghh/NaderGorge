# Tasks: Real-time Speed Remaining Completion

**Input**: Design documents from `/specs/123-realtime-speed-remaining-completion/`
**Prerequisites**: plan.md (required), spec.md (required), predecessor feature 122-realtime-speed-completion (100% complete)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Setup & Data Model (WebVitalsMetric)

**Purpose**: Scaffold the database table and entity for client Web Vitals reporting.

- [x] T001 [P] Create `WebVitalsMetric.cs` in `backend/src/NaderGorge.Domain/Entities/WebVitalsMetric.cs` inheriting from `BaseEntity`. Include fields `MetricName`, `Value`, `Rating`, `PageUrl`, `UserAgent`.
- [x] T002 Add `DbSet<WebVitalsMetric> WebVitalsMetrics { get; }` to `IAppDbContext.cs` and `AppDbContext.cs`.
- [x] T003 In `AppDbContext.cs`, add Entity mapping configurations for `WebVitalsMetric` setting appropriate MaxLength limits (e.g. 32 for MetricName/Rating, 512 for PageUrl/UserAgent).
- [x] T004 Run `dotnet ef migrations add AddWebVitalsMetricsTable --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API` to scaffold the migration.

**Checkpoint**: Backend compiles successfully with new DbSets and migrations.

---

## Phase 2: Complete Outbox Events Coverage (US1)

**Purpose**: Inject outbox events into all remaining state-changing commands so they propagate via SignalR.

- [x] T005 [P] In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`:
  - Under `UpdateTermCommandHandler`, add `TermUpdated` outbox event with `TargetGroup = $"Package_{term.PackageId}"`.
  - Under `DeleteTermCommandHandler`, add `TermDeleted` outbox event with `TargetGroup = $"Package_{term.PackageId}"`.
- [x] T006 [P] In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`:
  - Under `CreateSectionCommandHandler`, add `SectionCreated` outbox event.
  - Create and register `SectionUpdated` and `SectionDeleted` events in their corresponding handlers.
- [x] T007 [P] In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`:
  - Under `CreateVideoCommandHandler`, add `VideoProcessingStarted` event.
  - Under `UpdateVideoCommandHandler`, add `VideoUpdated` event.
  - Under `DeleteVideoCommandHandler`, add `VideoDeleted` event.
- [x] T008 [P] In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`:
  - Under `CreateLessonResourceCommandHandler`, add `ResourceReady` event.
  - Add corresponding delete/update outbox events to Lesson Resource handlers.
- [x] T009 [P] In Homework command handlers:
  - Add `HomeworkPublished` event when attaching homework to a lesson.
  - Add `HomeworkGraded` event in `GradeEssayCommandHandler` (when grading homework essays).
- [x] T010 [P] In Exams command handlers:
  - Add `ExamPublished` event when linking exams.
  - Add `ExamResultReady` event when automatic or manual grading finishes.
- [x] T011 [P] In Community command handlers:
  - Add `CommunityPostCreated` event when a post is created (pending moderation).
  - Add `CommunityPostApproved` event when admin approves a post.
  - Add `CommunityPostLiked` event when likes count changes.
- [x] T012 [P] In Community comment handlers:
  - Add `CommunityCommentCreated` event (pending moderation).
  - Add `CommunityCommentApproved` event upon approval.
- [x] T013 [P] Add `AiJobQueued` and `AiJobCancelled` events inside Video AI analysis request and cancellation commands.

**Checkpoint**: Build backend and verify all commands compile and outbox events list matches expectations.

---

## Phase 3: Lesson Detail Query Partitioning (US2)

**Purpose**: Partition the large lesson detail response payload to allow lazy loading.

- [x] T014 [P] In `GetLessonDetailQuery.cs`, modify the return DTO and handler to exclude the eager loading of Comments and Resources arrays.
- [x] T015 [P] Create MediatR query `GetLessonResourcesQuery.cs` in `backend/src/NaderGorge.Application/Features/Content/Queries/` to return the list of resources for a lesson.
- [x] T016 [P] Create MediatR query `GetLessonCommentsQuery.cs` in `backend/src/NaderGorge.Application/Features/Content/Queries/` to return comments with offset-based pagination.
- [x] T017 Register endpoints in `ContentController.cs`:
  - `GET /api/v1/content/lessons/{id}/resources`
  - `GET /api/v1/content/lessons/{id}/comments`
- [x] T018 In frontend `content-service.ts`, implement separate functions to fetch lesson resources and lesson comments asynchronously.
- [x] T019 In `LessonDetailPageClient.tsx`, update the UI hooks to fetch resources and comments asynchronously on component mount instead of using eager page props.

---

## Phase 4: Shell Store Optimization (US2)

**Purpose**: Prevent full shell refreshes by performing incremental store updates.

- [x] T020 In frontend `src/store/auth-store.ts` (or relevant shell store), implement methods to update notification badge counts and balance directly upon SignalR event receipt without triggering full refetches.

---

## Phase 5: Client Web Vitals & Redis Rate Limiting (US3)

**Purpose**: Monitor performance metrics and protect heavy routes.

- [x] T021 Create `WebVitalsController.cs` in `NaderGorge.API/Controllers/` with `POST /api/v1/metrics/web-vitals` endpoint.
- [x] T022 Implement `CreateWebVitalsMetricCommand` MediatR handler in `NaderGorge.Application/Features/Metrics/Commands/CreateWebVitalsMetricCommand.cs` to write reports to database.
- [x] T023 In frontend, implement hook or client script `useWebVitalsReporter.ts` to capture and post LCP, CLS, INP metrics to the backend.
- [x] T024 Apply per-user rate limiting using StackExchange.Redis to code activation, starting AI chapters, and generating download URLs.

---

## Phase 6: Remaining Outbox Events & Clear Notifications Implementation

**Purpose**: Implement the 15 missing outbox events and the clear notifications feature.

- [x] T030 Create `ClearNotificationsCommand.cs` in `backend/src/NaderGorge.Application/Features/Student/Commands/` and expose the endpoint `POST /api/v1/student/notifications/clear` in `StudentController.cs`.
- [x] T031 Emit `PackageArchived` and `PackagePublished` in `UpdatePackageCommand.cs` based on active status transition.
- [x] T032 Emit `PackageAccessGranted` in `PurchaseContentCommand.cs`, `ActivateCodeCommand.cs`, and `AdminCreateUserCommand.cs`.
- [x] T033 Emit `TermPublished` in `CreateTermCommand` and `SectionPublished` in `CreateSectionCommand`.
- [x] T034 Emit `ResourceProcessingStarted` in `CreateLessonResourceCommand`.
- [x] T035 Emit `CodeGroupCreated` and `CodeGroupExportReady` in `BulkGenerateCodesCommand.cs`.
- [x] T036 Emit `PurchaseCompleted` and `PurchaseFailed` in `PurchaseContentCommand.cs`.
- [x] T037 Emit `NotificationRead` in `MarkNotificationAsReadCommand.cs` and `NotificationsCleared` in `ClearNotificationsCommand.cs`.
- [x] T038 Emit `ExamGraded` in `GradeEssayCommand.cs` and `SubmitExamCommand.cs`.
- [x] T039 Emit `LessonLocked` and `LessonUnlocked` dynamically in `SubmitExamCommand.cs` and `GradeEssayCommand.cs` for progression blocks.
- [x] T040 Update frontend `usePlatformEvents.ts` to add listeners and invalidation rules for all 15 events.

---

## Phase 7: Quality Gates & Verification

**Purpose**: Verify all changes build cleanly, pass lint/tests, and conform to clean code standards.

- [x] T041 Run `clean-code-guard` on all modified production C# and TypeScript files.
- [x] T042 Run `test-guard` on all modified test files.
- [x] T043 Run full backend tests: `cd backend && dotnet test` — all tests must pass.
- [x] T044 Run full frontend build: `cd frontend && npm run build && npm run lint` — zero errors.
- [x] T045 Run Docker stack configuration check and health validations.
