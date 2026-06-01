# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Integrate VK.com as the exclusive third-party video serving infrastructure, utilizing the VK Javascript Iframe API (`postMessage`) to retain full custom UI tracking with zero server bandwidth. Deprecate and explicitly remove former proxy strategies (Google Drive, Telegram) from the entire stack.

## Technical Context

**Language/Version**: TypeScript 5.x, .NET 9
**Primary Dependencies**: Next.js App Router, Entity Framework Core
**Storage**: PostgreSQL (LessonVideo DB Table)
**Target Platform**: Browser (Cross Platform)
**Project Type**: Full-stack Web Application
**Performance Goals**: 0% Server Bandwidth utilized for Video Streaming
**Constraints**: Must accurately intercept VK's string-based HTTP JSON `postMessage` protocol to extract video `currentTime` values.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

No Constitutional violations detected. Code cleanup and simplification generally adhere to Clean Code patterns.

## Project Structure

### Documentation (this feature)

```text
specs/048-vk-video-provider/
├── plan.md              # This file
├── research.md          # VK API capabilities and Migration decisions
├── data-model.md        # DB validation changes
├── quickstart.md        # Admin instructional file
├── contracts/           # Empty structure for API models
└── spec.md              # Original Input
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Shared/
│   │   └── Enums/LessonVideoProvider.cs (Mock)
│   └── NaderGorge.API/
│       └── Controllers/LessonVideosController.cs

frontend/
├── src/
│   ├── app/api/video/drive-proxy/
│   │   └── route.ts     # TO BE DELETED
│   ├── app/api/video/embed/
│   │   └── route.ts     # TO BE REFACTORED to VK HTML wrapper
│   ├── components/video/
│   │   └── SecureVideoPlayer.tsx # Add VK listener hooks
```

**Structure Decision**: The frontend App Router `embed` route will be transformed into the dedicated VK `postMessage` cross-layer bridge parser, and all proxy modules will be effectively purged.

## Complexity Tracking

None. The architecture strictly reduces complexity by killing active proxy bots and deferring exclusively to an Iframe API.
