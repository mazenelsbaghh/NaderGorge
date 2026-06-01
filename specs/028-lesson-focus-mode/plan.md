# Implementation Plan: Lesson Focus Mode

**Branch**: `028-lesson-focus-mode` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/028-lesson-focus-mode/spec.md`

## Summary

Implement an immersive Focus Mode for the student Lesson Viewer. When the student accesses a lesson, the global navigation elements (like the sidebar and top navbar) will be intentionally hidden or collapsed, providing maximum viewport area for the video player. The feature relies on a Zustand store mapped to `StudentShellChrome` via Framer Motion to ensure smooth unmounting and reappearance of edge-chrome segments without layout jank.

## Technical Context

**Language/Version**: Next.js 15 (React 19), TypeScript  
**Primary Dependencies**: `framer-motion`, `zustand`
**Storage**: N/A (Client-only state via Zustand memory, resets on navigation)  
**Testing**: Feature verification via dev browser  
**Target Platform**: Web Browsers (Mobile + Desktop)  
**Project Type**: Fullstack Monorepo (Next.js Frontend feature)  
**Performance Goals**: 60fps animations for layout shifts
**Constraints**: MUST support RTL, MUST unmount properly when navigating away using the browser back button
**Scale/Scope**: Impacts all `StudentShellChrome` views recursively based on active state

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **VIII. Premium Editorial Design System**: Using `framer-motion` for smooth layout sliding ensures UI edges maintain the "Premium Scholar" identity instead of disappearing abruptly.
- [x] **VI. Single-Flow Registration & UX Simplicity**: A simpler, distraction-free studying interface aligns with controlling the focus and guiding the student explicitly.
- [x] **I. Modular Clean Architecture**: Bypassing direct DOM mutation in favor of `Zustand` store synchronization.

## Project Structure

### Documentation (this feature)

```text
specs/028-lesson-focus-mode/
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
│   ├── app/student/
│   │   ├── packages/[packageId]/lessons/[lessonId]/
│   │   │   └── page.tsx                         # Integrates setFocusMode
│   ├── components/layout/
│   │   └── StudentShellChrome.tsx               # Wrapper orchestrator
│   └── stores/
│       └── lesson-focus-store.ts                # Zustand State
```

**Structure Decision**: A new `Zustand` store `lesson-focus-store.ts` serves as the communication bridge. `StudentShellChrome` consumes the store directly to perform layout manipulation via `AnimatePresence`. The `LessonViewer` (or `page.tsx` directly) asserts focus control upon `useEffect` mount.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *None* | *N/A* | *N/A* |
