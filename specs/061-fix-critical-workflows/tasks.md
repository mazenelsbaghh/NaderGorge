# Tasks: Restore Critical Learning Workflows

**Input**: Design documents from `/specs/061-fix-critical-workflows/`
**Prerequisites**: [plan.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/061-fix-critical-workflows/plan.md), [spec.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/061-fix-critical-workflows/spec.md), [research.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/061-fix-critical-workflows/research.md), [data-model.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/061-fix-critical-workflows/data-model.md), [contracts/critical-workflows.openapi.yaml](/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/061-fix-critical-workflows/contracts/critical-workflows.openapi.yaml)

**Tests**: Include backend regression tests for each user story because the constitution requires service-layer coverage and this feature repairs broken production workflows.

**Organization**: Tasks are grouped by user story to keep each slice independently implementable and testable by a lower-cost LLM without extra interpretation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency on unfinished work
- **[Story]**: Maps the task to a specific user story from the spec
- Every task includes exact file paths and avoids hidden multi-step work

## Phase 1: Setup (Shared Scaffolding)

**Purpose**: Prepare focused regression-test files and shared test helpers before domain changes start

- [X] T001 Create feature-specific regression test files in `backend/tests/NaderGorge.Application.Tests/CommunityCommentModerationTests.cs`, `backend/tests/NaderGorge.Application.Tests/FindTheMistakeSubmissionTests.cs`, and `backend/tests/NaderGorge.Application.Tests/EssayGradingWorkflowTests.cs`
- [X] T002 [P] Extend shared test builders and fake context helpers in `backend/tests/NaderGorge.Application.Tests/TestAppDbContextFactory.cs` for community comments, find-the-mistake questions, and essay submissions

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain and persistence changes required before any story can be implemented safely

**⚠️ CRITICAL**: Do not start user-story tasks before this phase is complete

- [X] T003 Add community comment moderation state and rejection fields in `backend/src/NaderGorge.Domain/Entities/CommunityPost.cs`
- [X] T004 Add the four-step essay grading status model in `backend/src/NaderGorge.Domain/Entities/EssaySubmission.cs`
- [X] T005 [P] Persist the new community comment and essay fields in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [X] T006 Create the EF Core migration for community comment moderation and essay grading status changes in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [X] T007 [P] Add shared frontend DTO placeholders for moderation and grading-state payloads in `frontend/src/services/admin-service.ts`, `frontend/src/services/community-service.ts`, and `frontend/src/services/exam-service.ts`

**Checkpoint**: Foundation complete. User-story implementation can begin.

---

## Phase 3: User Story 1 - Moderate Community Comments Before Publication (Priority: P1) 🎯 MVP

**Goal**: New community comments stay hidden until an admin explicitly approves them, with a pending queue and rejection flow

**Independent Test**: Submit a new comment as a student, confirm it is hidden from the student feed, approve or reject it from the admin queue, then confirm only approved comments become visible

### Tests for User Story 1

- [X] T008 [P] [US1] Add backend regression tests for pending, approve, reject, and student-visibility rules in `backend/tests/NaderGorge.Application.Tests/CommunityCommentModerationTests.cs`

### Implementation for User Story 1

