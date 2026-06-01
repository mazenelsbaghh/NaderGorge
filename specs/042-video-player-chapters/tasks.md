---
description: "Task list for YouTube-like player chapters implementation"
---

# Tasks: YouTube-like player chapters

**Input**: Design documents from `/specs/042-video-player-chapters/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- exact file paths in descriptions ✅

## Path Conventions

- **Web app**: `frontend/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and validation

- [x] T001 Verify development environment and lesson video routing structure

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Ensure the `chapters` mapping explicitly trickles from `LessonCarousel` down to `PlayerControls` seamlessly in frontend/src/components/video/SecureVideoPlayer.tsx

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Segmented Progress Bar (Priority: P1) 🎯 MVP

**Goal**: As a student watching a lesson video, I want to see the progress bar divided into distinct segments representing chapters, so I can visually grasp the structure of the video and easily navigate between main topics.

**Independent Test**: Can be fully tested by playing a video with chapters; the progress bar should visually split at the exact timestamps of each chapter boundary.

### Implementation for User Story 1

- [x] T003 [P] [US1] Refine the segment rendering and gap calculation inside the timeline scrubber in frontend/src/components/video/InteractiveTimeline.tsx

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Chapter Hover Preview (Priority: P1)

**Goal**: As a student hovering over the video's progress bar, I want to see the title of the chapter I am hovering over, so I know what content to expect if I seek to that point.

**Independent Test**: Can be fully tested by mousing over different segments of the video progress bar and verifying the tooltip/overlay text matches the chapter title.

### Implementation for User Story 2

- [x] T004 [P] [US2] Review and finalize hover tooltip layout and bounding box calculation in frontend/src/components/video/InteractiveTimeline.tsx
- [x] T005 [P] [US2] Verify `computePercent` math seeks to exact clicked position within bounds in frontend/src/components/video/InteractiveTimeline.tsx

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Interactive Chapter Tracking (Priority: P2)

**Goal**: As a student, I want to see the current chapter name displayed persistently or semi-persistently in the player controls (e.g., next to the timestamp), so I always know which section I am currently studying.

**Independent Test**: Can be tested by seeking to different parts of the video and ensuring a UI element representing the "Current Chapter" updates accurately.

### Implementation for User Story 3

- [x] T006 [P] [US3] Add a derived state memo to calculate `activeChapter` based on `currentTime` parsing in frontend/src/components/video/PlayerControls.tsx
- [x] T007 [US3] Implement UI label beside the timestamp to render `activeChapter?.title` dynamically in frontend/src/components/video/PlayerControls.tsx

**Checkpoint**: All user stories should now be independently functional

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T008 Prevent framer-motion desync and layout shift inside control bar
- [x] T009 Review mobile-responsive touch interactions on the timeline in frontend/src/components/video/InteractiveTimeline.tsx
- [x] T010 Test edge cases (single chapter, missing chapter metadata) to verify gracefully degraded states.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User Story 1 and 2 modify the same component (`InteractiveTimeline.tsx`), so they should ideally be done by the same developer or sequentially avoiding merge conflicts.
  - User Story 3 can be done in parallel (`PlayerControls.tsx`).

### Parallel Opportunities

- T003, T004, and T005 can be combined or parallelized safely with T006/T007 since they touch distinct files.
