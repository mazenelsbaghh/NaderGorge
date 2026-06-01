---
description: "Task list for Telegram Large Media Fix"
---

# Tasks: Telegram Large Media Fix

**Input**: Design documents from `/specs/045-telegram-large-media/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 [P] Ensure local testing environment and Next.js development server are ready

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Locate and trace the existing Telegram embed flow in `frontend/src/app/api/video/embed/route.ts`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Reliable Playback of Large Videos (Priority: P1) 🎯 MVP

**Goal**: As a student, I want to be able to play video lessons hosted on Telegram regardless of their file size, so that my learning process is not interrupted by technical failures.

**Independent Test**: Can be fully tested by uploading a known large video (e.g., > 100MB) to Telegram, embedding it in a lesson, and successfully streaming it through the `/api/video/embed` endpoint without encountering a 404 timeout.

### Implementation for User Story 1

- [x] T003 [US1] Remove the `encryptProxyUrl` wrapping for `videoSrc` within `generateTelegramEmbedHtml` in `frontend/src/app/api/video/embed/route.ts`
- [x] T004 [US1] Pass the extracted direct `videoSrc` directly into the `generateTelegramPlayerWrapper` function call in `frontend/src/app/api/video/embed/route.ts`
- [x] T005 [US1] Ensure the `generateTelegramPlayerWrapper` function safely accepts and injects the raw URL into the `video.src` property in `frontend/src/app/api/video/embed/route.ts`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Large videos will stream.

---

## Phase 4: User Story 2 - Graceful Error Handling for Unsupported Media (Priority: P2)

**Goal**: As a user, if a video is fundamentally corrupted or impossible to load due to constraints we cannot bypass, I want to see a clear error message explaining the issue, so I don't wait indefinitely or see cryptic 404 pages.

**Independent Test**: Can be fully tested by purposely trying to load a broken/missing video ID and verifying the UI shows a friendly error message rather than breaking the page.

### Implementation for User Story 2

- [x] T006 [P] [US2] Verify `generateTelegramEmbedHtml` correctly identifies HTTP non-200 responses and falls back to `generateEmbedErrorHtml` with a localized message in `frontend/src/app/api/video/embed/route.ts`
- [x] T007 [P] [US2] Improve validation: If `videoSrc` is undefined (because the Web message payload indicates a file size limit or simply misses the `<video>` tag), immediately return `generateEmbedErrorHtml` explaining the unavailability in `frontend/src/app/api/video/embed/route.ts`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T008 [P] Remove unused functions or code blocks (like `encryptProxyUrl` if no longer used) specifically in `frontend/src/app/api/video/embed/route.ts` to reduce clutter.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed sequentially in priority order (P1 → P2)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Expands upon US1 error state handling.

### Parallel Opportunities

- All tasks marked [P] can run in parallel (e.g., T001, T006, T007, T008).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2: Setup & Foundations
2. Complete Phase 3: User Story 1
3. **STOP and VALIDATE**: Test User Story 1 independently by playing a large Telegram file.
4. Deploy/demo if ready

### Incremental Delivery

1. Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Final Polish
