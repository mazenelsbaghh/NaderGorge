---
description: "Task list for Exam Editor Enhancements implementation"
---

# Tasks: Exam Editor Enhancements

**Input**: Design documents from `/specs/024-exam-editor-enhancements/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md
**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Setup & Foundational Prerequisites

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [x] T001 Install `react-quill` dependencies in `frontend/package.json` 
- [x] T002 Update `NaderGorge.Domain/Entities/ExamEntities.cs` to add `TimePerQuestionSeconds` to Exam entity
- [x] T003 Generate EF Core migration for `TimePerQuestionSeconds` addition in `NaderGorge.Infrastructure`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 2: User Story 1 - Global Per-Question Timer (Priority: P1) 🎯 MVP

**Goal**: Move the timer from individual question setup to the global exam level for a faster admin workflow.

**Independent Test**: Can be fully tested by creating a new exam, filling out the "Time per question" field, adding questions, and observing that the exam successfully saves and inherits the global timer.

### Implementation for User Story 1

- [x] T004 [US1] Update `NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs` to handle `TimePerQuestionSeconds` in `CreateInlineExamCommand`
- [x] T005 [P] [US1] Update `GetExamDashboardQuery` to return the new setting
- [x] T006 [P] [US1] Update `frontend/src/services/admin-service.ts` to include `timePerQuestionSeconds` in exam DTOs
- [x] T007 [US1] Update `InlineExamEditor.tsx` to display and capture the new global timer field
- [x] T008 [US1] Update `QuestionEditor.tsx` to remove the individual `DurationSeconds` field completely 

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently.

---

## Phase 3: User Story 2 - Rich Text Question Editing (Priority: P2)

**Goal**: Allow admins to visually format question text and apply highlighting/colors using a Rich Text Editor.

**Independent Test**: Tested by opening the "Add Question" view, typing text, applying colors and bold formatting using a toolbar, saving, and verifying the HTML output is preserved and rendered correctly.

### Implementation for User Story 2

- [x] T009 [US2] Import and mount `ReactQuill` in `QuestionEditor.tsx` to replace the textarea
- [x] T010 [US2] Update CSS/Tailwind configuration in `frontend/src/app/globals.css` or component file to natively style the Quill editor to match the Admin UI theme
- [x] T011 [US2] Update `frontend/src/components/exams/ExamViewer.tsx` (or student answering component) to render `question.text` as HTML using `dangerouslySetInnerHTML`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T012 Run full E2E UI testing of creating an exam and fetching it as a student
- [x] T013 Verify mobile responsiveness of the Rich Text Editor toolbar

---

## Dependencies & Execution Order

- **Foundational (Phase 1)**: Must be completed first to lay the generic database groundwork and install packages.
- **User Story 1 & 2** can technically run in parallel, though US1 handles DB saving logic and US2 handles UI text editing logic.
- Both stories culminate in the `QuestionEditor.tsx` modifications, so sequentially doing US1 then US2 is slightly cleaner.
