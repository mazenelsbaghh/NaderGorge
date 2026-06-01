# Tasks: Phase 2 Data Integrity Fixes

**Input**: Design documents from `/specs/062-fix-data-integrity/`
**Prerequisites**: [plan.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/plan.md), [spec.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/spec.md), [research.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/research.md), [data-model.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/data-model.md), [contracts/data-integrity.openapi.yaml](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/contracts/data-integrity.openapi.yaml), [quickstart.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/062-fix-data-integrity/quickstart.md)

**Tests**: No dedicated test-first tasks are included because the specification did not explicitly request TDD. Final verification is captured in the polish phase and should be executed before merge.

**Organization**: Tasks are grouped by user story so each slice can be handed to a lower-cost LLM and completed with minimal cross-story context.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency on unfinished work
- **[Story]**: Maps the task to a user story from the specification (`US1`, `US2`, `US3`)
- Every task includes exact file paths and avoids hidden multi-step work

## Phase 1: Setup (Shared Scaffolding)

**Purpose**: Prepare shared helpers and client contract placeholders used by multiple stories

- [X] T001 Create the shared watch-threshold helper in `backend/src/NaderGorge.Application/Common/VideoWatchThresholdCalculator.cs`
- [X] T002 [P] Extend shared frontend contract placeholders for `currentMode`, `audioUrl`, `writtenCorrection`, and `rejectionReason` in `frontend/src/services/student-service.ts`, `frontend/src/services/exam-service.ts`, and `frontend/src/services/video-session-service.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Persisted schema changes that block multiple stories

**⚠️ CRITICAL**: No user story work should begin until this phase is complete

- [X] T003 Add `CurrentMode` to `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs`
- [X] T004 [P] Add `RejectionReason` to `backend/src/NaderGorge.Domain/Entities/ExtraWatchRequest.cs`
- [X] T005 Create the EF Core migration and snapshot updates for `StudentProfile.CurrentMode` and `ExtraWatchRequest.RejectionReason` in `backend/src/NaderGorge.Infrastructure/Migrations/` and `backend/src/NaderGorge.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`

**Checkpoint**: Foundation complete. Story work can now start.

---

## Phase 3: User Story 1 - Accurate lesson watch tracking (Priority: P1) 🎯 MVP

**Goal**: Make both watch-tracking flows use the same threshold rule, start first views at zero, and reject missing-duration requests

**Independent Test**: Send equivalent watch activity through both tracking flows and confirm they produce the same threshold seconds, the same first-view behavior, and the same validation failure when duration is missing

### Implementation for User Story 1

- [X] T006 [US1] Require valid `totalDurationSeconds` and map duration validation failures to `400` responses in `backend/src/NaderGorge.API/Controllers/TrackingController.cs`
- [X] T007 [US1] Require valid `totalDurationSeconds` for student video-session tracking in `backend/src/NaderGorge.API/Controllers/VideoSessionController.cs`
- [X] T008 [US1] Refactor new-watch creation and single-threshold counting in `backend/src/NaderGorge.Application/Features/Tracking/Commands/RecordVideoEventCommand.cs`
- [X] T009 [US1] Replace local threshold math with the shared helper and single-increment logic in `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs`
- [X] T010 [P] [US1] Align zero-based first-watch semantics in `backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs` and `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs`
- [X] T011 [P] [US1] Surface duration-required failures and revised threshold data in `frontend/src/services/video-session-service.ts` and `frontend/src/components/video/SecureVideoPlayer.tsx`

**Checkpoint**: User Story 1 is independently functional and ready for MVP validation.

---

## Phase 4: User Story 2 - Reliable persistence of student and exam data (Priority: P2)

**Goal**: Persist student theme mode correctly, auto-create missing profiles, retain essay audio references, and return written corrections only after exam completion

**Independent Test**: Save theme preferences for students with and without profiles, submit essay answers with optional audio, and verify completed exam results include written corrections while in-progress results do not

### Implementation for User Story 2

- [X] T012 [US2] Accept `currentMode` in the student theme update request model in `backend/src/NaderGorge.API/Controllers/StudentController.cs`
- [X] T013 [US2] Resolve `CurrentMode` from persisted profile data inside `backend/src/NaderGorge.Application/Features/Student/StudentThemeDtos.cs`
- [X] T014 [US2] Add upsert profile creation and `CurrentMode` persistence to `backend/src/NaderGorge.Application/Features/Student/Commands/UpdateStudentThemePreferencesCommand.cs`
- [X] T015 [P] [US2] Return persisted `CurrentMode` for both existing and newly created profiles in `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentThemePreferencesQuery.cs` and `frontend/src/services/student-service.ts`
- [X] T016 [P] [US2] Send and retain `currentMode` in `frontend/src/hooks/useStudentThemePreferences.ts` and `frontend/src/hooks/useStudentTheme.tsx`
- [X] T017 [US2] Update the student theme settings flow to keep the active mode and selected palettes synchronized in `frontend/src/components/student/StudentThemeSettingsPanel.tsx`
- [X] T018 [US2] Add optional `audioUrl` to exam answer submission DTOs in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs` and `frontend/src/services/exam-service.ts`
- [X] T019 [US2] Persist essay `audioUrl` without breaking typed-only essay submissions in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs` and `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`
- [X] T020 [US2] Return `writtenCorrection` only for eligible completed results in `backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs` and `backend/src/NaderGorge.Application/Features/Exams/Queries/GetLatestPassedExamResultQuery.cs`
- [X] T021 [P] [US2] Render the finalized `writtenCorrection` and retained essay `audioUrl` safely in `frontend/src/components/exams/ExamViewer.tsx` and `frontend/src/app/student/exams/[examId]/page.tsx`

**Checkpoint**: User Story 2 is independently functional without depending on extra watch rejection work.

---

## Phase 5: User Story 3 - Transparent outcomes for extra watch requests (Priority: P3)

**Goal**: Store a rejection reason when an extra watch request is rejected and show it only to the affected student when the latest request is rejected

**Independent Test**: Reject an extra watch request with a reason, then confirm the student status lookup returns the same reason only for the rejected state

### Implementation for User Story 3

- [X] T022 [US3] Add the reject-request body model that accepts `reason` in `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [X] T023 [US3] Persist `RejectionReason` and resolution metadata inside `backend/src/NaderGorge.Application/Features/Admin/Commands/RejectWatchRequestCommand.cs`
- [X] T024 [US3] Return `rejectionReason` only for rejected latest requests in `backend/src/NaderGorge.Application/Features/Student/Queries/CheckExtraWatchStatusQuery.cs`
- [X] T025 [P] [US3] Extend the extra-watch status DTO shape in `frontend/src/services/video-session-service.ts`
- [X] T026 [US3] Show the rejected extra-watch reason in the student player flow in `frontend/src/components/video/SecureVideoPlayer.tsx`

