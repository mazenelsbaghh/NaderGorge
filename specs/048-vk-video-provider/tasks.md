# Implementation Tasks: VK.com Video Provider

**Feature**: `048-vk-video-provider`

## Phase 1: Setup
-[x] T001 Remove dead Proxy routes: Delete `frontend/src/app/api/video/drive-proxy/route.ts`
-[x] T002 Remove any legacy Telegram proxy endpoints if present in Nginx or backend configurations

## Phase 2: Foundational
-[x] T003 Update Backend Mock Enum: Modify `backend/src/NaderGorge.Shared/Enums/LessonVideoProvider.cs` (or equivalent location) replacing `telegram` and `google_drive` with `vk`

## Phase 3: Watch VK.com Videos with Custom Player [US1]
**Goal**: Integrate VK Javascript Iframe bridge supporting play, pause, progress tracking, and removing native UI overlaps.
**Independent Test Criteria**: A VK embed loaded via `embed/route.ts` is accurately tracked by `PlayerControls.tsx` within the Secure environment without proxy intervention.
-[x] T004 [US1] Refactor `frontend/src/app/api/video/embed/route.ts` to output a VK `<iframe>` specifically wired with `window.postMessage` bridge payloads for `vk` provider instead of YouTube or the custom native `<video>` approach.
-[x] T005 [P] [US1] Refactor `frontend/src/components/video/SecureVideoPlayer.tsx` to listen on incoming `window.addEventListener('message')` hooks for VK-specific payload schemas (e.g. `event === 'timeupdate'`) to update `currentTime`.

## Phase 4: Remove Google Drive Video Proxy Mechanisms [US2]
**Goal**: Complete elimination of Google Drive specific paths.
**Independent Test Criteria**: No DB entity accepts `google_drive`, and no custom `<video>` drive payload handlers exist.
-[x] T006 [US2] Remove all Google Drive handler conditions (e.g., `generateGoogleDrivePlayerWrapper()` or custom bypass UI logic) from `frontend/src/app/api/video/embed/route.ts`
-[x] T007 [US2] Remove `backend/src/NaderGorge.Infrastructure/Providers/GoogleDriveVideoProvider.cs`

## Phase 5: Remove Telegram Local Proxy Mechanisms [US3]
**Goal**: Complete elimination of Telegram Video parsing logic.
**Independent Test Criteria**: No DB entity accepts `telegram` and infrastructure is cleaner.
-[x] T008 [US3] Remove `backend/src/NaderGorge.Infrastructure/Providers/TelegramVideoProvider.cs`
-[x] T009 [US3] Remove all Telegram handler conditions (`generateTelegramPlayerWrapper()` or custom player logic) from `frontend/src/app/api/video/embed/route.ts`

## Phase 6: Polish
-[x] T010 Final visual pass inside `SecureVideoPlayer.tsx` to adjust Z-Indexes ensuring the VK Iframe does not trap mouse interactions unexpectedly unless intended.
-[x] T011 Test end-to-end integration with a sample VK link hash to ensure Time Updates strictly fire across origins.

## Execution Strategy
- Tasks T001 through T003 are blocking refactors prioritizing technical debt reduction.
- Tasks T004 and T005 construct the essential MVP (User Story 1's goal).
- Tasks T006+ execute cleanup required across all remaining references to the deprecated systems.
