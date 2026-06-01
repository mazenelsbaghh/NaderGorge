# Tasks: Google Drive Custom Player

**Input**: Design documents from `/specs/047-google-drive-custom-player/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

N/A - Built on existing infrastructure.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

N/A

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Watch Google Drive Videos with Custom Controls (Priority: P1) 🎯 MVP

**Goal**: Deliver the video safely through a 302 proxy allowing the `<video>` element to function natively with `PlayerControls.tsx`.

**Independent Test**: Student navigates to a drive video and controls it exactly like a Telegram video.

### Implementation for User Story 1

- [x] T001 [P] [US1] Create API Route `frontend/src/app/api/video/drive-proxy/route.ts` to decode the security token `t`, resolve it to a `fileId`, and return an HTTP `302 Redirect` to either `googleapis.com/drive` (if `GOOGLE_DRIVE_API_KEY` exists) or `drive.google.com/uc` fallback.
- [x] T002 [US1] Update `frontend/src/app/api/video/embed/route.ts` to remove the hardcoded `generateGoogleDriveEmbedHtml()` iframe wrapper, so `google_drive` is handled exactly like `telegram`, passing the token to `SecureVideoPlayer`.
- [x] T003 [US1] Update `frontend/src/components/video/SecureVideoPlayer.tsx` to remove the `isExternal` UI bypass for `google_drive`. Define its `<video>` source as `/api/video/drive-proxy?t=${token}`.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T004 Run manual tests to verify proxy functionality and IDM blocking works.

---

## Dependencies & Execution Order

### User Story Dependencies

- **User Story 1 (P1)**: Can start immediately.

### Within Each User Story

- T001 (Backend route) is independent and can be tested via browser directly.
- T002 (Embed routing) defines the environment for T003.
- T003 (Player rendering) completes the pipeline.

### Implementation Strategy

#### MVP First (User Story 1 Only)

1. Complete T001 → Test Drive Proxy routing.
2. Complete T002, T003 → Restore custom player components for Google Drive videos.
3. Test seamlessly.
