# Tasks: VK Custom YouTube-like Video Player

**Input**: Design documents from `/specs/052-vk-custom-player/`
**Branch**: `052-vk-custom-player`
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | quickstart.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

## Path Conventions

- Frontend: `frontend/src/`
- Backend: `backend/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add VK provider option to admin UI so videos can be configured. No code changes are blocking on this — it runs immediately.

- [x] T001 [P] Add `"vk"` option to provider dropdown in `frontend/src/components/admin/AddVideoForm.tsx` with label "VK (فيكونتاكتي)"
- [x] T002 [P] Update the `urlOrEmbedCode` auto-detection logic in `frontend/src/components/admin/AddVideoForm.tsx` to detect `vk.com/video` URLs and automatically set provider to `"vk"`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The VK embed HTML generator must exist before player behaviour can be implemented. `SecureVideoPlayer` gets the `vk` provider name from the backend session token and sets the iframe `src`. The new HTML for VK must be served from `/api/video/embed`.

**⚠️ CRITICAL**: US1 (playback controls) and US2 (state sync) depend on this output.

- [x] T003 Add a new function `generateVkEmbedHtml(oid: string, videoId: string, studentName: string, studentPhone: string): string` inside `frontend/src/app/api/video/embed/route.ts`. The function must:
  1. Build a full `<!DOCTYPE html>` page
  2. Embed `<iframe id="vk-player" src="https://vk.com/video_ext.php?oid={oid}&id={videoId}&hd=2&js_api=1" allow="autoplay; encrypted-media; fullscreen; picture-in-picture" frameborder="0" allowfullscreen></iframe>` — **styled** `position:absolute; top:0; left:0; width:100%; height:100%; border:none; pointer-events:none;`
  3. Load `<script src="https://vk.com/js/api/videoplayer.js"></script>`
  4. Include an invisible click-overlay `<div id="click-overlay">` that fills the player area (`position:absolute; inset:0; z-index:10; background:transparent; cursor:pointer`)
  5. Include a watermark `<div id="video-watermark">` with float/random positioning (same pattern as YouTube embed: `position:absolute; top:10%; left:10%; z-index:99; pointer-events:none; color:rgba(255,255,255,0.2); font-size:1.5rem;`) populated with `studentName` and `studentPhone`
  6. Initialize `VK.VideoPlayer` on the iframe element after the SDK is ready
  7. Forward events to the parent via `window.parent.postMessage({ source: 'video-embed', type, data }, '*')` for: `ready` (with `{duration, volume, isMuted, provider:'vk'}`), `stateChange` (with `{isPlaying: bool}`), `timeUpdate` (with `{currentTime, duration}`), `error`
  8. Listen for incoming `postMessage` commands from the parent: `play` → `player.play()`, `pause` → `player.pause()`, `seekTo` → `player.seek(time)`, `setVolume` → `player.setVolume(vol/100)`, `mute` → `player.setMuted(true)`, `unmute` → `player.setMuted(false)`, `setPlaybackRate` → (no-op or call API if supported)

- [x] T004 Update the `GET` handler in `frontend/src/app/api/video/embed/route.ts` to parse the `ProviderVideoId` for VK videos. VK stores the ID as `oid=-XXXXXXXX&id=XXXXXXXXX`. When `provider === 'vk'`, split the stored `ProviderVideoId` string to extract `oid` and `videoId`, then call `generateVkEmbedHtml(oid, videoId, studentName, studentPhone)` and return the result (with the same security headers as the YouTube branch).

- [x] T005 Update `SecureVideoPlayer.tsx` in `frontend/src/components/video/SecureVideoPlayer.tsx` to recognise the `vk` provider. In the `loadVideo()` function, add a new `else if` branch for `provider === 'vk'` that:
  1. Sets `provider('vk')` state
  2. Creates an `<iframe>` with `src = /api/video/embed?t=...&k=...` (exactly the same construction as the YouTube branch)
  3. Appends the iframe to `containerRef.current`
  4. Stores the ref in `iframeRef.current`
  5. Applies `applyDomShields` to `containerRef.current`
  (No changes needed to `sendCommand` or `postMessage` listener since we use the same protocol.)

- [x] T006 Update the `sendCommand` helper in `frontend/src/components/video/SecureVideoPlayer.tsx` so that `vk` is treated identically to `youtube` (i.e., it routes through `iframeRef.current.contentWindow.postMessage`). Confirm the current `else` branch (non-telegram) already covers this; add `&& provider !== 'vk'` guard to the telegram-specific branch if needed.

**Checkpoint**: After T003–T006, a VK video can be loaded in the player and will show the custom controls overlaying the VK iframe.

---

## Phase 3: User Story 1 — Core Playback Controls (Priority: P1) 🎯 MVP

**Goal**: Student sees a branded player with full custom controls (play, pause, seek, volume, fullscreen, speed) over a VK-hosted video with no visible VK social controls.

**Independent Test**: Set a lesson's video provider to `"vk"` with a valid `oid` and `id`. Open the lesson page — the custom `PlayerControls` bar should appear over the VK video. All controls should fire the correct `postMessage` commands and the VK player should respond.

- [x] T007 [US1] Verify the `click-overlay` div in `generateVkEmbedHtml` (T003) correctly intercepts clicks on the VK iframe and routes them to `player.play()` / `player.pause()`. The click-overlay `click` event listener must: check current player state and toggle playback, then `postToParent('stateChange', { isPlaying: … })`.

- [x] T008 [US1] Add watermark roaming animation via `setInterval` inside `generateVkEmbedHtml` (introduced in T003) — fire every 12 seconds, randomize `top` (10–90%) and `left` (10–90%), same logic as the YouTube embed for visual protection consistency.

- [x] T009 [US1] Verify the VK `video_ext.php` URL format in T003 by reading `oid` and `videoId` from the parsed session token; confirm the embed URL: `https://vk.com/video_ext.php?oid={oid}&id={videoId}&hd=2&js_api=1`; add an early-return error if either value is missing/invalid.

