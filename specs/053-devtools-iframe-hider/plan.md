# Implementation Plan: devtools-iframe-hider

**Branch**: `053-devtools-iframe-hider` | **Date**: 2026-04-03 | **Spec**: [specs/053-devtools-iframe-hider/spec.md](spec.md)
**Input**: Feature specification from `/specs/053-devtools-iframe-hider/spec.md`

## Summary

Dynamically hide the video iframe target when a user opens DevTools by clearing its source or removing it from the DOM entirely, and properly restore it (and rebind player APIs) once DevTools are closed. This mitigates unauthorized URL extraction via the Inspect Elements tab.

## Technical Context

**Language/Version**: TypeScript 5.x, Next.js 16.2.x (Web API Route)
**Primary Dependencies**: None (vanilla JavaScript proxy generation)
**Storage**: N/A
**Testing**: Manual / End-to-end proxy tests
**Target Platform**: Web Browsers (Desktop/Mobile Web)
**Project Type**: Next.js App Router (Server-side rendered HTML response)
**Performance Goals**: Negligible footprint on proxy performance (< 10ms render time)
**Constraints**: Must not break YouTube/VK JS APIs which rely on iframe messages
**Scale/Scope**: Proxy route (`/api/video/embed/route.ts`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. Modifications are isolated to the specific proxy mechanism serving videos.
- **III. Security & Access Control by Default**: Pass. Implements defense-in-depth against resource scraping.
- **IV. Phased Delivery with MVP Discipline**: Pass. Incremental enhancement to Phase 2.5 Video Security.

## Project Structure

### Documentation (this feature)

```text
specs/053-devtools-iframe-hider/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── quickstart.md        # Phase 1 output
```

### Source Code (repository root)

```text
frontend/
└── src/
    └── app/
        └── api/
            └── video/
                └── embed/
                    └── route.ts
```

**Structure Decision**: Modifications will apply to the existing single file `route.ts` which acts as the generic proxy generator for all supported video providers.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |
