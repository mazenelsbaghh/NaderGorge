# Tasks: Real-time Platform Speed & Sync

**Input**: Design documents from `/specs/121-realtime-platform-speed/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Database migrations and entity mappings for OutboxEvents.

- [x] T001 Create database migration for `OutboxEvents` table in `backend/src/NaderGorge.Infrastructure/Persistence/Migrations/`
- [x] T002 Add `OutboxEvent` model class in `backend/src/NaderGorge.Domain/Entities/OutboxEvent.cs`
- [x] T003 Map `OutboxEvent` in `backend/src/NaderGorge.Infrastructure/Persistence/AppDbContext.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Base SignalR hub setup, Outbox background service, and frontend SignalR hook.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T004 Create `PlatformHub.cs` SignalR hub in `backend/src/NaderGorge.API/Hubs/PlatformHub.cs` supporting User, Role, and dynamic Package/Lesson group routing.
- [x] T005 Register SignalR services and map hub route `/hubs/platform` in `backend/src/NaderGorge.API/Program.cs`
- [x] T006 Implement `OutboxProcessorBackgroundService.cs` background worker in `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs` to process outbox events every 2 seconds.
- [x] T007 Create custom hook `usePlatformEvents.ts` in `frontend/src/hooks/usePlatformEvents.ts` to manage client connection to `/hubs/platform` and listen to events.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Instant Notification & Balance Updates (Priority: P1) 🎯 MVP

**Goal**: Deliver real-time notifications and balance updates to students immediately upon change.

**Independent Test**: Trigger a balance change on admin side; verify student balance number updates instantly without reload.

### Tests for User Story 1
- [x] T008 [P] [US1] Create integration test verifying balance change writes an outbox event in `backend/tests/NaderGorge.Application.Tests/BalanceOutboxTests.cs`

### Implementation for User Story 1
- [x] T009 [US1] Update `NaderGorge.Application` balance update handlers (e.g. `PurchaseContentHandler.cs` or code activation handlers) to write a `BalanceChanged` outbox event to context
- [x] T010 [US1] Update `NaderGorge.Application` notification creation service/handler to write a `NotificationCreated` outbox event to context
- [x] T011 [P] [US1] Update `frontend/src/components/layout/Sidebar.tsx` to subscribe to `BalanceChanged` and `NotificationCreated` events via `usePlatformEvents` hook and update UI local state/cache
- [x] T012 [P] [US1] Update `frontend/src/components/layout/StudentBottomNav.tsx` (or dashboard shell) to show toast notifications on `NotificationCreated`

---

## Phase 4: User Story 2 - Real-time Lesson and Content Updates (Priority: P1)

**Goal**: Update curriculum and lesson media views in real-time when published/updated or when video processing completes.

**Independent Test**: Publish a lesson on admin panel; check student package page. The new lesson card must render instantly.

### Tests for User Story 2
- [x] T013 [P] [US2] Create integration test verifying lesson publishing writes outbox event in `backend/tests/NaderGorge.Application.Tests/LessonOutboxTests.cs`

### Implementation for User Story 2
- [x] T014 [US2] Update Lesson publish commands in `backend/src/NaderGorge.Application/Content/Commands/PublishLessonHandler.cs` to record a `LessonPublished` outbox event
- [x] T015 [US2] Update worker status callbacks in `backend/src/NaderGorge.API/Controllers/InternalController.cs` to write a `VideoReady` outbox event when chapter analysis is completed
- [x] T016 [P] [US2] Update `frontend/src/services/content-service.ts` to expose `clearPackagesCache()` and force refetch
- [x] T017 [US2] Update the packages grid/curriculum components in `frontend/src/app/student/packages/components/CurriculumList.tsx` (or package page client) to invalidate and refetch on `LessonPublished`, `VideoReady`, or `ResourceReady`

---

## Phase 5: User Story 3 - Real-time AI Analytics Monitoring (Priority: P2)

**Goal**: Monitor AI background job progress in real-time without fast polling.

**Independent Test**: Initiate an AI job; watch progress bar update percentage in real-time.

### Implementation for User Story 3
- [x] T018 [US3] Update worker callback controllers in `backend/src/NaderGorge.API/Controllers/InternalController.cs` to write `AiJobProgress` outbox events
- [x] T019 [P] [US3] Update AI monitor client component `frontend/src/app/admin/ai-monitor/AiMonitorClient.tsx` to listen to `AiJobProgress` events via SignalR and update progress state

---

## Phase 6: User Story 4 - Secure, Fast and Idempotent Operations (Priority: P1)

**Goal**: Implement Redis-backed idempotency filter to prevent duplicate submissions or charges.

**Independent Test**: Submit two requests with the same `Idempotency-Key` header and check that the second returns cached response.

### Implementation for User Story 4
- [x] T020 [P] [US4] Implement `RedisIdempotencyService.cs` in `backend/src/NaderGorge.Infrastructure/Services/RedisIdempotencyService.cs`
- [x] T021 [US4] Implement `IdempotentAttribute.cs` action filter in `backend/src/NaderGorge.API/Filters/IdempotentAttribute.cs` using the idempotency service to validate the `Idempotency-Key` header
- [x] T022 [US4] Apply `[Idempotent]` to purchase and code activation controllers in `backend/src/NaderGorge.API/Controllers/StudentController.cs`

---

## Phase 7: Polish, Quality Gates & Verification

**Purpose**: Execute code guards, build, validation checks, and update master documents.

- [x] T023 Run `clean-code-guard` on all modified production C# and TypeScript files
- [x] T024 Run `test-guard` on all modified tests files
- [x] T025 Run full compilation and test commands: `cd backend && dotnet test` and `cd frontend && npm run build`
- [x] T026 Update `docs/backend_plan.md`, `docs/frontend_plan.md`, `docs/ui_ux_plan.md`, and `docs/ops_plan.md` to document the completed implementation

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: Blocks Phase 2.
- **Phase 2 (Foundational)**: Blocks all user stories.
- **User Stories (Phase 3+)**: Can run sequentially in priority order or in parallel.
- **Phase 7 (Quality Gates & Verification)**: Blocks final completion.