- [x] T010 [P] [US1] Add `"vk"` to the provider options in `frontend/src/components/admin/AddVideoForm.tsx` dropdown if not already done in T001. The admin input label should read `"VK — oid=-XXXXX&id=XXXXX"` as the placeholder in the URL field to guide proper entry format.

- [x] T011 [P] [US1] Update the `VideoProviderAbstraction` on the backend: create `backend/src/NaderGorge.Infrastructure/Providers/VkVideoProvider.cs` implementing `IVideoProvider`. `Name` = `"vk"`. `ExtractVideoId` returns the raw query-string portion (e.g. `oid=-22822305&id=456241864`) and `GetEmbedUrl` returns the full `video_ext.php?...&js_api=1` URL.

- [x] T012 [US1] Register `VkVideoProvider` in the backend DI container in `backend/src/NaderGorge.Infrastructure/DependencyInjection.cs` (or wherever `YouTubeVideoProvider` and `TelegramVideoProvider` are registered).

**Checkpoint**: All custom controls respond within 300 ms, no VK branding is visible in the player chrome.

---

## Phase 4: User Story 2 — Playback State Synchronization (Priority: P2)

**Goal**: Seek bar, time counter, buffer indicator, volume icon, and ended state all remain synchronized with the actual VK playback position in real-time.

**Independent Test**: Play a VK-hosted video to 30 s, pause, then resume — confirm the seek bar and time counter remain accurate (within ±1 s). Mute the player and confirm the volume icon updates. Skip to the end and verify the ended state renders a replay option.

- [x] T013 [US2] Wire up the VK player's progress event(s) inside `generateVkEmbedHtml`. Use `setInterval` (500 ms) to poll `player.getCurrentTime()` and `player.getDuration()` (or equivalent VK API methods) and call `postToParent('timeUpdate', { currentTime, duration })`. Start the interval in the `loaded` / `ready` callback.

- [x] T014 [US2] Map VK `VK.VideoPlayer` state-change events (the API fires a `buffered`, `playing`, `paused`, `ended` equivalents — attach via the undocumented but standard event binding: `player.on('timeupdate', ...)` if available, else rely on polling from T013) in `generateVkEmbedHtml` to `postToParent('stateChange', { isPlaying })`.

- [x] T015 [US2] Handle the `ended` event inside `generateVkEmbedHtml`: when the VK player reaches end of video, `postToParent('stateChange', { isPlaying: false })` so `SecureVideoPlayer` can surface the replay UI (the existing `isPlaying = false` state already reveals the play overlay; no additional React changes needed).

- [x] T016 [P] [US2] Emit `ready` event with accurate `isMuted` and `volume` values from the VK player inside `generateVkEmbedHtml`. Call `player.getVolume()` during `ready` callback (0–1 scale from VK) and multiply by 100 to match our 0–100 convention before posting.

**Checkpoint**: Controls stay in sync, time tracking persists reliably, and the `onWatchProgress` callback in `SecureVideoPlayer` fires correctly for watch-time tracking.

---

## Phase 5: User Story 3 — Chapter Panel Integration (Priority: P3)

**Goal**: Clicking a chapter in `ChapterList` calls `seekTo` on the VK player and the video jumps to the correct timestamp; the active chapter indicator updates.

**Independent Test**: Load a VK lesson with AI-generated chapters. Click each chapter entry; verify the VK player seeks within ±2 s of the chapter's `startTimeSeconds`.

- [x] T017 [US3] Verify `SecureVideoPlayer`'s `ref`-exposed `seekTo(seconds)` method routes correctly for VK via `sendCommand('seekTo', { seconds })` now that `vk` is an iframe-based provider (ensured by T006). No separate code is needed if T006 already makes this work.

- [x] T018 [US3] Confirm that `frontend/src/components/video/ChapterList.tsx` calls `seekTo` on the `SecureVideoPlayerRef` and that the call resolves correctly for VK-hosted videos. Trace the call from `ChapterList` → `SecureVideoPlayer ref` → `sendCommand('seekTo')` → `postMessage` → `embed iframe` → `player.seek(time)` (in `generateVkEmbedHtml` listener). Fix any gaps in the chain.

