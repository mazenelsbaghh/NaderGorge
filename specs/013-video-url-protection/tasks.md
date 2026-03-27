# Tasks: Video URL Protection

**Input**: Design documents from `/specs/013-video-url-protection/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

## Phase 2: Foundational (Cleanup)

- [x] T001 Delete legacy insecure player at `frontend/src/components/content/VideoPlayer.tsx`
- [x] T002 Search codebase for any remaining imports of `VideoPlayer` and remove/replace with `SecureVideoPlayer` references

---

## Phase 3: User Story 1 — Student Cannot Extract Video URL (Priority: P1) 🎯 MVP

- [x] T003 [US1] Modify `initPlayer()` in `SecureVideoPlayer.tsx` to create a closed Shadow DOM and append wrapper into shadow root
- [x] T004 [US1] Strip iframe `src` attribute in `onReady` callback + freeze with `Object.defineProperty`
- [x] T005 [US1] Move overlay and cover elements into shadow root
- [x] T006 [US1] Re-target `applyDomShields()` to container host element (works outside shadow boundary)

---

## Phase 4: User Story 2 — Seamless Playback Experience (Priority: P2)

- [ ] T007 [US2] Verify PlayerControls interaction works through shadow root boundary
- [ ] T008 [US2] Verify fullscreen functionality targets outer container correctly
- [ ] T009 [US2] Verify watch count tracking fires correctly

---

## Phase 5: Polish

- [x] T010 [P] Add console.warn tamper detector in `dom-shield.ts`
- [ ] T011 Run quickstart.md validation in browser
