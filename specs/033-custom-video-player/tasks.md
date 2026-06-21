# Tasks: Custom Animated Video Player Controls

**Input**: Design documents from `/specs/033-custom-video-player/`
**Prerequisites**: plan.md ✅, spec.md ✅, data-model.md ✅, research.md ✅
**Status**: ✅ COMPLETE

---

## Phase 1: Setup (Shared Infrastructure)

- [x] T001 Verify `framer-motion ^12`, `lucide-react ^1.7`, and `@/components/ui/button` are importable in `frontend/src/components/video/PlayerControls.tsx`
- [x] T002 Run `npx tsc --noEmit` in `frontend/` to confirm current baseline compiles

---

## Phase 2: Foundational (Blocking Prerequisites)

- [x] T003 Rewrite `CustomSlider` in `frontend/src/components/video/PlayerControls.tsx` to add drag support: add `isDragging` ref, `onMouseDown` handler on the container div that sets `isDragging = true` and attaches `mousemove`/`mouseup` listeners to `document`, and cleanup on `mouseup`
- [x] T004 Verify `CustomSlider` drag: clicking seeks correctly; dragging outside the slider bounds still clamps to `[0, 100]`; releasing mouse anywhere stops the drag

**Checkpoint**: ✅ `CustomSlider` supports click + drag

---

## Phase 3: User Story 1 — Animated Floating Controls on Hover (Priority: P1) 🎯 MVP

- [x] T005 [US1] Wrap pill in `AnimatePresence` with `initial={{ y: 20, opacity: 0, filter: "blur(10px)" }}` / `animate` / `exit` in `frontend/src/components/video/PlayerControls.tsx`
- [x] T006 [US1] Set pill wrapper classes: `absolute bottom-0 mx-auto max-w-xl left-0 right-0 p-4 m-2 bg-[#11111198] backdrop-blur-md rounded-2xl z-30` in `frontend/src/components/video/PlayerControls.tsx`
- [x] T007 [US1] Confirm `SecureVideoPlayer.tsx` `showControls` logic is correct — no changes needed
- [x] T008 [US1] Run `npx tsc --noEmit` — zero errors

**Checkpoint**: ✅ Hover → pill animates in. Mouse leave → animates out.

---

## Phase 4: User Story 2 — Draggable Progress & Volume Sliders (Priority: P2)

- [x] T009 [US2] Wire progress slider `value={progress}`, `onChange={onSeek}`, `className="flex-1"` in `frontend/src/components/video/PlayerControls.tsx`
- [x] T010 [US2] Wire volume slider with auto-unmute on drag in `frontend/src/components/video/PlayerControls.tsx`
- [x] T011 [P] [US2] Add `volumeForSlider` and `volumeNormalised` derived values in `frontend/src/components/video/PlayerControls.tsx`
- [x] T012 [US2] Verify dragging volume while muted calls `onToggleMute()` to restore sound

**Checkpoint**: ✅ Both sliders fully draggable with out-of-bounds clamping.

---

## Phase 5: User Story 3 — Full-Screen Pause Blur Overlay with Play Button (Priority: P3)

- [x] T013 [US3] Replace `h-[40%]` bottom blur with `motion.div` using `className="absolute inset-0 bg-black/50 backdrop-blur-[14px] z-10"` in `frontend/src/components/video/SecureVideoPlayer.tsx`
- [x] T014 [US3] Add centred `motion.button` with gold play circle (`w-20 h-20`) calling `togglePlay` in `frontend/src/components/video/SecureVideoPlayer.tsx`
- [x] T015 [US3] Add `تم الإيقاف` label below play button in `frontend/src/components/video/SecureVideoPlayer.tsx`
- [x] T016 [US3] Verify z-index: overlay `z-10` < controls pill `z-30` in `frontend/src/components/video/SecureVideoPlayer.tsx`
- [x] T017 [US3] Import `motion` from `framer-motion` in `frontend/src/components/video/SecureVideoPlayer.tsx`
- [x] T018 [US3] Run `npx tsc --noEmit` — zero new errors

**Checkpoint**: ✅ Full-screen pause overlay functional. Controls pill appears above on hover.

---

## Phase 6: User Story 4 — Speed Chips & Quality Settings (Priority: P4)

- [x] T019 [P] [US4] Render speed chips with `playbackSpeed === speed && "bg-[#111111d1]"` active highlight in `frontend/src/components/video/PlayerControls.tsx`
- [x] T020 [US4] Add `settingsOpen` state and gear `Button` with `AnimatePresence` quality popover in `frontend/src/components/video/PlayerControls.tsx`
- [x] T021 [US4] Populate quality popover with 7 options calling `onQualityChange` in `frontend/src/components/video/PlayerControls.tsx`
- [x] T022 [P] [US4] Add fullscreen `Button` calling `onToggleFullscreen` in `frontend/src/components/video/PlayerControls.tsx`

**Checkpoint**: ✅ All controls functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T023 [P] `npx tsc --noEmit` — zero errors ✅
- [x] T024 `npx eslint src/components/video/PlayerControls.tsx` — zero errors ✅
- [x] T025 [P] Manual smoke test checklist: hover ✅ | drag progress ✅ | drag volume ✅ | pause overlay ✅ | speed chips ✅ | quality popover ✅ | fullscreen ✅
- [x] T026 Update `specs/033-custom-video-player/checklists/requirements.md` — all items passing ✅

## Phase 8: Admin Player Appearance Preview

- [x] T027 [US5] Add a dedicated player-preview tab and provider/video identifier controls to `frontend/src/app/admin/settings/AdminSettingsPageClient.tsx`.
- [x] T028 [US5] Add live top/bottom shadow opacity and provider-specific hide-delay controls with persisted platform-setting keys.
- [x] T029 [US5] Add the server-side preview redirect route in `frontend/src/app/api/video/preview/route.ts` so previewing does not consume student watch sessions.
- [x] T030 [US5] Expose sanitized player appearance settings from the public settings endpoint and load them in `SecureVideoPlayer.tsx`.
- [x] T031 [US5] Verify targeted ESLint, TypeScript strict compilation, and backend build.

---

## Dependencies & Execution Order

All phases complete. Implementation delivered in full.

---

## Implementation Strategy

✅ MVP (US1 + US2) delivered  
✅ Full delivery (US3 + US4) delivered  
✅ Polish complete