- [x] T019 [P] [US3] Add chapter-active-state tracking inside `SecureVideoPlayer.tsx`: on each `timeUpdate` message, compare `currentTime` against the `chapters` prop timestamps to determine the currently active chapter, and call `onChapterChange` (if such a prop/callback exists) or expose via the ref. If the chapter panel already handles this from `currentTime`, no change is needed — just verify.

**Checkpoint**: Chapter navigation works end-to-end for VK-hosted videos with sub-2-second accuracy.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Fallback, edge-case handling, and design system compliance.

- [x] T020 [P] Add an error fallback path in `generateVkEmbedHtml`: if `VK.VideoPlayer` fails to initialize (API error, SDK load failure, or timeout), post `{ source: 'video-embed', type: 'error', data: { code: 'VK_INIT_FAILED' } }` to the parent so `SecureVideoPlayer` falls back to its `'error'` render state (which shows the "حاول مرة أخرى" retry button).

- [x] T021 [P] Handle the edge case in T004 where `ProviderVideoId` for VK is missing `oid` or `id` — return `NextResponse` with status `400` and a clear message: `"Invalid VK video identifier format. Expected: oid=-XXXXX&id=XXXXX"`.

- [x] T022 Add `context menu` and `drag` blocking to the VK embed HTML (same `oncontextmenu`, `ondragstart`, `onselectstart` on `<body>` as in the YouTube embed) in `generateVkEmbedHtml` inside `frontend/src/app/api/video/embed/route.ts`.

- [x] T023 [P] Manual smoke-test using the quickstart.md checklist: open a lesson with a VK video, verify all controls respond within 300 ms, verify no VK branding visible, verify chapter seek accuracy, verify watch-time tracking fires correctly (check server receives `trackProgress` calls).

- [x] T024 [P] Run `npm run lint` in `frontend/` to confirm no TypeScript strict-mode violations from the new code paths. Fix any issues.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — T001, T002 can start immediately
- **Foundational (Phase 2)**: Depends on Setup; BLOCKS US1, US2, US3 — T003→T004→T005→T006
- **US1 (Phase 3)**: Depends on Foundational — T007 onward
- **US2 (Phase 4)**: Depends on Foundational; best started after US1 (T013 extends T003's polling setup)
- **US3 (Phase 5)**: Depends on Foundational; best started after US1 (confirms seekTo works)
- **Polish (Phase 6)**: Depends on all user stories complete

### Within Each Phase (Sequential where noted)

```
T003 (generateVkEmbedHtml) ──┬─→ T004 (route handler)
                              └─→ T007, T008, T009 (US1 control polish)
T004 ──→ T005 (SecureVideoPlayer branch)
T005 ──→ T006 (sendCommand guard)
T006 ──→ T013 (US2 sync), T017 (US3 seek)
```

### Parallel Opportunities

- T001 and T002 (admin form) run in parallel with all other phases
- T007, T008, T009, T010 all target different aspects of the embed HTML or admin form — parallelizable within Phase 3
- T013, T014, T015, T016 all operate in `generateVkEmbedHtml` — sequential within Phase 4 (same function body)
- T020, T021, T022, T024 are independent polish items — parallelizable

---

## Parallel Example: User Story 1

```bash
# After T003–T006 complete, these can run simultaneously:
Task T007: Wire click-overlay toggle in generateVkEmbedHtml
Task T008: Add watermark roaming animation in generateVkEmbedHtml
Task T009: Validate oid/videoId extraction logic
Task T010: Admin form VK placeholder text
Task T011: VkVideoProvider.cs backend class
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: T001, T002 (admin UI)
2. Complete Phase 2: T003 → T004 → T005 → T006 (embed routing + player branch)
3. Complete Phase 3: T007–T012 (controls work end-to-end)
4. **STOP and VALIDATE**: Load a VK video and confirm all custom controls fire correctly
5. Demo to stakeholder — fully watchable, branded VK player

### Incremental Delivery

1. MVP → Functional VK playback with controls (Phase 1–3)
2. Add US2 state sync → Accurate seek/time/mute display (Phase 4)
3. Add US3 chapter nav → Full VK parity with YouTube/Telegram (Phase 5)
4. Polish (Phase 6) → Production-ready

---

## Notes

- [P] tasks = different files or non-conflicting code sections, can run in parallel
- VK's JS API uses `player.seek(seconds)` not `seekTo` — double-check against VK documentation during T003
- VK `setVolume` accepts 0.0–1.0 range; our UI uses 0–100; conversion must happen in the embed HTML
- `applyDomShields` is called once per container — already handled in T005; do NOT call it again from within the iframe HTML
- The `postMessage` source must always be `'video-embed'` for `SecureVideoPlayer`'s message listener to accept it
- Commit after each phase checkpoint to enable easy rollback if VK API behaves unexpectedly
