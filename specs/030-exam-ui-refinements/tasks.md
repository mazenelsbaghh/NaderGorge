---
description: "Task list for Exam UI Refinements and Locked Reasons"
---

# Tasks: Exam UI Refinements and Locked Reasons

**Input**: Design documents from `/specs/030-exam-ui-refinements/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

*(No additional project setup or library installation tasks required for this feature)*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

*(No foundational tasks required for this feature)*

---

## Phase 3: User Story 1 - Clear Locked Lesson Reasons (Priority: P1) 🎯 MVP

**Goal**: Display exactly why a lesson is locked by pulling the title of the blocking homework or exam.

**Independent Test**: Can be fully tested by fetching the details of a lesson that depends on an unpassed assessment.

### Implementation for User Story 1

- [x] T001 [US1] Modify `GetLessonDetailQueryHandler` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs` to fetch and inject the exact assessment title into the `LockedReason` message.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Enhanced Exam Navigation UI (Priority: P1)

**Goal**: Transform exam navigation into a series of checkbox-like circles indicating answered, current, skipped, and unvisited states. Add a skip button.

**Independent Test**: Start an exam, answer some questions, skip others, and visually confirm the navigation circles update their states accurately.

### Implementation for User Story 2

- [x] T002 [US2] Update `AnimatedStepper` element mapping in `frontend/src/components/ui/animated-stepper.tsx` to support `status` per step (e.g., `'unvisited'`, `'current'`, `'answered'`, `'skipped'`) and render numbered circles accordingly.
- [x] T003 [US2] Update `ExamViewer` in `frontend/src/components/exams/ExamViewer.tsx` to compute status arrays (`answered`, `skipped`) and pass them downwards to the `AnimatedStepper`.
- [x] T004 [US2] Add a "تخطي" (Skip) button to the current active question view in `ExamViewer` that flags the current idx as skipped and navigates Next.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Exam Countdown Timer (Priority: P2)

**Goal**: Display a dynamic, styled countdown timer indicating the remaining exam duration.

**Independent Test**: Enter an active timed exam and observe the animated countdown ticking. 

### Implementation for User Story 3

- [x] T005 [P] [US3] Add `.countdown` CSS rules to `frontend/src/styles/globals.css` taking inspiration from the DaisyUI countdown mechanism.
- [x] T006 [US3] Create a new `CountdownTimer` component in `frontend/src/components/exams/CountdownTimer.tsx` wrapping the CSS variables (`--value`) tied to a 1000ms tick React `useEffect`.
- [x] T007 [US3] Replace previous `ExamTimer` or internal time rendering logic in `frontend/src/components/exams/ExamViewer.tsx` with the new `CountdownTimer` component to enforce submission on expiry.

**Checkpoint**: All user stories up to P2 should now be independently functional.

---

## Phase 6: User Story 4 - Free Test Content Seeding (Priority: P3)

**Goal**: Allow quick, single-click generation of a 0-cost Term hierarchy for seamless full-flow system testing without payment hurdles.

**Independent Test**: Hit the endpoint via Postman/Swagger and ensure a new Term package is visible without a price tag.

### Implementation for User Story 4

- [x] T008 [P] [US4] Create `SeedTestCourseCommand` and its handler inside `backend/src/NaderGorge.Application/Features/Admin/Commands/SeedTestCourseCommand.cs`.
  - The command must generate a `Term` with title indicating it's a test course, `Price = 0`, containing 1 `ContentSection` with 2 dummy `Lessons`.
  - The first `Lesson` should have an attached mandatory `Homework`. The second `Lesson` should test an inline/associated `Exam`.
- [x] T009 [US4] Add `[HttpPost("seed-test-course")]` admin endpoint to `backend/src/NaderGorge.API/Controllers/AdminController.cs` mapping to the seeding command.

**Checkpoint**: All user stories should now be functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T010 [P] Verify frontend builds without type-errors using `npm run build` in `frontend/`.
- [ ] T011 [P] Verify backend compiels without errors using `dotnet build` in `backend/`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **User Stories (Phase 3+)**: Can proceed in priority order or concurrently if staffed. Let T002 complete before working on T003.

### Parallel Opportunities

- T005 and T008 can be executed in parallel initially as they don't block core logic and touch separate system edges. 

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Implement the server-side logic (T001).
2. **STOP and VALIDATE**: Test User Story 1 independently. Observe the Locked Reason reflecting the specific title securely.

### Incremental Delivery

1. Follow with Phase 4 (US2) to implement the circular navigation framework.
2. Advance to Phase 5 (US3) to replace the text timer with the visual countdown UI.
3. Finish with Phase 6 (US4) admin testing shortcuts to review everything end-to-end.
