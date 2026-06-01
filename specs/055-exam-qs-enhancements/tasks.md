# Tasks: Exam and Question UI Enhancements

**Input**: Design documents from `/specs/055-exam-qs-enhancements/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. This file has been tailored to ensure that tasks are highly atomic and explicitly detailed with file paths to enable robust LLM agent execution.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Exact file paths are strict instructions to LLMs.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Initialize database migration snapshot check in `backend/src/NaderGorge.Infrastructure/Data/ApplicationDbContext.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Update `Question` entity in `backend/src/NaderGorge.Domain/Entities/Question.cs` to add nullable string properties `AudioUrl`, `WrittenCorrection`, and `HintText`.
- [x] T003 [P] Create `EssaySubmission` entity in `backend/src/NaderGorge.Domain/Entities/EssaySubmission.cs` incorporating `Id` (Guid), `StudentId` (Guid), `QuestionId` (Guid), `AnswerText` (string), `AiInitialScore` (float?), `AiFeedback` (string?), `TeacherFinalScore` (float?), `TeacherFeedback` (string?), and `Status` (WaitAI, WaitTeacher, Graded).
- [x] T004 [P] Update DbContext in `backend/src/NaderGorge.Infrastructure/Data/ApplicationDbContext.cs` to include `DbSet<EssaySubmission>` and register mappings.
- [x] T005 Run `.NET CLI` EF Core add migration command in the terminal to scaffold migration for these schema changes.

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Voice & Written Explanations (Priority: P1) 🎯 MVP

**Goal**: Teachers can attach audio explanations and written text explanations to questions.

**Independent Test**: Create a question, attach audio and text, view it as a student in review mode.

### Implementation for User Story 1

- [x] T006 [P] [US1] Create API endpoint `POST /api/admin/questions/{id}/audio` in `backend/src/NaderGorge.API/Controllers/AdminQuestionController.cs` (or standard admin endpoint) to handle audio multipart form data uploads.
- [x] T007 [P] [US1] Update Question update Handlers/DTOs in `backend/src/NaderGorge.Application/Features/Questions/` to accept updating `WrittenCorrection` and `AudioUrl`.
- [x] T008 [P] [US1] Add `uploadQuestionAudio` method to `frontend/src/services/admin-service.ts` matching the backend endpoint.
- [x] T009 [US1] Inject audio upload button and text area for `WrittenCorrection` inside the `frontend/src/components/admin/QuestionBuilder.tsx` file.
- [x] T010 [US1] Modify `frontend/src/components/student/QuestionReview.tsx` (or `ExamViewer.tsx`) to conditionally render an HTML5 `<audio>` tag if `AudioUrl` exists, and a text block displaying `WrittenCorrection`.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Essay Question Correction (Priority: P1)

**Goal**: System grades essays using AI initial assessment and provides a teacher review UI for final grades.

**Independent Test**: Submit a mock essay, observe AI worker grade, then use the teacher interface to adjust the score.

### Implementation for User Story 2

- [x] T011 [P] [US2] Create Essay Grading endpoint `POST /api/teacher/essay-submissions/{id}/grade` in a newly created or existing `backend/src/NaderGorge.API/Controllers/TeacherController.cs` (or AdminController).
- [x] T012 [P] [US2] Create webhook `POST /api/internal/essay-ai-callback` in `backend/src/NaderGorge.API/Controllers/InternalController.cs` for the Node worker to ping after generating an AI grade.
- [x] T013 [P] [US2] Add API calls for essay fetching and manual grading in `frontend/src/services/teacher-service.ts` or `admin-service.ts`.
- [x] T014 [US2] Create an `EssayGradingView` teacher UI component in `frontend/src/components/teacher/EssayGradingView.tsx` with inputs for `TeacherFinalScore` and `TeacherFeedback`, displaying the `AiFeedback` side-by-side.
- [x] T015 [US2] Implement BullMQ processor `essayGradingProcessor.ts` inside `worker/src/processors/` leveraging `@google/genai` to evaluate student `answerText` against an expected system prompt, and HTTP ping the C# webhook.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Add Question Hints/Aids (Priority: P2)

**Goal**: Teachers can attach hints to questions that students can reveal during practice without point penalty.

**Independent Test**: Reveal hint during an exam state and verify the student's submission score remains unaffected.

### Implementation for User Story 3

- [x] T016 [P] [US3] Update Question Handlers/DTOs in `backend/src/NaderGorge.Application/Features/Questions/` to accept saving `HintText`.
- [x] T017 [P] [US3] Add a "Hint Text" input area to `frontend/src/components/admin/QuestionBuilder.tsx`.
- [x] T018 [US3] Create `QuestionHintButton` client component in `frontend/src/components/exam/QuestionHintButton.tsx` (using Framer Motion for a smooth dropdown/reveal transition) or mapped to `ExamViewer.tsx`.
- [x] T019 [US3] Wire the `QuestionHintButton` into the active question flow in `frontend/src/components/exam/ActiveQuestion.tsx` (mapped to `ExamViewer.tsx`).

**Checkpoint**: All user stories 1, 2, and 3 should now be independently functional

---

## Phase 6: User Story 4 - "Find the Mistake" Question Type (Priority: P2)

**Goal**: New interactive question format where students detect the error in a provided text string.

**Independent Test**: Answer a FindTheMistake question via text token click and verify standard marking rules applied.

### Implementation for User Story 4

- [x] T020 [P] [US4] Create `FindTheMistakeQuestion` entity mapped via TPH in `backend/src/NaderGorge.Domain/Entities/FindTheMistakeQuestion.cs` (string BaseText, int MistakeStartIndex, int MistakeEndIndex).
- [x] T021 [US4] Add handler logic in C# backend Features to process `QuestionType = FindTheMistake`.
- [x] T022 [P] [US4] Build `FindTheMistakeBuilder` UI inside `frontend/src/components/admin/FindTheMistakeBuilder.tsx` letting the Admin select words highlighting the index offsets.
- [x] T023 [US4] Build `FindTheMistakeInteract` UI for students in `frontend/src/components/exam/FindTheMistakeInteract.tsx` parsing `BaseText` into word spans allowing `onClick` selection marking for answers.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T024 [P] Update `frontend/src/lib/constants.ts` or internationalization to ensure "Hint", "Mistake", and "Correction" terms are mapped appropriately in Arabic UI.
- [x] T025 [P] Run linter and ensure Typescript types (`Question`, `EssaySubmission`) in `frontend/src/types` are mirrored perfectly from backend specifications.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - Phase 3, 4, 5, 6 can execute dynamically via isolated PRs.
- **Polish (Final Phase)**: Depends on all user stories.

### Parallel Opportunities

- LLMs should be able to execute any task marked `[P]` natively if `Foundational` models are scaffolded first. For example, `T006`, `T007`, `T008` can be simultaneously written contextually before merging `T009`. 
