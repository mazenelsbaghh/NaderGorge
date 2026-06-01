---
description: "Task list for Cancel AI Analysis and Provider Handling"
---

# Tasks: Cancel AI Analysis and Provider Handling

**Input**: Design documents from `/specs/038-cancel-ai-analysis/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- No shared infrastructure changes needed for this feature.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T001 [P] Ensure `youtube-dl-exec` is installed in the worker package (`worker/package.json`).

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Graceful Job Cancellation (Priority: P1) 🎯 MVP

**Goal**: Allow administrators to cancel an ongoing AI video analysis job from the dashboard so that they can stop a stuck or unnecessary process without waiting for it to fail or finish.

**Independent Test**: Can be fully tested by triggering an AI job and clicking "Cancel", verifying the job is removed from the queue and the UI reverts to idle state.

### Implementation for User Story 1

- [x] T002 [US1] Expose `DELETE /api/status/:id` to delete/abort jobs residing in `bull:ai-video-chapters` in `worker/src/index.ts`.
- [x] T003 [P] [US1] Create `CancelAnalyzeVideoAICommand.cs` in `backend/src/NaderGorge.Application` to toggle `IsProcessingAI` back to false gracefully.
- [x] T004 [US1] Integrate the cancellation flow into the frontend UI `frontend/src/components/admin/LessonVideoList.tsx` rendering a Cancel button within the `AIProgressTracker`.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently.

---

## Phase 4: User Story 2 - Automated URL Normalization for Third-Party Providers (Priority: P2)

**Goal**: Automatically handle videos hosted on YouTube (which only provide an ID) and Telegram (which provide an embed URL) when extracting audio.

**Independent Test**: Can be fully tested by triggering an AI job on a YouTube video (providing the video ID) and confirming the audio extraction successfully initiates.

### Implementation for User Story 2

- [x] T005 [US2] Update `worker/src/utils/audioExtractor.ts` to replace `fluent-ffmpeg` download logic with `yt-dlp` using `youtube-dl-exec`.
- [x] T006 [US2] Modify `audioExtractor.ts` to identify 11-char strings as YouTube IDs and automatically prepend `https://www.youtube.com/watch?v=`.
- [x] T007 [US2] Validate that `ytDlp` configuration handles telegram generic embeds gracefully without hard-crashing like FFMPEG.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T008 [P] Documentation updates in docs/ if needed.
- [ ] T009 Run quickstart.md validation locally.

---

## Dependencies & Execution Order

### User Story Dependencies

- **User Story 1 (P1)**: Can start immediately.
- **User Story 2 (P2)**: Can start immediately in parallel with US1.

### Parallel Opportunities

- T002, T003, and T005 can be implemented by different developers strictly in parallel since they touch entirely disconnected domain zones (.NET, Worker APIs, Worker utilities).
