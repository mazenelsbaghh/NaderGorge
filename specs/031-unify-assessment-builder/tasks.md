---

description: "Task list for Unified Assessment Builder implementation"
---

# Tasks: Unified Assessment Builder

**Input**: Design documents from `/specs/031-unify-assessment-builder/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/api.md
**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Verify project compiles and runs with no existing errors before modifications

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Update `Exam` entity in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs` to include `IsMandatory` and `IsRandomized` boolean properties.
- [x] T003 Update `Homework` entity in `backend/src/NaderGorge.Domain/Entities/Homework/Homework.cs` to include `IsRandomized` boolean property.
- [x] T004 Generate and apply EF Core Migration for the unified builder explicitly mapping these properties (e.g., `UnifiedBuilderMigration`) in the backend context.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Unified Builder Interface (Priority: P1) 🎯 MVP

**Goal**: As an administrator, I want to use a single, unified interface to create both exams and homework assignments.

**Independent Test**: Can be fully tested by opening the builder inside a lesson, selecting "Homework" or "Exam", adding questions, saving, and verifying the correct entity was created.

### Implementation for User Story 1

- [x] T005 [US1] Create new shared React component `frontend/src/components/admin/UnifiedAssessmentBuilder.tsx` based on the old `InlineExamEditor` but accepting a `type` prop ('exam' | 'homework').
- [x] T006 [P] [US1] Refactor `frontend/src/app/admin/content/lessons/[id]/page.tsx` to replace `<InlineExamEditor>` and `<AddHomeworkForm>` usages with the new `<UnifiedAssessmentBuilder>`.
- [x] T007 [US1] Map the submission logic inside `UnifiedAssessmentBuilder` to route the unified payload correctly to either `/api/Admin/homework` or `/api/Admin/exams` depending on the `type` prop.
- [x] T008 [US1] Translate question structures properly in the payload within the component.
- [x] T009 [P] [US1] Delete `frontend/src/components/admin/AddHomeworkForm.tsx` and `InlineExamEditor.tsx`. as it is now obsolete.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Assessment Configuration Toggles (Priority: P2)

**Goal**: As an administrator, I want to be able to toggle whether an assessment's questions should be randomized and whether completing it is mandatory.

**Independent Test**: Can be tested by creating an assessment, enabling randomization and mandatory flags, then verifying the questions appear in random order and the next lesson is locked until passed.

### Implementation for User Story 2

- [x] T010 [P] [US2] Update `CreateExamCommand` and `UpdateExamCommand` in `backend` to accept and map `IsMandatory` and `IsRandomized` DTO properties.
- [x] T011 [P] [US2] Update `CreateHomeworkCommand` and `UpdateHomeworkCommand` in `backend` to accept and map `IsRandomized` DTO property.
- [x] T012 [US2] Add the UI switches (Toggle buttons) to the Shared Assessment Builder for "Question Randomization" and "Mandatory Completion".
- [x] T013 [US2] Ensure the frontend passes these mapped boolean variables in its payload.
- [x] T014 [US2] Update query handlers in `backend` to shuffle exam and homework question sequences if `IsRandomized` is true before returning to the student UI.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: Polish & Cross-Cutting Concerns

- [x] T015 Verify `seed-test-course` admin endpoint properly accommodates these flags (e.g. setting true/false).
- [x] T016 Run comprehensive Next.js frontend build test `npm run build` to ensure no typing regressions from component refactoring.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2)
- **User Story 2 (P2)**: Can start after US1 is mostly structured because it adds toggles to the newly created component, though backend DTOs can be modified in parallel.

### Parallel Opportunities

- The backend migration (Phase 2) can be done by a backend dev while Phase 3 (US1 Component unification) is started by a frontend dev.
- Update commands across Exams and Homeworks are independent and can be paralleled (T010 and T011).
