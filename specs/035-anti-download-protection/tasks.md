# Tasks: Anti-Download DRM & IDM Protection

## Phase 1: Setup
*No external DB tables or complex routing setup needed. Modifications apply directly to existing proxy API handlers.*

## Phase 2: Foundational (Backend Stream Protection)
**Goal:** Prevent IDM and other sniffers from successfully downloading the video outside of the native playback environment via HTTP header validation.
- [x] T001 Enforce tight `Sec-Fetch-Dest` validation strictly to `video`, and block direct tab access in `frontend/src/app/api/video/stream-proxy/route.ts`

## Phase 3: User Story 1 (DOM Obfuscation & IDM Overlay Prevention)
**Goal:** Destroy the `<video src>` immediately after the connection is initiated to blind IDM's DOM polling, and block visual overlay injections.
- [x] T002 [US1] Inject a Javascript event listener in `frontend/src/app/api/video/embed/route.ts` that fires on `loadstart` and forcefully calls `video.removeAttribute('src')` to wipe the DOM trace.
- [x] T003 [P] [US1] Add aggressive CSS isolation (`pointer-events: none`) to the video element and create an interactive overlay if necessary in `frontend/src/app/api/video/embed/route.ts` to defeat click-jacking downloads.

## Phase 4: User Story 2 (Stream Hijack Rejection & Direct Link Blocker)
**Goal:** Ensure the proxy outright rejects requests generated directly by download managers simulating the browser.
- [x] T004 [US2] Verify Referer header chains across both `embed/route.ts` and `stream-proxy/route.ts` to ensure the source is exclusively from valid internal dashboards.

## Phase 5: Polish & Cross-Cutting Concerns
- [x] T005 Verify playback stability across desktop and mobile devices after implementing the DOM destruction hook.

## Dependencies

*   T001 (Foundational) ➔ T004 (Integration checking)
*   T002 (DOM Obfuscation) ➔ T005 (Stability checking)

## Implementation Strategy
Start by deploying the Backend Header strictness (T001) as this is computationally the safest. Then inject the DOM-stripping logic (T002) and extensively monitor if the native browser media player interrupts playback. If successful, IDM will lose both DOM and Network vectors of attack.
