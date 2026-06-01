# Research: Custom Animated Video Player Controls

**Branch**: `033-custom-video-player`  
**Phase**: 0 — Outline & Research  
**Date**: 2026-03-31

## Research Questions

No NEEDS CLARIFICATION markers were present in the spec. Research focused on:

1. framer-motion v12 API compatibility for spring animations and AnimatePresence
2. PlayerControlsProps interface preservation requirements
3. Design token alignment with the "Curated Archive" design system

---

## Decision 1: Animation Strategy

**Decision**: Use `framer-motion` `AnimatePresence` + `motion.div` with `type: "spring"` on the slider fill, and standard ease transitions on the container overlay.

**Rationale**: The constitution mandates framer-motion as the sole animation library (§Tech Stack). `AnimatePresence` is needed to cleanly unmount the control bar on exit with a reverse blur+slide animation. Spring transitions on sliders give a physical "snap" feel without timing fatigue.

**Alternatives considered**:
- CSS `transition` only — rejected; cannot achieve exit animations or spring physics without JS
- `@formkit/auto-animate` — rejected; not in the stack, overkill for a single component

---

## Decision 2: Slider Implementation

**Decision**: Implement a custom `CustomSlider` component using a `motion.div` fill with `animate={{ width: \`${value}%\` }}` and `transition={{ type: "spring", stiffness: 300, damping: 30 }}`, rather than native `<input type="range">`.

**Rationale**: The native `<input type="range">` cannot be styled to the pill/minimalist aesthetic without browser-specific hacks. The custom div slider matches the exact visual spec provided by the user and stays consistent with the rest of the Tailwind-based design system.

**Alternatives considered**:
- Native range input with `accent-color` styling — used in old `PlayerControls.tsx`; rejected for this redesign due to inconsistent cross-browser theming
- Radix UI Slider — available via shadcn but adds a new primitive dependency; rejected in favor of lightweight custom implementation

---

## Decision 3: Playback Speed Placement

**Decision**: Speed buttons (0.5x, 1x, 1.5x, 2x) rendered as inline chip buttons directly in the control bar right section, replacing the previous settings-menu sub-list for speed selection.

**Rationale**: The user's provided component explicitly shows speed chips inline. This matches the spec (FR-007) and reduces interaction steps — no need to open a menu to change speed.

**Alternatives considered**:
- Keep speed inside the settings flyout menu (old behavior) — rejected per user's explicit design choice
- Radio-button group — rejected; chip buttons with active highlight are visually cleaner

---

## Decision 4: Interface Contract Preservation

**Decision**: Preserve `PlayerControlsProps` interface verbatim. The `playbackSpeed` state is managed internally in `PlayerControls.tsx` and communicated up via `onPlaybackRateChange` callback.

**Rationale**: `SecureVideoPlayer.tsx` passes `onPlaybackRateChange` and `onQualityChange` callbacks already. Adding internal state for `playbackSpeed` (active highlight) avoids requiring a new prop on the parent, keeping the change surface minimal.

**Alternatives considered**:
- Adding `currentPlaybackSpeed` prop — rejected; unnecessary prop surface increase, parent doesn't need to track visual state

---

## Decision 5: Quality Menu Retention

**Decision**: Keep the quality selector in a compact settings popover (gear icon) in the right section of the bar, styled to match the new dark glassmorphism aesthetic.

**Rationale**: Quality is a less-frequently-used control that would clutter the inline bar. A popover is the natural UX pattern for secondary settings. FR-009 explicitly requires quality and fullscreen to be preserved.

---

## Summary of Knowns

| Topic | Finding |
|-------|---------|
| framer-motion v12 | `AnimatePresence`, `motion.div`, `animate`, `transition` APIs are unchanged from v11. Spring transitions work as before. |
| Design tokens | `bg-[#111111cc]`, `backdrop-blur-md`, `white/20`, `white/10` are already used across the project (e.g., `LessonCarousel`, `StudentShellChrome`). No new tokens needed. |
| `cn()` utility | Available at `@/lib/utils`; already imported in sibling components. |
| lucide-react v1.7 | `Volume1`, `Volume2`, `VolumeX`, `Play`, `Pause`, `Maximize`, `Settings` are all available. |
| TypeScript | `tsc --noEmit` passes with zero errors after implementation. |

**All research items resolved. Ready for Phase 1 design.**
