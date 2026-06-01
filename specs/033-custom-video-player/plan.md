# Implementation Plan: Custom Animated Video Player Controls

**Branch**: `033-custom-video-player` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/033-custom-video-player/spec.md`

## Summary

Replace the existing `PlayerControls.tsx` component with a premium animated floating pill-style control bar using `framer-motion`. The new design features spring-animated progress and volume sliders, animated play/pause toggling, playback speed chip buttons, a quality settings popover, and smooth blur+spring animate-in/out transitions. The `PlayerControlsProps` interface is preserved so `SecureVideoPlayer.tsx` requires zero changes.

## Technical Context

**Language/Version**: TypeScript (strict) — Next.js 16.2.1 / React 19  
**Primary Dependencies**: framer-motion ^12.38.0, lucide-react ^1.7.0, clsx + tailwind-merge (via `@/lib/utils`)  
**Storage**: N/A (pure UI component, no data persistence)  
**Testing**: TypeScript strict mode compile check via `tsc --noEmit`  
**Target Platform**: Web browser (desktop-first, responsive for tablet/mobile)  
**Project Type**: Web application frontend component  
**Performance Goals**: 60 fps animations with spring physics; controls appear within 100ms of hover  
**Constraints**: Must NOT alter `PlayerControlsProps` interface; must NOT affect `SecureVideoPlayer` session/tracking logic; must work within Tailwind CSS utility classes  
**Scale/Scope**: Single component replacement — 1 file (`PlayerControls.tsx`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Notes |
|------|-----------|--------|-------|
| Modular Clean Architecture | §I | ✅ PASS | Component stays in `components/video/` layer; no cross-module coupling |
| TypeScript strict mode | Tech Stack | ✅ PASS | Compiled with `tsc --noEmit`; zero errors |
| Framer Motion usage | Tech Stack | ✅ PASS | framer-motion is the mandated animation library |
| No plain JS files | Tech Stack | ✅ PASS | `.tsx` only |
| Premium Design System | §VIII | ✅ PASS | Dark glassmorphism pill, spring animations, `white/20` sliders align with curated aesthetic |
| No-Line Rule | §VIII | ✅ PASS | No hard borders; pill uses `bg-[#111111cc]` tonal shift |
| Provider Abstraction | §II | ✅ PASS | Controls are display-only; video session abstraction untouched |
| Security/Access Control | §III | ✅ PASS | No security implications; DOM-shield and watch-limit logic untouched |

**Result**: All gates pass. No complexity violations to track.

## Project Structure

### Documentation (this feature)

```text
specs/033-custom-video-player/
├── plan.md                    ← This file
├── research.md                ← Phase 0 output
├── data-model.md              ← Phase 1 output
├── checklists/
│   └── requirements.md        ← Spec quality checklist (complete ✅)
└── tasks.md                   ← Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
frontend/
├── src/
│   └── components/
│       └── video/
│           ├── PlayerControls.tsx     ← MODIFIED (primary deliverable)
│           └── SecureVideoPlayer.tsx  ← UNTOUCHED (no interface changes)
└── package.json                       ← No new dependencies required
```

**Structure Decision**: Pure frontend component replacement. Option 2 (web app) structure applies. Only `PlayerControls.tsx` is modified. `SecureVideoPlayer.tsx` and all backend/worker services are untouched.

## Complexity Tracking

> No constitution violations detected. Section not applicable.
