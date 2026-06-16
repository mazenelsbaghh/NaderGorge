# Tasks: Audio Upload Restrictions & Review Display

**Input**: Design documents from `/specs/136-audio-upload-restrictions-and-reviews/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md

**Tests**: Tests are MANDATORY for this project when a phase changes behavior, data, permissions, API contracts, worker jobs, or user-visible UI. We will write Playwright feature tests verifying audio restrictions and review player rendering.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup & Preparation Workflow

**Purpose**: Verify planning and prepare workspace.

- [x] T001 Verify feature branch `136-audio-upload-restrictions` is active
- [x] T002 Verify spec.md and plan.md quality check passes
- [x] T003 Complete Arabic clarification phase and update spec

---

## Phase 2: Foundational (API & Storage Infrastructure)

**Purpose**: Core API endpoints, validation logic, and storage helpers.

- [ ] T004 [P] Fix base64 file persistence to `wwwroot/uploads/audio/` in `UploadQuestionAudioCommandHandler` inside [AdminQuestionCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminQuestionCommands.cs)
- [ ] T005 [P] Add file mime-type and extension validation to `UploadQuestionAudio` in [AdminController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/AdminController.cs)
- [ ] T006 [P] Create `POST /api/Student/upload-audio` endpoint with mimetype/extension validation and file saving in [StudentController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/StudentController.cs)
- [ ] T007 Extend `QuestionReviewSnapshot` and `ExamQuestionReviewDto` with `StudentAudioUrl` property in [ExamResultBuilder.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs)

**Checkpoint**: Foundation ready - APIs are fully functional and secure.

---

## Phase 3: User Story 1 - Restrict Voice Note Uploads to Audio Files (Priority: P1)

**Goal**: Restrict file uploads to audio formats on frontend and backend.
**Independent Test**: Use a file selector inside Homework/Exam solvers to attempt uploading a non-audio file, verify rejection.

- [ ] T008 [P] [US1] Add `uploadAudio` client method to [student-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/student-service.ts)
- [ ] T009 [US1] Add file input with `accept="audio/*"` and type validation under Essay textarea in [HomeworkViewer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/homework/HomeworkViewer.tsx)
- [ ] T010 [US1] Add file input with `accept="audio/*"` and type validation under Essay textarea in [ExamViewer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/exams/ExamViewer.tsx)

**Checkpoint**: User Story 1 is functional - only audio files are accepted.

---

## Phase 4: User Story 2 - Audio Player in Homework Review (Priority: P1)

**Goal**: Display student's submitted audio note in homework review.
**Independent Test**: Complete a homework with audio, open the result panel, play the audio.

- [ ] T011 [US2] Parse and render `<audio>` player inside incorrect questions and review details sections when `providedAnswer` is an audio URL in [HomeworkResultPanel.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/homework/HomeworkResultPanel.tsx)

**Checkpoint**: User Story 2 is functional - homework audio reviews load and play.

---

## Phase 5: User Story 3 - Audio Player in Exam Review (Priority: P1)

**Goal**: Display student's submitted audio note in exam review.
**Independent Test**: Complete an exam with audio, open the review panel, play the audio.

- [ ] T012 [P] [US3] Add `studentAudioUrl` to `ExamQuestionReviewDto` in [exam-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/exam-service.ts)
- [ ] T013 [US3] Map `essay.AudioUrl` to `QuestionReviewSnapshot` in `SubmitExamCommand.cs` [SubmitExamCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs)
- [ ] T014 [US3] Map `essay.AudioUrl` to `QuestionReviewSnapshot` in `GetLatestPassedExamResultQuery.cs` [GetLatestPassedExamResultQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Exams/Queries/GetLatestPassedExamResultQuery.cs)
- [ ] T015 [US3] Update `ExamResultBuilder` to map snapshot `StudentAudioUrl` to review DTO inside [ExamResultBuilder.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Exams/ExamResultBuilder.cs)
- [ ] T016 [US3] Render student's submitted audio player inside incorrect list and full review in [ExamViewer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/exams/ExamViewer.tsx)

**Checkpoint**: User Story 3 is functional - exam audio reviews load and play.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Ensure consistency, resolve media URLs via assets domain.

- [ ] T017 [P] Resolve teacher essays audio player URL with `resolveMediaUrl` in [TeacherEssaysPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/teacher/essays/TeacherEssaysPageClient.tsx)
- [ ] T018 [P] Resolve question explanation audio player URL with `resolveMediaUrl` in [ExamProfilePageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/content/exams/%5Bid%5D/ExamProfilePageClient.tsx)
- [ ] T019 [P] Resolve question explanation audio player URL with `resolveMediaUrl` in [ExamViewer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/exams/ExamViewer.tsx)
- [ ] T020 [P] Resolve teacher correction audio player URL with `resolveMediaUrl` in [HomeworkResultPanel.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/homework/HomeworkResultPanel.tsx)

---

## Phase 7: End-of-Phase Verification & Quality Gates

**Purpose**: Build, test, lint, and run E2E validation.

- [ ] T021 Run `clean-code-guard` against modified C# and TypeScript files
- [ ] T022 Run `test-guard` against changed test scripts
- [ ] T023 Run full backend compile/build tests
- [ ] T024 Run full frontend build/lint tests
- [ ] T025 Execute Playwright feature tests for audio restrictions under `frontend/tests/e2e/audio-upload-restrictions.spec.ts`

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1) runs first.
- Foundational (Phase 2) builds the endpoints and DTOs.
- US1 (Phase 3) connects the upload buttons.
- US2 (Phase 4) and US3 (Phase 5) add the reviews.
- Polish (Phase 6) cleans up raw URL media elements.
- Verification (Phase 7) completes tests.
