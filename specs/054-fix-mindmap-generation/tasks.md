---
description: "Task list for fixing mindmap generation UI and logic bugs"
---

# Tasks: Fix Mindmap Generation

**Input**: Design documents from `/specs/054-fix-mindmap-generation/`
**Prerequisites**: spec.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure.
*(No setup required for this bug fix feature)*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented
*(No foundational changes needed for this feature)*

---

## Phase 3: User Story 1 - Accurate State Representation (Priority: P1) 🎯 MVP

**Goal**: Ensure the AI generation status accurately reflects the actual outcome without contradictory UI elements (like showing Cancel/Retry options when finished).

**Independent Test**: Can be fully tested by triggering a mindmap generation task in the admin UI. Wait for completion and ensure no "X" (Cancel) button is visible alongside the success text.

### Implementation for User Story 1

- [x] T001 [P] [US1] Update UI logic to conditionally render action buttons based on `!isCompleted` in `frontend/src/components/admin/LessonVideoList.tsx`

---

## Phase 4: User Story 2 - Robust AI Generation Logic (Priority: P2)

**Goal**: Fix the backend logic for mindmap image generation by adopting diffusion-model optimal prompts, avoiding hallucinated Arabic text gibberish.

**Independent Test**: Evaluate the generated image visually to ensure it produces an abstract, aesthetically pleasing Pixar-style mindmap template without garbled text.

### Implementation for User Story 2

- [x] T002 [P] [US2] Refactor the systemic image generation prompt from procedural ChatGPT style to descriptive diffusion style in `worker/src/services/geminiService.ts`

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T003 Verify the generated mindmaps across existing videos in the dashboard.
- [x] T004 Test the "Cancel" webhook handling when job states transition from active to failed.

---

## Dependencies & Execution Order

### Phase Dependencies
- **User Stories (Phase 3+)**: Can begin immediately.

### User Story Dependencies
- **User Story 1 (P1)**: Independent.
- **User Story 2 (P2)**: Independent.

### Parallel Opportunities
- Both T001 and T002 have been implemented in parallel, as they affect separate layers of the stack (Frontend UI vs Background Worker).
