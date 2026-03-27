# Tasks: Phase 2 — Structured Learning and Academic Operations

**Input**: Design documents from `/specs/007-phase2-academic-ops/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Initialize Phase 2 development branch and verify existing .NET configuration

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Create Entity Framework Migration `AddPhase2AcademicOps` for all new entities in `backend/src/NaderGorge.Infrastructure`
- [x] T003 [P] Add `bullmq` and associated Redis worker dependencies to `worker/package.json`
- [x] T004 Build generic BullMQ Enqueue interface in `backend/src/NaderGorge.Infrastructure/Background/RedisJobEnqueuer.cs`

**Checkpoint**: Foundation ready - database models mapped and worker queues established.

---

## Phase 3: User Story 1 - Homework Submission and Review Flow (Priority: P1) 🎯 MVP

**Goal**: Students need to complete required homework assignments (MCQ and textual essays) to progress to the next lesson, and assistants need to grade these submissions.

**Independent Test**: Student successfully submits an essay homework via Postman and the progression blocker resets.

### Implementation for User Story 1

- [x] T005 [P] [US1] Create `Homework`, `HomeworkQuestion`, and `HomeworkSubmission` models in `backend/src/NaderGorge.Domain/Entities/Homework/Homework.cs`
- [x] T006 [P] [US1] Add AssistantReviewer RBAC logic mapping in authentication services
- [x] T007 [US1] Implement `GET /api/v1/students/homework/pending` endpoint in `backend/src/NaderGorge.API/Controllers/HomeworkController.cs`
- [x] T008 [US1] Implement `POST /api/v1/students/homework/{homeworkId}/submit` endpoint in `backend/src/NaderGorge.API/Controllers/HomeworkController.cs`
- [x] T009 [US1] Create React components for Homework view in `frontend/src/components/homework/HomeworkView.tsx`
- [x] T010 [US1] Update lesson progress logic to prevent unlock if `IsMandatory` homework is pending

**Checkpoint**: The core new academic process (Homework gating) is functioning.

---

## Phase 4: User Story 2 - Automated Commitment Engine & Warnings (Priority: P2)

**Goal**: The system automatically classifies students and dispatches warnings based on their academic behavior.

**Independent Test**: The Node.js cron job executes, correctly sweeping postgres to mark inactive students as "At Risk" and drops an SMS job into Redis.

### Implementation for User Story 2

- [x] T011 [P] [US2] Create `StudentStatusTracker` and `WarningEvent` entities in `backend/src/NaderGorge.Domain/Entities/Student/StudentStatus.cs`
- [x] T012 [P] [US2] Implement critical warning trigger in `backend/src/NaderGorge.Application/UseCases/Warnings/TriggerWarningCommand.cs`
- [x] T013 [US2] Create nightly sweep cron job in `worker/src/jobs/commitment-engine.ts`
- [x] T014 [US2] Implement BullMQ processor `notification-sender.ts` in Node Worker to handle SMS delivery queue

**Checkpoint**: Student behavior is automatically evaluated in the background without degrading the API.

---

## Phase 5: User Story 3 - Assistant Operational Dashboard (Priority: P2)

**Goal**: Assistants need a dedicated panel to handle grading, monitor at-risk students, and resolve pending alerts.

**Independent Test**: An Assistant user can view their populated queue on the frontend and issue a grade for an essay.

### Implementation for User Story 3

- [x] T015 [P] [US3] Create `AssistantTaskQueue` model in `backend/src/NaderGorge.Domain/Entities/Assistant/AssistantTaskQueue.cs`
- [x] T016 [US3] Implement `GET /api/v1/assistant/tasks/queue` endpoint in `backend/src/NaderGorge.API/Controllers/AssistantController.cs`
- [x] T017 [US3] Implement `POST /api/v1/assistant/tasks/{taskId}/resolve` endpoint to grade/close tasks
- [x] T018 [US3] Build the Assistant Dashboard UI page in `frontend/src/app/assistant/dashboard/page.tsx`

**Checkpoint**: Staff can now manage their assigned grading workloads systematically. 

---

## Phase 6: User Story 4 - Parent Reporting Layer (Priority: P3)

**Goal**: Parents need a readable summary format to track the student's academic progress, attendance, and warnings.

**Independent Test**: An unauthenticated or parent-auth user hits the report endpoint and receives a clean aggregate of the student's metrics.

### Implementation for User Story 4

- [x] T019 [P] [US4] Implement `GET /api/v1/parent/reports/{studentId}/summary` endpoint aggregating progress and warnings in `backend/src/NaderGorge.API/Controllers/ParentController.cs`
- [x] T020 [US4] Create Parent Report UI in `frontend/src/app/parent-report/[studentId]/page.tsx`

---

## Phase 7: User Story 5 - Gamification Engine (Priority: P4)

**Goal**: Students earn points, unlock badges, and climb leaderboards upon successfully completing academic tasks on time.

**Independent Test**: Submitting a test updates the Redis sorted set leaderboard and returns updated total points.

### Implementation for User Story 5

- [x] T021 [P] [US5] Create `StudentGamification` and `GamificationActionLog` entities in `backend/src/NaderGorge.Domain/Entities/Gamification/StudentGamification.cs`
- [x] T022 [P] [US5] Implement `GET /api/v1/students/gamification/status` endpoint 
- [x] T023 [US5] Insert points evaluation logic (Domain Events) inside the Homework/Exam Grading use cases in `backend/src/NaderGorge.Application/UseCases/`
- [x] T024 [US5] Build Gamification points UI element in `frontend/src/components/layout/Header.tsx` (or Sidebar)

**Checkpoint**: The platform is substantially more engaging for students.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T025 Execute end-to-end integration tests over the Homework -> Assistant Grade -> Gamification Points complete flow.
- [x] T026 Update Swagger / OpenAPI documentation for all new endpoints.
- [x] T027 Code cleanup and linting execution.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup
- **User Stories (Phase 3-7)**: All depend on Foundational phase completion.
- **Polish (Final Phase)**: Depends on all user stories.

### User Story Dependencies

- **US1 (Homework) & US2 (Commitment)**: Can be developed perfectly in parallel once Phase 2 is complete.
- **US3 (Assistant Dashboard)**: Ideally begins *after* US1 models are done (as tasks generally revolve around grading Homeworks).
- **US4 (Parent) & US5 (Gamification)**: Can be run in parallel anytime after US1 and US2, relying heavily on the base structures created in earlier stories.

### Parallel Opportunities

- The Node Worker logic (`worker/src/jobs/`) can be built completely in parallel to the Frontend React components (`frontend/src/`).
- `US1`, `US2`, `US4`, and `US5` Backend EF Core Entities and DTOs can be entirely modeled and committed simultaneously by multiple developers.
