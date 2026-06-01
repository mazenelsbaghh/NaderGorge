# Implementation Plan: Telegram Video Provider Support

**Branch**: `034-telegram-video-provider` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/034-telegram-video-provider/spec.md`

## Summary

Add the ability for admins to attach Telegram-hosted videos to lessons via public post URLs (`https://t.me/channel/postid`), utilizing a new `telegram` video provider type. The student player will stream the video seamlessly through the existing `SecureVideoPlayer` by wrapping an HTML5 `<video>` tag exactly like the current YouTube wrapper.

## Technical Context

**Language/Version**: C# (.NET 9) Backend, TypeScript (Next.js) Frontend
**Primary Dependencies**: Next.js App Router API Handlers (Proxy), Cheerio/HtmlAgilityPack (for scraping the embed tag), PostgreSQL (Data Store)
**Storage**: Modify the API endpoints/DTOs to accept `"telegram"` as a valid `provider` string for `LessonVideo`.
**Target Platform**: Web browsers (via `SecureVideoPlayer.tsx` HTML shell)
**Project Type**: Full-stack web application
**Constraints**: Keep backend DB changes zero-impact (the `Provider` and `ProviderVideoId` field already exist as strings). The API handler must map Telegram `<video>` events (`play`, `pause`, `timeupdate`, `ended`) into the exact YouTube PostMessage shape (`ready`, `stateChange`, `timeUpdate`) to avoid disturbing the decoupled frontend tracking system.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Yes. Extending `IVideoProvider` and adding `TelegramVideoProvider` abstraction.
- **II. Provider Abstraction First**: Yes. The `SecureVideoPlayer` has no hard dependency on YouTube. It listens precisely to normalized events via its iframe `postMessage` protocol. We are building a Telegram shim inside the embed route to mimic this protocol.
- **V. Academic Content Integrity**: Yes. Content remains protected, context menu disabled, and limits tracked.
- **XI. Frontend Reliability & Rendering Strictness**: The `<video>` wrapper will be rendered cleanly inside a shadow DOM without Next.js Image injection issues or external UI dependencies.

## Project Structure

### Documentation (this feature)

```text
specs/034-telegram-video-provider/
├── plan.md              
├── research.md          
├── data-model.md        
├── contracts/api.md             
└── quickstart.md             
```

### Source Code

```text
backend/src/NaderGorge.Domain/
├── Interfaces/
│   └── IVideoProvider.cs
└── Services/
    └── TelegramVideoProvider.cs (NEW)

backend/src/NaderGorge.Application/
└── Features/Admin/Commands/
    └── AddLessonVideoCommand.cs (Update Validation/Provider mapping)

frontend/src/app/api/video/embed/
└── route.ts (Enhance logic to handle provider == "telegram")

frontend/src/app/admin/content/
└── [id]/components/ (Enhance Admin Lesson video entry form to allow dropdown selection of Provider)
```

**Structure Decision**: Web application spanning the `.NET Backend` domain model abstraction and the `Next.js Frontend` embed API handler logic.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| No violations | N/A | N/A |
