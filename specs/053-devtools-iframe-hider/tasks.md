# Implementation Tasks: devtools-iframe-hider

## Implementation Strategy
- **MVP**: Directly modify the `setInterval` watchers in `route.ts` to implement removing and recreating the `iframe` based on DevTools visibility.
- **Incremental Delivery**: 
  - Update YouTube visibility flow.
  - Update VK visibility flow.

## Dependencies & Execution Order
1. **US1 (DevTools Hide/Restore)**: Independent.

## Phase 1: Setup
No foundational setup required.

## Phase 2: [US1] DevTools Iframe Toggle
*Goal: Remove the video iframe element from the DOM entirely when DevTools open, and inject a fresh interactive iframe appropriately when DevTools close.*
*Independent Test Criteria: Open the browser's developer tools on an embed video. The video should immediately vanish and the `iframe` node should disappear from the Elements tab. Close developer tools, and the video should reappear and resume working.*

- [x] T001 [P] [US1] Modify YouTube DevTools watcher logic to `.remove()` the `iframe` node on open, and re-invoke `new YT.Player(...)` inside `ytDiv` on close in `frontend/src/app/api/video/embed/route.ts`
- [x] T002 [P] [US1] Modify VK DevTools watcher logic to `.remove()` the `iframe` from the DOM on open, and dynamically create, append, and instantiate `new VK.VideoPlayer(...)` on close in `frontend/src/app/api/video/embed/route.ts`

## Phase 3: Polish
- [x] T003 Verify `dom-shield` and injected watermarks are unaffected by the DOM manipulation cycle in `frontend/src/app/api/video/embed/route.ts`.