**Checkpoint**: User Story 3 is independently functional and student-visible.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final alignment, documentation refresh, and branch-level verification

- [X] T027 [P] Refresh field-level examples and descriptions in `specs/062-fix-data-integrity/contracts/data-integrity.openapi.yaml`
- [X] T028 [P] Refresh the manual walkthrough steps in `specs/062-fix-data-integrity/quickstart.md`
- [X] T029 Run branch-level validation against `specs/062-fix-data-integrity/quickstart.md`, `backend/tests/NaderGorge.Application.Tests/`, and `frontend/src/components/video/SecureVideoPlayer.tsx`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all story work
- **Phase 3 (US1)**: Depends on Phase 2 only
- **Phase 4 (US2)**: Depends on Phase 2 only
- **Phase 5 (US3)**: Depends on Phase 2 only
- **Phase 6 (Polish)**: Depends on all completed stories you intend to ship

### User Story Dependencies

- **US1**: Fully independent after Phase 2 and is the recommended MVP slice
- **US2**: Independent from US1 in business behavior, but it shares frontend service files with US3 and exam submission files with result rendering work inside the same story
- **US3**: Independent from US1 and US2 in business behavior, but it shares `frontend/src/services/video-session-service.ts` and `frontend/src/components/video/SecureVideoPlayer.tsx` with US1

### Recommended Execution Order

1. Complete Phase 1 and Phase 2 fully
2. Implement **US1** and validate it as the MVP
3. Implement **US2**
4. Implement **US3**
5. Finish Phase 6 polish tasks

### File-Level Coordination Notes

- Do not run **T008** and **T009** in parallel because they implement the same threshold rule across two tracking flows and must stay semantically aligned
- Do not run **T014**, **T018**, **T019**, or **T020** in parallel because they all change exam/theme persistence files with overlapping request/response semantics
- Do not run **T011** and **T026** in parallel because both change `frontend/src/components/video/SecureVideoPlayer.tsx`
- **T015** and **T016** can run in parallel after **T014**
- **T022**, **T023**, and **T024** should run sequentially before the frontend work in **T025** and **T026**

---

## Parallel Example: User Story 1

```bash
# After T008 and T009 are complete:
Task: "T010 Align zero-based first-watch semantics in backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs and backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs"
Task: "T011 Surface duration-required failures and revised threshold data in frontend/src/services/video-session-service.ts and frontend/src/components/video/SecureVideoPlayer.tsx"
```

---

## Parallel Example: User Story 2

```bash
# After T014 is complete:
Task: "T015 Return persisted CurrentMode for both existing and newly created profiles in backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentThemePreferencesQuery.cs and frontend/src/services/student-service.ts"
Task: "T016 Send and retain currentMode in frontend/src/hooks/useStudentThemePreferences.ts and frontend/src/hooks/useStudentTheme.tsx"

# After T020 is complete:
Task: "T021 Render the finalized writtenCorrection and retained essay audioUrl safely in frontend/src/components/exams/ExamViewer.tsx and frontend/src/app/student/exams/[examId]/page.tsx"
```

---

## Parallel Example: User Story 3

```bash
# After T024 is complete:
Task: "T025 Extend the extra-watch status DTO shape in frontend/src/services/video-session-service.ts"
Task: "T026 Show the rejected extra-watch reason in frontend/src/components/video/SecureVideoPlayer.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Validate watch-threshold consistency and duration-required behavior
5. Stop for review before expanding scope

### Incremental Delivery

1. Deliver **US1** first because it repairs the most central data corruption path and has the clearest user-visible validation
2. Deliver **US2** second because it spans theme persistence and exam result fidelity without affecting extra-watch moderation flows
3. Deliver **US3** last because it is isolated and easier to land after the shared video-session client changes from US1

### Low-Cost LLM Execution Notes

1. Give the model one task at a time, never an entire phase
2. Do not combine tasks that touch the same file, even if they are in the same user story
3. Re-read the referenced file before editing because this repository already has substantial in-flight changes
4. Keep migrations isolated to the foundational phase so later tasks can assume the schema exists
5. Use the quickstart steps as the final manual verification checklist, not as implementation instructions