- [X] T009 [US1] Make newly created community comments default to `Pending` and return a moderation-aware response in `backend/src/NaderGorge.Application/Features/Community/Commands/CreateCommunityPostCommentCommand.cs`
- [X] T010 [US1] Restrict student-facing comment reads to approved comments only in `backend/src/NaderGorge.Application/Features/Community/Queries/GetCommunityPostCommentsQuery.cs`
- [X] T011 [P] [US1] Create the pending-comment moderation query and DTOs in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetCommunityCommentsForModerationQuery.cs`
- [X] T012 [P] [US1] Create comment-approval and comment-rejection commands in `backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveCommunityCommentCommand.cs` and `backend/src/NaderGorge.Application/Features/Admin/Commands/RejectCommunityCommentCommand.cs`
- [X] T013 [US1] Expose `GET /api/admin/community/comments/pending`, `POST /approve`, and `POST /reject` endpoints in `backend/src/NaderGorge.API/Controllers/AdminCommunityController.cs`
- [X] T014 [US1] Add admin service methods and DTOs for comment moderation in `frontend/src/services/admin-service.ts`
- [X] T015 [US1] Extend the admin moderation screen to list and act on pending comments in `frontend/src/components/admin/CommunityPostsModerationTable.tsx` and `frontend/src/app/admin/community/page.tsx`
- [X] T016 [US1] Update student comment composer behavior so pending comments are acknowledged without rendering unpublished content in `frontend/src/components/student/CommunityPostComments.tsx` and `frontend/src/services/community-service.ts`

**Checkpoint**: User Story 1 is fully functional and can ship as the MVP for this feature branch

---

## Phase 4: User Story 2 - Grade Find-The-Mistake Answers Correctly (Priority: P2)

**Goal**: Find-the-mistake submissions use their own answer payload and scoring logic instead of multiple-choice evaluation

**Independent Test**: Submit one correct and one incorrect find-the-mistake answer, then confirm score, correctness, and review output match the selected mistake rather than MCQ behavior

### Tests for User Story 2

- [X] T017 [P] [US2] Add backend regression tests for correct, incorrect, and missing find-the-mistake submissions in `backend/tests/NaderGorge.Application.Tests/FindTheMistakeSubmissionTests.cs`

### Implementation for User Story 2

- [X] T018 [US2] Extend answer-submission DTOs to carry find-the-mistake payload fields in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs` and `frontend/src/services/exam-service.ts`
- [X] T019 [US2] Capture find-the-mistake answers from the student exam viewer in `frontend/src/components/exams/ExamViewer.tsx`
- [X] T020 [US2] Implement a dedicated `QuestionType.FindTheMistake` grading branch in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`
- [X] T021 [US2] Extend exam review DTOs to return find-the-mistake answer details in `backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs` and `backend/src/NaderGorge.Application/Features/Exams/Queries/GetLatestPassedExamResultQuery.cs`
- [X] T022 [US2] Update student exam result types and rendering for find-the-mistake review output in `frontend/src/services/exam-service.ts`, `frontend/src/components/exams/ExamViewer.tsx`, and `frontend/src/app/student/exams/[examId]/page.tsx`

**Checkpoint**: User Story 2 is independently testable without relying on community moderation or essay grading

---

## Phase 5: User Story 3 - Track Essay Grading Through Completion (Priority: P3)

**Goal**: Essay answers move through explicit AI and teacher grading states, expose grading progress, and avoid final exam results until grading is complete

**Independent Test**: Submit an exam with essay questions, confirm initial `WaitAI` state, complete the internal AI callback, confirm teacher grading unlocks only after AI scoring, then finalize grading and verify the attempt becomes completed

### Tests for User Story 3

- [X] T023 [P] [US3] Add backend regression tests for AI callback transitions, teacher-grade preconditions, grading-status queries, and partial-result behavior in `backend/tests/NaderGorge.Application.Tests/EssayGradingWorkflowTests.cs`

### Implementation for User Story 3

- [X] T024 [US3] Propagate the new essay status names and result-state fields through submission DTOs in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs` and `backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs`
- [X] T025 [P] [US3] Create the exam-attempt grading-status query and response DTOs in `backend/src/NaderGorge.Application/Features/Exams/Queries/GetExamAttemptGradingStatusQuery.cs`
- [X] T026 [US3] Update AI callback transitions to persist AI score and advance the essay lifecycle in `backend/src/NaderGorge.Application/Features/Webhooks/Commands/WebhookEssayGradedCommand.cs`
- [X] T027 [US3] Enforce teacher-grading preconditions and final status updates in `backend/src/NaderGorge.Application/Features/Admin/Commands/GradeEssayCommand.cs`
- [X] T028 [US3] Expose the grading-status endpoint and tighten essay-grade controller behavior in `backend/src/NaderGorge.API/Controllers/ExamsController.cs` and `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [X] T029 [US3] Return partial or pending exam results when essay grading is incomplete in `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`, `backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs`, and `backend/src/NaderGorge.Application/Features/Exams/Queries/GetLatestPassedExamResultQuery.cs`
- [X] T030 [US3] Align internal callback response semantics with the new lifecycle in `backend/src/NaderGorge.API/Controllers/InternalController.cs`
- [X] T031 [US3] Extend admin essay grading DTOs and teacher-review UI states in `frontend/src/services/admin-service.ts` and `frontend/src/components/admin/EssayGradingView.tsx`
- [X] T032 [US3] Add student-facing grading-status consumption and pending-result handling in `frontend/src/services/exam-service.ts`, `frontend/src/app/student/exams/[examId]/page.tsx`, and `frontend/src/components/exams/ExamViewer.tsx`

**Checkpoint**: User Story 3 is independently testable after Phase 2, though it shares core exam files with User Story 2

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup, contract sync, and full-feature verification

- [X] T033 [P] Update the final API contract and example payloads after implementation in `specs/061-fix-critical-workflows/contracts/critical-workflows.openapi.yaml`
- [X] T034 [P] Update validation and walkthrough steps to match the final behavior in `specs/061-fix-critical-workflows/quickstart.md`
- [X] T035 Harden shared frontend typings after all story work lands in `frontend/src/services/admin-service.ts`, `frontend/src/services/community-service.ts`, and `frontend/src/services/exam-service.ts`
- [X] T036 Run the full backend regression cleanup pass in `backend/tests/NaderGorge.Application.Tests/CommunityCommentModerationTests.cs`, `backend/tests/NaderGorge.Application.Tests/FindTheMistakeSubmissionTests.cs`, and `backend/tests/NaderGorge.Application.Tests/EssayGradingWorkflowTests.cs`

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
- **US2**: Independent from US1 in business behavior, but it touches shared exam files that also change in US3
- **US3**: Independent from US1 in business behavior, but it shares `SubmitExamCommand.cs`, `ExamResultBuilder.cs`, and exam frontend files with US2

### Recommended Execution Order

1. Finish Phase 1 and Phase 2 completely
2. Implement **US1** and validate it as the MVP
3. Implement **US2**
4. Implement **US3**
5. Finish Phase 6 polish tasks

### File-Level Coordination Notes

- Do not run **T020**, **T024**, **T029**, or **T032** in parallel because they modify the same exam workflow files
- **T011** and **T012** can run in parallel after **T009** and **T010**
- **T014** and **T016** can run in parallel after **T013**
- **T025** and **T026** can run in parallel after **T024**

---

## Parallel Example: User Story 1

```bash
# After T009 and T010 are complete:
Task: "T011 Create the pending-comment moderation query and DTOs in backend/src/NaderGorge.Application/Features/Admin/Queries/GetCommunityCommentsForModerationQuery.cs"
Task: "T012 Create comment-approval and comment-rejection commands in backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveCommunityCommentCommand.cs and backend/src/NaderGorge.Application/Features/Admin/Commands/RejectCommunityCommentCommand.cs"

