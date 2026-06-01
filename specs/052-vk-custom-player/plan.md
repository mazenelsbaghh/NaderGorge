# Implementation Plan: VK Custom Video Player

**Branch**: `052-vk-custom-player` | **Date**: 2026-04-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/052-vk-custom-player/spec.md`

## Summary

The platform requires a custom branded video player replacing the native VK (VKontakte) player while maintaining programmatic control over video playback. We will achieve this by extending our existing Server-Side Embed Approach (`/api/video/embed`). When a session for a VK video is requested, the frontend proxy will return an HTML document that embeds the native VK iframe (`js_api=1`) and standardizes its events to match the `postMessage` protocol already used for YouTube. This hides the native controls from the user behind an overlay and provides our custom React UI (`SecureVideoPlayer` and `PlayerControls`) seamless control without architectural fragmentation.

## Technical Context

**Language/Version**: TypeScript (strict) / React 19 / Next.js 16.2.1
**Primary Dependencies**: Next.js, standard DOM APIs, VK JS API (`https://vk.com/js/api/videoplayer.js`)
**Storage**: PostgreSQL (existing `LessonVideo` support for `provider = "vk"`)
**Testing**: Existing testing structure.
**Target Platform**: Web browsers (Desktop and Mobile)
**Project Type**: Next.js Web Application Feature
**Performance Goals**: Controls responsive <300ms, event sync <1s delay.
**Constraints**: Must match exact UI/UX of our `PlayerControls` module. Must prevent direct interaction with native VK UI.
**Scale/Scope**: Frontend routing and UI overlay modification, applicable to all videos mapped to the VK provider.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: PASS. The integration follows the established `VideoProviderAbstraction` pattern at the API boundary, keeping external provider logic isolated from the `SecureVideoPlayer` core.
- **II. Provider Abstraction First**: PASS. Reusing our `embed/route.ts` postMessage abstraction ensures the frontend React components don't care if the video is YouTube or VK.
- **III. Security & Access Control by Default**: PASS. VK Video IDs are not exposed directly to the React client application; they are decrypted server-side in the embed route. Domain locking (`X-Frame-Options`) will be maintained.
- **V. Academic Content Integrity**: PASS. Seamless integration with `ChapterList` via identical programmatic `seekTime()` control guarantees academic pacing parity.
- **VIII. Premium Editorial Design System**: PASS. By hiding the native VK controls and using our `PlayerControls` overlay, we maintain the "Editorial Scholar" visual branding.

## Project Structure

### Documentation (this feature)

```text
specs/052-vk-custom-player/
├── plan.md              # This file
├── research.md          # Research on VK API integration
├── data-model.md        # Entities involved
├── quickstart.md        # Implementation entry flow
└── tasks.md             # Task list for execution (done later)
```

### Source Code

**Structure Decision**: Single Monorepo with Next.js frontend and C# backend. Changes are purely on the Frontend structure logic.

- `frontend/src/app/api/video/embed/route.ts` - Extended to serve VK HTML template
- `frontend/src/components/admin/AddVideoForm.tsx` - Admin selection for VK
- `frontend/src/components/video/SecureVideoPlayer.tsx` - Verification for any slight postMessage additions (if needed)

## Complexity Tracking

No violations of the Constitution or unusual complexity added. Alternative approaches (like direct frontend embedding) were rejected to keep the security boundary clean and uniform with YouTube.
