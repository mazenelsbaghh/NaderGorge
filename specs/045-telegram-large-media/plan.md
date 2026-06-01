# Implementation Plan: Telegram Large Media Fix

**Branch**: `045-telegram-large-media` | **Date**: 2026-04-01 | **Spec**: [spec.md](../../specs/045-telegram-large-media/spec.md)
**Input**: Feature specification from `/specs/045-telegram-large-media/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Enable playback of large Telegram videos by removing the server-side stream proxy and resolving the direct Telegram CDN URL for the client's browser. This bypasses Vercel/Next.js memory and payload limits, avoids timeouts and 404s, and ensures the bandwidth consumption rests entirely on Telegram's servers.

## Technical Context

**Language/Version**: TypeScript / Next.js 16.2.1
**Primary Dependencies**: `cheerio` (for parsing Telegram embed page)
**Storage**: N/A
**Testing**: N/A
**Target Platform**: Next.js App Router (API Handlers)
**Project Type**: Web API (Next.js Edge/Serverless functions)
**Performance Goals**: < 3 seconds Time to First Byte (TTFB)
**Constraints**: Bandwidth must remain on Telegram. The Next.js API must not buffer or stream large files to avoid serverless function limits.
**Scale/Scope**: Support Telegram video files exceeding 50MB.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Architecture**: Modifying an existing Next.js API route (`/api/video/embed`). No architectural boundary violations. (Pass)
- **Provider Abstraction First**: The logic is isolated to the "Telegram" provider branch inside the API route. (Pass)
- **Security & Access Control by Default**: The player is still hidden behind a generated Shadow DOM, and the direct URL is only injected securely via the server-side Next.js route, preserving the anti-download measures to the greatest extent possible. (Pass)
- **Phased Delivery**: This is a direct fix for Phase 2.5/3 Video Security capabilities regarding Telegram provider. (Pass)

## Project Structure

### Documentation (this feature)

```text
specs/045-telegram-large-media/
├── plan.md              
├── research.md          
├── data-model.md        
└── tasks.md             
```

### Source Code (repository root)

```text
frontend/
└── src/
    └── app/
        └── api/
            ├── video/
            │   ├── embed/
            │   │   └── route.ts
            │   └── stream-proxy/
            │       └── route.ts
```

**Structure Decision**: Using the Next.js `frontend` structure containing the `api/video` routes, as we will directly modify the URL resolution logic within the embed route and potentially deprecate or adjust the stream proxy.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

N/A