# After T013 is complete:
Task: "T014 Add admin service methods and DTOs for comment moderation in frontend/src/services/admin-service.ts"
Task: "T016 Update student comment composer behavior in frontend/src/components/student/CommunityPostComments.tsx and frontend/src/services/community-service.ts"
```

---

## Parallel Example: User Story 2

```bash
# After T018 is complete:
Task: "T019 Capture find-the-mistake answers from the student exam viewer in frontend/src/components/exams/ExamViewer.tsx"
Task: "T017 Add backend regression tests in backend/tests/NaderGorge.Application.Tests/FindTheMistakeSubmissionTests.cs"
```

---

## Parallel Example: User Story 3

```bash
# After T024 is complete:
Task: "T025 Create the exam-attempt grading-status query and response DTOs in backend/src/NaderGorge.Application/Features/Exams/Queries/GetExamAttemptGradingStatusQuery.cs"
Task: "T026 Update AI callback transitions in backend/src/NaderGorge.Application/Features/Webhooks/Commands/WebhookEssayGradedCommand.cs"

# After T028 is complete:
Task: "T031 Extend admin essay grading DTOs and teacher-review UI states in frontend/src/services/admin-service.ts and frontend/src/components/admin/EssayGradingView.tsx"
Task: "T032 Add student-facing grading-status handling in frontend/src/services/exam-service.ts, frontend/src/app/student/exams/[examId]/page.tsx, and frontend/src/components/exams/ExamViewer.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Validate student-hidden pending comments plus admin approve/reject flow
5. Ship or demo the moderation repair before touching exam workflows

### Incremental Delivery

1. Deliver **US1** first because it is the cleanest independent slice and lowest-risk handoff for a cheaper LLM
2. Deliver **US2** second because it changes exam submission logic but has smaller surface area than essay lifecycle work
3. Deliver **US3** last because it spans callbacks, grading rules, aggregate result state, admin review UI, and student exam status

### Low-Cost LLM Execution Notes

1. Give the model one task at a time, never an entire phase
2. Complete all non-parallel tasks in order
3. For tasks marked `[P]`, only parallelize them if they do not touch the same file
4. Re-run the relevant regression test file after each completed story
5. Do not let the model improvise new endpoints or filenames outside this task list unless a blocking inconsistency is found

---

## Notes

- Total tasks: 36
- User-story tasks: **US1 = 9**, **US2 = 6**, **US3 = 10**
- Setup + Foundational tasks: 7
- Polish tasks: 4
- Suggested MVP scope: **Phase 1 + Phase 2 + Phase 3 (US1 only)**
- All tasks follow the required checklist format with task ID, optional `[P]`, required story label where applicable, and explicit file paths
