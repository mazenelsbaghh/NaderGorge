# Tasks: Phase 3 Logic and Performance Fixes

**Input**: Design documents from `/specs/063-fix-logic-performance/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api-contracts.md, quickstart.md

**Tests**: No test-authoring tasks are included because the specification did not request TDD. Validation and execution tasks are included so an implementation model can verify completion against existing test and runtime flows.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently by a lower-cost LLM with minimal hidden context.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`)
- Every task includes the primary file path to change

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Introduce shared primitives that reduce ambiguity for later tasks.

- [X] T001 Create platform setting key constants in `backend/src/NaderGorge.Application/Common/PlatformSettingKeys.cs`
- [X] T002 Create cached platform settings snapshot/reader in `backend/src/NaderGorge.Application/Common/CachedPlatformSettings.cs`
- [X] T003 Register `IMemoryCache` and the cached settings reader in `backend/src/NaderGorge.API/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the shared persistence and settings plumbing that blocks all three user stories.

**⚠️ CRITICAL**: Do not start user story tasks until this phase is complete.

- [X] T004 Extend `StudentAnswer` with `HintUsed`, `QuestionStartedAt`, and `TimedOut` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`
- [X] T005 [P] Persist the new `StudentAnswer` fields in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [X] T006 Expose any new persistence surface needed for answer updates in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [X] T007 [P] Add support for `MaxExtraWatchRequestsPerVideo` and `HintPenaltyPercentage` defaults in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetPlatformSettingsQuery.cs`
- [X] T008 Update admin settings writes to invalidate the cached settings entry in `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdatePlatformSettingsCommand.cs`
- [X] T009 Create the EF Core migration for phase 3 answer metadata in `backend/src/NaderGorge.Infrastructure/Data/Migrations/<timestamp>_AddPhase3LogicPerformanceFixes.cs`

**Checkpoint**: Shared settings access, cache invalidation, and answer metadata persistence are ready for story work.

---

## Phase 3: User Story 1 - Reliable watch tracking and request limits (Priority: P1) 🎯 MVP

**Goal**: Make watch tracking consistent, duration-aware, and capped correctly for repeated extra watch requests.

**Independent Test**: Submit watch events and session progress with valid/invalid durations, then submit repeated extra watch requests for one video and confirm stable threshold behavior plus a clear limit-reached failure.

### Implementation for User Story 1

- [X] T010 [P] [US1] Replace direct settings reads with the cached settings reader in `backend/src/NaderGorge.Application/Features/Tracking/Commands/RecordVideoEventCommand.cs`
- [X] T011 [P] [US1] Replace direct settings reads with the cached settings reader in `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs`
- [X] T012 [US1] Normalize threshold calculation and single-increment behavior in `backend/src/NaderGorge.Application/Features/Tracking/Commands/RecordVideoEventCommand.cs`
- [X] T013 [US1] Normalize threshold calculation and single-increment behavior in `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs`
- [X] T014 [US1] Enforce the per-video request cap using `MaxExtraWatchRequestsPerVideo` in `backend/src/NaderGorge.Application/Features/Student/Commands/CreateExtraWatchRequestCommand.cs`
- [X] T015 [US1] Return limit-reached failures as client-visible bad requests in `backend/src/NaderGorge.API/Controllers/VideoSessionController.cs`
- [X] T016 [US1] Keep strict `DURATION_REQUIRED` handling aligned with the updated tracking flow in `backend/src/NaderGorge.API/Controllers/TrackingController.cs`

**Checkpoint**: User Story 1 is complete when watch tracking reuses cached settings, rejects missing durations, and blocks extra watch requests above the configured cap.

---

## Phase 4: User Story 2 - Predictable comment and moderation behavior (Priority: P2)

**Goal**: Make moderation filters exact and remove the community moderation N+1 count pattern without expanding API scope.

**Independent Test**: Query lesson moderation with valid and invalid status values, query a student's own lesson comments with rejected records present, and load the admin community posts list with comment/like totals still present.

### Implementation for User Story 2

- [X] T017 [P] [US2] Parse and validate lesson comment status filters with enum matching in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetLessonCommentsForModerationQuery.cs`
- [X] T018 [P] [US2] Hide rejected lesson comments from the student self-view in `backend/src/NaderGorge.Application/Features/Content/Queries/GetMyLessonCommentsQuery.cs`
- [X] T019 [US2] Map invalid lesson moderation status values to `400 Bad Request` in `backend/src/NaderGorge.API/Controllers/AdminLessonCommentsController.cs`
- [X] T020 [P] [US2] Parse and validate community post status filters with enum matching in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetCommunityPostsForModerationQuery.cs`
- [X] T021 [US2] Replace per-post comment and like count lookups with set-based aggregation in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetCommunityPostsForModerationQuery.cs`
- [X] T022 [US2] Map invalid community moderation status values to `400 Bad Request` in `backend/src/NaderGorge.API/Controllers/AdminCommunityController.cs`

**Checkpoint**: User Story 2 is complete when moderation filters are exact, invalid statuses fail clearly, and community post totals load without per-row follow-up counts.

---

## Phase 5: User Story 3 - Fair exam scoring and time enforcement (Priority: P3)

**Goal**: Persist help-tool usage and per-question timing state so final scoring is reproducible and server-enforced.

**Independent Test**: Start an exam attempt, use fifty-fifty on one MCQ, let one timed question expire, submit the attempt, and confirm the result shows a penalty on the helped question plus zero score on the timed-out question.

### Implementation for User Story 3

