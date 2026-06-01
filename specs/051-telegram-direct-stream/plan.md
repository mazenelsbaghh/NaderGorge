# Implementation Plan: Telegram Direct Stream

**Branch**: `051-telegram-direct-stream` | **Date**: 2026-04-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/051-telegram-direct-stream/spec.md`

## Summary

Remove OK.ru entirely and build a proxy endpoint that evaluates a direct Telegram file link (provided by external bots as the `VideoId`/`Url`), intercepts its 302 Redirect destination dynamically, and streams it back natively via HTML5 Video logic inside `SecureVideoPlayer`, giving complete headless playback capabilities without taking bandwidth from the academy's host server.

## Technical Context

**Language/Version**: TypeScript (Next.js 16.2.1 / React 19 Frontend) & C# 13 (.NET 9 Backend API)
**Primary Dependencies**: Next.js App Router (for Proxy Endpoint), `HTML5 <video>`
**Storage**: PostgreSQL (LessonVideo DB Table, minimal modification required - just enum change)
**Testing**: Manual E2E test via Browser  
**Target Platform**: Web Browsers
**Project Type**: Next.js Web App / .NET Backend
**Performance Goals**: <50ms redirect response for `/api/video/stream-proxy`
**Constraints**: Must never stream actual MP4 content through the Next.js server to avoid bandwidth costs. Must ONLY proxy the redirect headers.
**Scale/Scope**: Removing OK.ru provider enum and adding Telegram logic in API routes and AddVideoForm.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: The proxy logic is isolated correctly within `frontend/src/app/api/video/stream-proxy/route.ts` and the UI respects component boundaries in `SecureVideoPlayer`.
- **Provider Abstraction First**: The C# backend will seamlessly adapt `telegram` as just another string option in the video provider logic exactly as it did for youtube/okru.
- **Academic Content Integrity**: The content remains 100% academic via full player control (no suggested videos from YouTube or OK.ru tracking).

## Project Structure

### Documentation (this feature)

```text
specs/051-telegram-direct-stream/
├── plan.md              
├── data-model.md        
└── contracts/           
```

### Source Code

```text
backend/
├── src/NaderGorge.Infrastructure/
│   ├── Providers/
│   │   ├── TelegramVideoProvider.cs # If not already present
│   │   └── OkVideoProvider.cs       # To be REMOVED

frontend/
├── src/
│   ├── app/
│   │   ├── api/
│   │   │   └── video/
│   │   │       ├── embed/route.ts         # Remove okru references
│   │   │       └── stream-proxy/route.ts  # Add Telegram 302 logic
│   ├── components/
│   │   ├── video/
│   │   │   └── SecureVideoPlayer.tsx      # Add HTML5 video handler
│   │   ├── admin/
│   │       └── AddVideoForm.tsx           # Swap okru with telegram provider
```

**Structure Decision**: Web application option 2. Backend adapts enum/provider models. Frontend applies player UX logic and hosts the `stream-proxy` Edge handler.
