---
description: "Task list template for feature implementation"
---

# Tasks: Lesson Progression Stepper

**Input**: Design documents from `/specs/029-lesson-progression-stepper/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Initialize the feature branch according to plan.md (Completed via Speckit Setup)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

*(No blocking infrastructural changes for this feature since Prisma/Next.js/Framer are already installed)*

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Sequential Lesson Unlocking (Priority: P1) 🎯 MVP

**Goal**: Block access to a lesson if the user hasn't completed the homework and exam of the preceding lesson.

**Independent Test**: Can be independently tested by attempting to open a locked lesson without completing the prior lesson's homework and exam. The system should block access and prompt completion.

### Implementation for User Story 1

- [ ] T002 [US1] Backend: Create or update `backend/src/NaderGorge.Core/Features/Lessons/Validations/LessonProgressionValidator.cs` to evaluate prerequisite completion status for the requested lesson.
- [ ] T003 [US1] Backend: Modify `GetLessonDetailQueryHandler.cs` (or corresponding endpoint) to return `isLocked: boolean` and `lockedReason: string` based on the validator.
- [ ] T004 [P] [US1] Frontend: Update TypeScript DTO interfaces in `frontend/src/services/content-service.ts` to include `isLocked` and `lockedReason`.
- [ ] T005 [US1] Frontend: Update `frontend/src/components/content/LessonViewer.tsx` to display a Locked State UI preventing video/resource access if `isLocked` is true.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently.

---

## Phase 4: User Story 2 - Randomized Assessments (Priority: P2)

**Goal**: Randomize the order of questions and answers for each student attempt.

**Independent Test**: Can be tested by opening the same exam with two different student accounts. The questions and their respective multiple-choice answers must appear in a uniquely shuffled order.

### Implementation for User Story 2

- [ ] T006 [P] [US2] Create or update a utility function in `frontend/src/lib/utils.ts` (or similar) to handle deterministic or pure random array shuffling (e.g., Fisher-Yates).
- [ ] T007 [US2] Update `frontend/src/components/content/LessonViewer.tsx` to shuffle `lesson.homework.questions` and their nested `options` locally on initial component mount using `useEffect` or `useMemo`.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently.

---

## Phase 5: User Story 3 - Unified Stepper Assessment Experience (Priority: P3)

**Goal**: Interact with both homework and exams using the same animated, step-by-step interface (Stepper) that aligns with the academy's premium brand identity.

**Independent Test**: Can be tested by navigating through both an exam and a homework assignment. Both must use the same stepper component with fluid spring animations and identical branding.

### Implementation for User Story 3

- [ ] T008 [P] [US3] Create `frontend/src/components/ui/animated-stepper.tsx` using Framer Motion, adapting the provided React Bits code to utilize standard platform CSS variables (e.g. `var(--admin-primary)`).
- [ ] T009 [US3] Refactor `frontend/src/components/content/LessonViewer.tsx` to replace the vertical scrolling homework form with the new `<AnimatedStepper />` component.
- [ ] T010 [US3] Ensure the `AnimatedStepper` cleanly executes the `handleHomeworkSubmit` function when the student completes the final step.

**Checkpoint**: All user stories should now be independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T011 [P] Review `animated-stepper.tsx` animations to ensure smooth transitions without layout jumping, matching the Editorial Design System.
- [ ] T012 Run `quickstart.md` manual validation tests to ensure progression locking and randomization work harmoniously within the new Stepper UI.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Setup.
- **User Stories (Phase 3+)**: Depend on Foundational phase.
- **Polish (Final Phase)**: Depends on all user stories.

### Parallel Opportunities

- T004 can be executed in parallel with backend tasks (T002, T003).
- T006 and T008 can be executed purely independently before plugging them into `LessonViewer.tsx`.

## Implementation Strategy

### Incremental Delivery

1. Complete US1 first: Prove the backend can securely lock lessons.
2. Complete US2 second: Ensure the data shuffling works correctly before introducing complex UI states.
3. Complete US3 third: Wrap the working homework logic inside the massive UI overhaul (`AnimatedStepper`). This prevents UI bugs from masking core logical bugs.
