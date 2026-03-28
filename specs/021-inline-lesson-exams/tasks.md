---
description: "Task list for Inline Lesson Exams"
---

# Tasks: Inline Lesson Exams

**Input**: Design documents from `/specs/021-inline-lesson-exams/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Verify branch `021-inline-lesson-exams` is active and clean.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Add `QuestionType` enum to `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`
- [x] T003 Update `QuestionBankItem` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs` to include `Type` property mapping to `QuestionType`
- [x] T004 Add `ExamId` nullable property to `LessonVideo` in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs`
- [x] T005 Run Entity Framework Core migrations to capture entity changes: `dotnet ef migrations add InlineExamsAndQuestions` and `database update`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Centralized Inline Exam Creation (Priority: P1) 🎯 MVP

**Goal**: Admins can create exams natively inside the Cockpit Exam Tab.

**Independent Test**: Render an inline interface under the existing Exam Tab in Cockpit, fill out the Title and Passing Score, and save it to the database with a 201 Created response.

### Implementation for User Story 1

- [x] T006 [US1] Create `CreateInlineExamCommand` (accepting basic exam details) in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs`
- [x] T007 [US1] Implement POST endpoint `/admin/exams/inline` in `backend/src/NaderGorge.API/Controllers/AdminController.cs` handling `CreateInlineExamCommand`
- [x] T008 [P] [US1] Add `createInlineExam` binding function to `frontend/src/services/admin-service.ts`
- [x] T009 [US1] Create `InlineExamEditor.tsx` in `frontend/src/components/admin/` with standard form fields (Title, Description, Passing Score)
- [x] T010 [US1] Integrate `InlineExamEditor.tsx` into `frontend/src/app/admin/content/lessons/[id]/page.tsx` replacing the current basic Exam linking view

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Defing Exam Questions & Types (Priority: P1)

**Goal**: Extend the inline exam logic to include adding MCQs with correct options, and Essay questions.

**Independent Test**: The inline editor can dynamically spawn Question sub-forms. MCQ questions require marking exactly one correct answer. Essay questions require no answers. Saving persists them correctly via the enhanced API endpoint.

### Implementation for User Story 2

- [x] T011 [US2] Update `CreateInlineExamCommand` to accept an array of Questions and their Options according to the contract in `specs/021-inline-lesson-exams/contracts/api.md`
- [x] T012 [US2] Update `CreateInlineExamCommandHandler` to iterate over questions, creating `QuestionBankItem`, mapping `QuestionOption`s (if MCQ), and creating `ExamQuestion` junctions
- [x] T013 [P] [US2] Create secondary `QuestionEditor.tsx` component in `frontend/src/components/admin/` to manage individual question layout, typing (MCQ/Essay) and options
- [x] T014 [US2] Integrate `QuestionEditor.tsx` as an array list inside `InlineExamEditor.tsx`, enforcing front-end validation (1 correct answer minimum for MCQ)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Target Attachment Level (Lesson vs. Video) (Priority: P2)

**Goal**: Admin determines if the exam applies to the whole lesson or an individual video.

**Independent Test**: The form has a selector (Lesson vs. Video). If Video, it loads and shows the videos of the lesson to pick from. When saved, the backend attaches it properly.

### Implementation for User Story 3

- [x] T015 [US3] Update `CreateInlineExamCommandHandler` to conditionally update `Lesson.ExamId` OR `LessonVideo.ExamId` depending on the requested target Type
- [x] T016 [P] [US3] Add Target scope selector and video dropdown to `InlineExamEditor.tsx`
- [x] T017 [US3] Ensure `InlineExamEditor.tsx` reads `lesson.videos` from the cockpit props to populate the dropdown

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T018 Confirm UI handles saving states gracefully (loading spinners, Toast notifications for error/success) in `InlineExamEditor.tsx`
- [x] T019 Update previous references or pages (e.g. `LinkExamForm` from previous phase) to be superseded by `InlineExamEditor` or co-exist cleanly
- [x] T020 Run full end-to-end tests manually in development to ensure exam creation flows correctly into database.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - Sequential priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Integrates completely into US1
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Expands US1 UI
