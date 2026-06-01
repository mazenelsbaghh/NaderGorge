---
description: "Task list for Video Carousel Navigation implementation"
---

# Tasks: Video Carousel Navigation

**Input**: Design documents from `/specs/027-video-carousel-navigation/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.
**Important Context**: The existing `feature-carousel.tsx` component is strictly coupled to the public registration/landing page. We will create a fresh, dynamic `LessonCarousel.tsx` in the student lesson module that leverages the same framer-motion concepts but accepts dynamic video arrays and uses `<img>` per the constitution.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Export `SPECIFY_FEATURE=027-video-carousel-navigation` in environment.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Architecture for dynamic lesson carousel mapping.

- [x] T002 Create a new `LessonCarousel` UI component strictly for lessons at `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel.tsx`. This file will serve as the base wrapper.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Animated Visual Video Navigation (Priority: P1) 🎯 MVP

**Goal**: Display all lesson videos in an animated carousel structure, completely distinct from the rigid static landing page carousel, utilizing Framer Motion for premium transitions.

**Independent Test**: Load a lesson with multiple videos. Verify the carousel renders with video thumbnails/gradients. Verify clicking a video animates the carousel visually.

### Implementation for User Story 1

- [x] T003 [US1] Scaffold the `LessonCarousel.tsx` to accept a dynamic `videos` array and `activeVideoId` state. Implement the `useNumberCycler` logic internally to track the selected step.
- [x] T004 [US1] Implement the dynamic `renderStepContent` inside `LessonCarousel.tsx` using `framer-motion` `<AnimatePresence>`. It should loop through the `videos` array instead of using a hardcoded switch statement. 
- [x] T005 [US1] Implement native `<img>` tag support (instead of `<Image>`) for video thumbnails within the carousel to strictly obey Constitution Principle XI (no Next.js Image component in scale animations). Apply `onError` fallbacks.
- [x] T006 [US1] Implement the `<Steps>` pagination dots mapping dynamically to the `videos.length` in `LessonCarousel.tsx`.
- [x] T007 [US1] Refactor `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/page.tsx` to import and render `<LessonCarousel>` below or beside the main video player, passing in the fetched `videos` list.
- [x] T008 [US1] Remove the old static video list mapping from `page.tsx` (if applicable) and fully replace it with the new layout logic holding the `LessonCarousel`.

**Checkpoint**: User Story 1 functional. The student can visually interact with the animated carousel.

---

## Phase 4: User Story 2 - Synchronization with Active Video Playback (Priority: P2)

**Goal**: Keep the carousel completely synchronized dynamically with the actual video playing on screen, even when the player auto-advances.

**Independent Test**: Let a video finish naturally. Ensure the player switches to the next video AND the carousel visually animates to the next step.

### Implementation for User Story 2

- [x] T009 [US2] Update `LessonCarousel` component to accept external `controlledStep` and `onStepChange` props from the parent `page.tsx`.
- [x] T010 [US2] In `page.tsx`, bind the active video state from the player (e.g., `currentVideoIndex`) to the `LessonCarousel`'s `controlledStep` prop.
- [x] T011 [US2] Ensure that when `LessonCarousel` fires `onStepChange`, it updates the parent's `activeVideoIndex` state, causing the video player to switch seamlessly.

**Checkpoint**: State syncing complete. External player auto-play dictates the carousel's UI state.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, responsivenes, and final aesthetic improvements.

- [x] T012 Verify responsive design: Carousel must not break viewport width on mobile (`<=375px`). Adjust Tailwind padding/margins in `LessonCarousel.tsx`.
- [x] T013 Verify animation performance (60fps) and lack of FOUC dropping.

---

## Dependencies & Execution Order

- **Phase 1 -> Phase 2 -> Phase 3 (US1)** -> **Phase 4 (US2)** -> **Phase 5**.
- US1 focuses on rendering the standalone interactive visual structure.
- US2 bridges the gap between the new visual structure and the active player.
