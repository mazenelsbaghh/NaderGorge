# Implementation Plan: Video URL Protection

**Branch**: `013-video-url-protection` | **Date**: 2026-03-26 | **Spec**: [spec.md](./spec.md)

## Summary

The platform already has a `SecureVideoPlayer` with encrypted sessions, client-side decryption, YouTube API injection, and DOM shield utilities. However, the YouTube IFrame API still creates a visible `<iframe src="https://youtube.com/embed/VIDEO_ID">` in the DOM that students can extract via DevTools. This plan adds **closed Shadow DOM encapsulation** and **iframe `src` stripping** to hide the video URL from casual inspection, plus deletes the legacy insecure `VideoPlayer.tsx`.

## Technical Context

**Language/Version**: TypeScript (Next.js 15)  
**Primary Dependencies**: YouTube IFrame API, `shadow-dom`, `MutationObserver`  
**Storage**: N/A (no backend changes)  
**Testing**: Browser DevTools manual inspection + Playwright  
**Target Platform**: Web (desktop + mobile browsers)  
**Performance Goals**: Video playback starts within 3 seconds  
**Constraints**: Cannot break YouTube IFrame API functionality  
**Scale/Scope**: Single component change (`SecureVideoPlayer.tsx`)

## Constitution Check

| Principle | Status | Notes |
|---|---|---|
| I. Modular Architecture | ✅ Pass | Changes isolated to `SecureVideoPlayer.tsx` and utilities |
| II. Provider Abstraction | ✅ Pass | Using existing `VideoProviderAbstraction` |
| III. Security | ✅ Pass | This feature directly improves content security |
| V. Content Integrity | ✅ Pass | Protects teacher's paid video content |
| VIII. Design System | ✅ Pass | No visual changes to player UI |

## Project Structure

### Documentation

```text
specs/013-video-url-protection/
├── spec.md
├── plan.md              # This file
├── research.md          # ✅ Complete
├── data-model.md        # ✅ Complete (no changes needed)
└── quickstart.md        # ✅ Complete
```

### Source Code Changes

```text
frontend/src/
├── components/
│   ├── video/
│   │   └── SecureVideoPlayer.tsx   # MODIFY: Add Shadow DOM + src stripping
│   └── content/
│       └── VideoPlayer.tsx         # DELETE: Legacy insecure player
└── utils/
    └── dom-shield.ts               # MODIFY: Add Shadow DOM helpers
```

## Implementation Phases

### Phase 1: Shadow DOM Encapsulation

Modify `SecureVideoPlayer.tsx` → `initPlayer()`:

1. Create a closed Shadow DOM (`attachShadow({ mode: 'closed' })`) on the container div
2. Move the YouTube player `div` into the shadow root
3. After YouTube API `onReady` fires, strip the iframe `src` attribute (replace with `about:blank` while keeping the internal API reference alive)

### Phase 2: Cleanup

1. Delete `VideoPlayer.tsx` (legacy insecure player)
2. Verify no other imports reference it
3. Update `dom-shield.ts` to add a Shadow DOM helper if needed

### Phase 3: Verification

1. DevTools Elements tab → iframe should not be visible (hidden in closed Shadow DOM)
2. Network tab → YouTube requests still visible (unavoidable) but video ID is harder to correlate
3. Video playback must work normally with all custom controls
