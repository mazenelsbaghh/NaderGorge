# Implementation Plan: YouTube-like player chapters

**Branch**: `042-video-player-chapters` | **Date**: 2026-04-01 | **Spec**: [link](../spec.md)
**Input**: Feature specification from `/specs/042-video-player-chapters/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Enhancing the custom video player interface with interactive YouTube-style chapter markers on the timeline scrubber. The existing `InteractiveTimeline` component partially handles gap calculations and hover preview tooltips, but the `PlayerControls` component needs to be updated to permanently sync and display the `activeChapter` next to the running timestamp as the video plays.

## Technical Context

**Language/Version**: TypeScript (Next.js 16.2.1 / React 19)
**Primary Dependencies**: `framer-motion`, existing custom components (`PlayerControls.tsx`, `InteractiveTimeline.tsx`)
**Storage**: N/A (Frontend data merely passed as props from pre-existing backend query)
**Testing**: Frontend manual validation
**Target Platform**: Web Browsers
**Project Type**: React Frontend Application
**Performance Goals**: <100ms latency for hover tooltips. Seeking latency negligible. Native framer motion scrubber tracking without React state lag overrides.
**Constraints**: Bypassing DRM IDM traps; strictly DOM manipulation-free apart from React's domain.
**Scale/Scope**: Impacts single page route (`LessonCarousel.tsx → SecureVideoPlayer.tsx → PlayerControls.tsx`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: PASS (Changes isolated to `components/video` components).
- **V. Academic Content Integrity**: PASS (Enhances learning structure for bounded videos).
- **VIII. Premium Editorial Design System**: PASS (Utilizes glassmorphism and motion components consistent with the platform UI).

## Project Structure

### Documentation (this feature)

```text
specs/042-video-player-chapters/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
frontend/
├── src/
│   ├── components/
│   │   └── video/
│   │       ├── PlayerControls.tsx
│   │       ├── InteractiveTimeline.tsx
│   │       └── SecureVideoPlayer.tsx
```

**Structure Decision**: Utilizing the existing single frontend application project folder. No newly architectural files are projected; merely feature enhancements to the components mapped above.
