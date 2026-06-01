# Implementation Plan: Anti-Download DRM & IDM Protection

**Branch**: `035-anti-download-protection` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/035-anti-download-protection/spec.md`

## Summary

Implement robust anti-download mechanics against extensions like Internet Download Manager. The approach combines strict backend streaming proxy validation (verifying `Sec-Fetch-Dest` and `Referer`) with aggressive frontend DOM obfuscation (`video.removeAttribute('src')` and CSS click-jacking isolators) to prevent extensions from identifying the raw Telegram `.mp4` URLs.

## Technical Context

**Language/Version**: TypeScript (Next.js 16.2.1 / React 19)
**Primary Dependencies**: Next.js App Router (Server-side API handlers)
**Storage**: N/A (Stateless Token Validation)
**Testing**: Jest
**Target Platform**: Linux server (Web Browsers)
**Project Type**: Next.js web application
**Performance Goals**: <5ms proxy token decryption overhead
**Constraints**: Must maintain stable playback native connection speed
**Scale/Scope**: Affects all video lessons

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

*   **I. Modular Architecture:** Complies. All logic remains isolated within the `video/stream-proxy` boundary.
*   **IV. Phased Delivery:** Complies. Feature built atop the Phase 2.5 Video Security footprint.
*   **V. Academic Content Integrity:** Complies. Prevents unauthorized redistribution of the teacher's brand content.

## Project Structure

### Documentation (this feature)

```text
specs/035-anti-download-protection/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code

```text
frontend/src/app/api/video/
├── embed/
│   └── route.ts         # DOM obfuscation inject layer
└── stream-proxy/
    └── route.ts         # Token and secure referrer validation
```

**Structure Decision**: Modifications happen strictly inside the dedicated Next.js video API boundaries created during standard phase implementations. No new components are required.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| No URL in DOM | Defeat IDM overlay | Letting native URL stay in the `src` was rejected because IDM uses interval DOM-scraping mutation observers to hook standard MP4 files. |
