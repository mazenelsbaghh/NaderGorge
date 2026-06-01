# Implementation Plan: Rutube Video Provider

**Branch**: `049-rutube-video-provider` | **Date**: 2026-04-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/049-rutube-video-provider/spec.md`

## Summary

This feature replaces `vk` with `rutube` as a valid video provider. It will parse Rutube video URLs, host the Rutube iframe within the `/api/video/embed` proxy route, and hook into Rutube's native Javascript Iframe API (`postMessage`) to ensure seamless synchronization with our custom secured `SecureVideoPlayer.tsx`.

## Technical Context

**Language/Version**: C# 13 (.NET 9) Backend, TypeScript 5.x / Next.js 16.2.1 Frontend  
**Primary Dependencies**: Next.js App Router (Frontend Proxy), EF Core 9.0 (Backend Data)  
**Storage**: PostgreSQL (LessonVideo DB Table)  
**Testing**: E2E via E2E testing framework manually   
**Target Platform**: Web Browsers (Chrome, Safari, Firefox, Edge)  
**Project Type**: Fullstack Educational Web Application  
**Constraints**: Rutube Iframe structure and postMessage API must be accurately matched.  
**Scale/Scope**: System-wide integration for all new video content.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Modular Clean Architecture**: Adds Rutube handling behind the `VideoProviderAbstraction`.
- [x] **Provider Abstraction First**: Maintains decoupling of the UI from Rutube SDK.
- [x] **Security by Default**: Maintains DOM layer protection (Z-Indexes) restricting direct interaction with Rutube frame elements.
- [x] **Single-Flow & UX Simplicity**: Hides Russian UI branding from students and aligns Rutube UI seamlessly to local UI.
- [x] **Premium Editorial Design System**: Overlays standard UI patterns onto Rutube.
- [x] **Pricing & Currency**: Unaffected.

**All Gates Passed.**

## Project Structure

### Documentation (this feature)

```text
specs/049-rutube-video-provider/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Shared/Enums/
│   └── NaderGorge.Infrastructure/Providers/
└── tests/

frontend/
├── src/
│   ├── app/api/video/embed/
│   ├── components/admin/
│   ├── components/video/
│   └── app/admin/content/
└── tests/
```

**Structure Decision**: The feature spans the Backend Enum definitions and the Frontend `embed` App Router + Secure Player components.
