# Implementation Tasks: Rutube Video Provider

**Feature**: `049-rutube-video-provider`

## Phase 1: Setup

*No distinct setup tasks, structural folders already exist.*

## Phase 2: Foundational

- [x] T001 Update Backend `LessonVideoProvider` string usages replacing `vk` with `rutube` where hardcoded or inferred.
- [x] T002 Replace `VkVideoProvider` with `RutubeVideoProvider` in `backend/src/NaderGorge.Infrastructure/Providers/` to handle embed URL formatting if needed.
- [x] T003 Ensure dependency injection container (`backend/src/NaderGorge.API/Program.cs`) injects `RutubeVideoProvider` instead of VK.

## Phase 3: Watch Course Videos via Rutube [US1]

**Goal**: Students watch Rutube videos inside SecureVideoPlayer seamlessly with progress tracking.
**Independent Test Criteria**: A lesson containing a Rutube video plays properly, hiding Russian UI, and correctly advancing the progression tracker.

- [x] T004 [US1] Rewrite `frontend/src/app/api/video/embed/route.ts` to output a `rutube` Iframe format, including JS event translation bridge for `player:currentTime`, `player:changeState` to `video-embed-progress`.
- [x] T005 [P] [US1] Ensure `frontend/src/components/video/SecureVideoPlayer.tsx` correctly dispatches play/pause/seek events as `{"type":"player:play","data":{}}` specifically when iframe format dictates it.

## Phase 4: Add/Manage Rutube Videos in Admin Panel [US2]

**Goal**: Staff can easily add Rutube.ru links natively.
**Independent Test Criteria**: The content form recognizes Rutube links, selects the Rutube provider option, and saves properly to Postgres.

- [x] T006 [US2] Update `frontend/src/components/admin/AddVideoForm.tsx` to include `rutube` as an option and parse links automatically.
- [x] T007 [US2] Update `frontend/src/app/admin/content/page.tsx` states to handle `rutube` replacing `vk`.

## Phase 5: Polish & Cross-Cutting Concerns

- [x] T008 Cleanup any remaining references to `vk.com` in `frontend/src/components/video/PlayerControls.tsx` mapping.
- [x] T009 Validate z-index protection on the Rutube player to prevent right-clicking and branding leaks.

## Implementation Strategy

1. Work foundationally to swap backend logic.
2. The core of this migration lies in `T004` and `T005`, building the secure javascript bridge for Rutube in MVP.
3. Finish admin interface mapping and conduct a full E2E test.
