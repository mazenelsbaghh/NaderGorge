---
description: "Task list for Lesson Focus Mode implementation"
---

# Tasks: Lesson Focus Mode

**Input**: Design documents from `/specs/028-lesson-focus-mode/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure.

- [x] T001 Initialize the feature branch according to plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented.

- [x] T002 Create global layout store in `frontend/src/stores/lesson-focus-store.ts` to manage `isFocusMode` state using Zustand.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Enter Focus Mode (Priority: P1) 🎯 MVP

**Goal**: Hide the global navigation bar and sidebars seamlessly when opening a lesson.

**Independent Test**: Opening a lesson page should hide the main layout navigation leaving only the lesson content.

### Implementation for User Story 1

- [x] T003 [US1] Modify `frontend/src/components/layout/StudentShellChrome.tsx` to consume `lesson-focus-store` and wrap the `<aside>` and mobile `<nav>` with framer-motion `<AnimatePresence>` to slide them out of view when `isFocusMode` is true.
- [x] T004 [US1] Modify `frontend/src/components/layout/StudentShellChrome.tsx` to remove padding/margins (`lg:mr-24 px-5 py-8 pb-28`) dynamically from `<main>` when `isFocusMode` is true, allowing full-screen spread.
- [x] T005 [US1] Update `frontend/src/components/content/LessonViewer.tsx` to effectively set `isFocusMode(true)` on mount and `isFocusMode(false)` gracefully on unmount.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently.

---

## Phase 4: User Story 2 - Toggle Focus Elements (Priority: P2)

**Goal**: Provide a clear toggle switch within the viewer to bring back or hide the navigation menus.

**Independent Test**: Clicking the toggle button inside the lesson view should successfully animate the sidebars back in or out without navigating away.

### Implementation for User Story 2

- [x] T006 [US2] Implement a minimal, non-intrusive "Toggle Menu" or "Exit Focus" control element inside `frontend/src/components/content/LessonViewer.tsx`.
- [x] T007 [US2] Hook the new control to `toggleFocusMode()` from the `lesson-focus-store`.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T008 [P] Review `frontend/src/components/layout/StudentShellChrome.tsx` to ensure transitions preserve the Constitution's Premium Editorial Design System aesthetic (smooth spring animations, not jarring cuts).
- [x] T009 Run quickstart.md validation to ensure edge-cases like browser back buttons restore the layout state correctly.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: BLOCKS all user stories.
- **User Stories (Phase 3+)**: All depend on Foundational phase completion. User stories proceed sequentially in priority order (P1 → P2).
- **Polish (Final Phase)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2).
- **User Story 2 (P2)**: Can start after User Story 1 is stable and visually verifiable.

### Parallel Opportunities

- Polish review tasks can run anytime post US2 integration.
