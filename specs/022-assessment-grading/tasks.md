---
description: "Task list template for feature implementation"
---

# Tasks: Assessment Grading

**Input**: Design documents from `/specs/022-assessment-grading/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure
*(Note: Project structure is already established, no specific setup tasks required)*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data model updates required before implementing grading logic

- [x] T001 Update `Exam` entity adding `TotalScore` and `PassingScore` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`
- [x] T002 Update `Homework` entity adding `TotalScore` in `backend/src/NaderGorge.Domain/Entities/Homework/Homework.cs`
- [x] T003 Generate and apply Entity Framework Core migrations for the modified entities in `backend/src/NaderGorge.Infrastructure/`

**Checkpoint**: Foundation ready - database supports explicit assessment grading bounds.

---

## Phase 3: User Story 1 - Defining Assessment Grading Rules (Priority: P1)

**Goal**: Admins must be able to specify the absolute Total Score and Passing Score when creating or editing any Homework or Exam independently of the sum of the individual question points.

**Independent Test**: Create an Exam via the Admin Cockpit, input "Total Score = 100" and "Passing Score = 50", and verify these bounds are saved database without errors.

### Implementation for User Story 1

- [x] T004 [P] [US1] Update `CreateInlineExamCommand` to accept and validate `TotalScore` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs`
- [x] T005 [P] [US1] Update `AttachHomeworkCommand` to accept and set `TotalScore` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`
- [x] T006 [P] [US1] Expose "الدرجة النهائية" and "درجة النجاح" input fields, adding `Passing <= Total` validation in `frontend/src/components/admin/InlineExamEditor.tsx`
- [x] T007 [P] [US1] Expose "الدرجة النهائية" input field and validate against existing threshold in `frontend/src/components/admin/AddHomeworkForm.tsx`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Admins can save exams and homeworks with explicit scale bounds.

---

## Phase 4: User Story 2 - Automated Evaluation and Grading Scale (Priority: P1)

**Goal**: The system automatically calculates the student's textual Evaluation (التقدير) based on the percentage of their earned score relative to the Total Score.

**Independent Test**: Submit a dummy exam result simulating 90% score and verify that the correct Evaluation string (e.g., "ممتاز") is calculated.

### Implementation for User Story 2

- [x] T008 [US2] Update `StudentExamResult` and `StudentHomeworkResult` adding `EarnedScore` and `Evaluation` string properties in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs` and `Homework.cs`.
- [x] T009 [US2] Generate and apply EF Core migrations for the new result properties in `backend/src/NaderGorge.Infrastructure/`
- [x] T010 [US2] Create shared `GradingEvaluationService` containing textual scaling logic (ممتاز, جيد جداً, etc.) in `backend/src/NaderGorge.Application/Services/GradingEvaluationService.cs`
- [x] T011 [US2] Ensure any API models mapping Assessment Results include the `Evaluation` string so the frontend displays it appropriately.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T012 Verify `POST /api/admin/exams/inline` returns `200 OK` safely, confirming resolution of the previous `404 Not Found` environment routing bug.
- [x] T013 Run full end-to-end tests manually in development to ensure assessment creation and evaluation calculation flow correctly.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: BLOCKS all user stories
- **User Stories (Phase 3 & 4)**: Depend on Foundational phase completion. Can proceed sequentially.
- **Polish (Final Phase)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational Phase
- **User Story 2 (P2)**: Depends on Foundational Phase & partially US1 (TotalScore is needed to test Calculation).

### Parallel Opportunities

- Updating the Command Handlers (T004, T005) and Frontend UI components (T006, T007) can run entirely in parallel after T003 is complete.
