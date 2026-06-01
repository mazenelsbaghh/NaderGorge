# Implementation Plan: Video Carousel Navigation

**Branch**: `027-video-carousel-navigation` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/027-video-carousel-navigation/spec.md`

## Summary

Implement an interactive `FeatureCarousel` (derived from the user's `cult-ui` example) to replace the static list of lesson videos on the student lesson page. The carousel will sync bi-directionally with the Active Video player, animating step transitions smoothly without utilizing error-prone `next/image` components within `framer-motion` states.

## Technical Context

**Language/Version**: Next.js 15 (React 19), TypeScript  
**Primary Dependencies**: `framer-motion`, `@remix-run/react` (for routing/params if applicable), `lucide-react`  
**Storage**: N/A (State is managed locally + URL params)  
**Testing**: Feature verification via dev browser  
**Target Platform**: Web Browsers (Mobile + Desktop)  
**Project Type**: Fullstack Monorepo (Next.js Frontend)  
**Performance Goals**: 60fps animations for carousel step transitions  
**Constraints**: MUST NOT use `next/image` in Framer Motion containers (Constitution Principle XI)  
**Scale/Scope**: Impacts all multi-video lessons across the platform

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **VIII. Premium Editorial Design System**: Glassmorphism and animations (`framer-motion`) elevate the student experience in line with the "curated archive" identity.
- [x] **XI. Frontend Reliability & Rendering Strictness**: The implementation strictly prohibits `<Image fill>` to avoid Turbopack compilation drops and height miscalculations during `scale`/`opacity` transitions.

## Project Structure

### Documentation (this feature)

```text
specs/027-video-carousel-navigation/
├── plan.md              # This file
├── research.md          # Strategy for integrating cult-ui safely
├── data-model.md        # Video DTO state mapping
├── quickstart.md        # Instructions for testing UI sync
├── contracts/           # Empty (no API boundaries changed)
```

### Source Code (repository root)

```text
frontend/
├── src/
│   ├── app/student/packages/[packageId]/lessons/[lessonId]/ # Target Page Location
│   │   ├── components/            
│   │   │   └── VideoCarousel.tsx    # Carousel Component Wrapper
│   │   └── page.tsx                 # Integration point
│   ├── components/
│   │   └── ui/                      # Shared Components
│   │       └── feature-carousel.tsx # Base cult-ui component (modified)
```

**Structure Decision**: The base `cult-ui` component will go into the shared `ui` folder. Because it needs to map specifically to our `Video` objects and handle player syncing, a wrapper component `VideoCarousel.tsx` will be created adjacent to the `page.tsx` it serves.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *None* | *N/A* | *N/A* |