- [X] T023 [US3] Create placeholder or reusable answer records with `QuestionStartedAt` when an attempt starts in `backend/src/NaderGorge.Application/Features/Exams/Commands/StartExamAttemptCommand.cs`
- [X] T024 [P] [US3] Persist `HintUsed = true` on the targeted question answer in `backend/src/NaderGorge.Application/Features/Exams/Commands/UseFiftyFiftyCommand.cs`
- [X] T025 [US3] Rework answer persistence to update existing answer records instead of writing timing-blind duplicates in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`
- [X] T026 [US3] Mark late answers as `TimedOut` using server-side elapsed time in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`
- [X] T027 [P] [US3] Apply `HintPenaltyPercentage` and clamp awarded scores at zero in `backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs`
- [X] T028 [US3] Ensure timed-out answers always surface as zero-score review snapshots in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`

**Checkpoint**: User Story 3 is complete when help-tool penalties and per-question timeout rules are both persisted and reflected in result payloads.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency pass and execution verification across all stories.

- [X] T029 [P] Update feature documentation notes if implementation decisions changed the planned behavior in `specs/063-fix-logic-performance/plan.md`
- [X] T030 Run the backend test project for the affected application flows via `backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj`
- [ ] T031 Run the quickstart verification checklist against `specs/063-fix-logic-performance/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup**: Starts immediately.
- **Phase 2: Foundational**: Depends on Phase 1 and blocks all user stories.
- **Phase 3: US1**: Depends on Phase 2 only. This is the MVP slice.
- **Phase 4: US2**: Depends on Phase 2 only.
- **Phase 5: US3**: Depends on Phase 2 only.
- **Phase 6: Polish**: Depends on whichever user stories are implemented.

### User Story Dependencies

- **US1 (P1)**: Independent after foundational work.
- **US2 (P2)**: Independent after foundational work.
- **US3 (P3)**: Independent after foundational work.

### Task-Level Dependency Notes

- **T002** depends on **T001**.
- **T003** depends on **T002**.
- **T005** and **T006** depend on **T004**.
- **T008** depends on **T002** and **T003**.
- **T009** depends on **T004** through **T008** being settled.
- **T010** and **T011** depend on **T001** through **T008**.
- **T012** depends on **T010**.
- **T013** depends on **T011**.
- **T014** depends on **T007** and **T008**.
- **T015** depends on **T014**.
- **T016** depends on **T010** and **T012**.
- **T017** through **T022** depend only on foundational completion.
- **T021** depends on **T020**.
- **T022** depends on **T020**.
- **T023** depends on **T004** through **T009**.
- **T024** depends on **T023**.
- **T025** depends on **T023**.
- **T026** depends on **T023** and **T025**.
- **T027** depends on **T007**, **T024**, and **T025**.
- **T028** depends on **T025** through **T027**.
- **T030** and **T031** depend on the implemented story phases being complete.

### Story Completion Order

1. **US1** for MVP delivery.
2. **US2** for moderation correctness and performance.
3. **US3** for exam scoring integrity.

---

## Parallel Opportunities

- [ ] T005 [P] and T007 [P] can be done in parallel after T004 because they touch different files.
- [ ] T010 [P] and T011 [P] can be done in parallel after foundational work because they affect separate command handlers.
- [ ] T017 [P] and T018 [P] can be done in parallel for the two lesson-comment query paths.
- [ ] T020 [P] and T019 [P] can be done in parallel because one changes the admin community query and the other changes the lesson comments controller.
- [ ] T024 [P] and T027 [P] can proceed in parallel after T023/T025 establish reusable answer records.

## Parallel Example: User Story 1

```bash
Task: "Update backend/src/NaderGorge.Application/Features/Tracking/Commands/RecordVideoEventCommand.cs to use cached settings"
Task: "Update backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs to use cached settings"
```

## Parallel Example: User Story 2

```bash
Task: "Update backend/src/NaderGorge.Application/Features/Admin/Queries/GetLessonCommentsForModerationQuery.cs to parse enum status filters"
Task: "Update backend/src/NaderGorge.Application/Features/Content/Queries/GetMyLessonCommentsQuery.cs to hide rejected comments"
```

## Parallel Example: User Story 3

```bash
Task: "Update backend/src/NaderGorge.Application/Features/Exams/Commands/UseFiftyFiftyCommand.cs to persist HintUsed"
Task: "Update backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs to apply hint penalties and timeout zero-scoring"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 (US1) only.
3. Run T030 and the US1-relevant parts of T031.
4. Stop and validate the watch tracking/request-limit slice before touching moderation or exams.

### Incremental Delivery

1. Finish shared setup and foundational tasks once.
2. Deliver **US1** as the first independently testable increment.
3. Deliver **US2** next without reopening watch-tracking logic.
4. Deliver **US3** last because it has the highest answer-state complexity.

### Cheap-LLM Execution Guidance

1. Execute tasks strictly in task ID order unless a task is explicitly marked as parallel-safe above.
2. Do not combine adjacent tasks into one prompt; each task is intentionally small.
3. After each completed phase, restate the affected files before moving to the next phase.
4. When a task modifies one file only, keep the prompt scoped to that file and the exact rule in the description.

---

## Notes

- Every user story phase is independently testable from the acceptance criteria in `spec.md`.
- The cheapest safe MVP is **US1 only**.
- The highest-risk tasks for a smaller model are **T023-T028** because they change persisted exam-answer lifecycle behavior; keep them isolated and sequential.
- Format validation passed: every task uses checkbox + task ID + optional markers + story label where required + explicit file path.
