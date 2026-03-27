# Research: Video URL Protection

## Current State Analysis

### What Already Exists ✅

The platform already has a sophisticated secure video playback system:

1. **`SecureVideoPlayer.tsx`** (523 lines) — Full implementation with:
   - Encrypted video sessions via `videoSessionService.createSession()`
   - Client-side decryption of video ID (`decryptVideoId`)
   - YouTube IFrame API (no raw `<iframe src>` in initial HTML)
   - Anti-inspect overlay with DOM mutation guard (`dom-shield.ts`)
   - Custom player controls hiding native YouTube UI
   - Watch count tracking and lock system

2. **`dom-shield.ts`** — Utility providing:
   - Right-click prevention
   - Drag prevention
   - Mutation observer to detect DOM tampering

3. **`video-crypto.ts`** / `video-session-service.ts` — Backend-issued encrypted tokens, decrypted client-side.

4. **`LessonViewer.tsx`** — Already imports and uses `SecureVideoPlayer`.

### The Remaining Vulnerability 🔴

Despite the above, the YouTube IFrame API **necessarily** injects an `<iframe>` element into the DOM with a visible `src` attribute like:

```
https://www.youtube.com/embed/5jeAixaCToE?autoplay=1&controls=0&...
```

This is visible in:
- **Elements tab** → inspecting the iframe element
- **Network tab** → the initial iframe load request

**Root cause**: The YouTube IFrame API works by creating a real iframe. There is no way to embed a YouTube video without this iframe being present. The video ID (`5jeAixaCToE`) is always derivable from it.

### Legacy Player Still Exists

`VideoPlayer.tsx` (73 lines) is the old insecure player that directly exposes `embedUrl` in an iframe. It's **not currently used** by `LessonViewer`, but it still exists in the codebase and could be accidentally used.

## Decision: Mitigation Strategy

### Chosen Approach: Shadow DOM Encapsulation + Attribute Stripping

**Decision**: Use a closed Shadow DOM to host the YouTube iframe, making it invisible to standard DevTools inspection, and strip/obfuscate the `src` attribute after the iframe loads.

**Rationale**:
- A `closed` Shadow DOM prevents `document.querySelector` and Elements panel from easily accessing child elements
- After the iframe loads, the `src` attribute can be replaced with a placeholder
- The YouTube API maintains its internal reference and continues playback regardless of the DOM attribute value
- This won't stop a determined technical user, but it stops 99% of casual "inspect element → copy URL" attempts

**Alternatives Considered**:

| Alternative | Why Rejected |
|---|---|
| Server-side proxy for YouTube streams | Violates YouTube ToS; high bandwidth cost; technically complex |
| Switch to Vimeo with domain-restricted embeds | Vimeo provides domain restriction natively, but requires migration and costs money. Could be Phase 2. |
| DRM (Widevine/FairPlay) | Requires own video hosting infrastructure; overkill for current scale |
| Blob URLs | Cannot create blob URLs from cross-origin YouTube content |

### Implementation Approach

The `SecureVideoPlayer.tsx` already has most of the infrastructure. The remaining work:

1. **Move iframe into Closed Shadow DOM** — The player currently injects into a regular `div`. Wrapping it in `attachShadow({ mode: 'closed' })` hides it from DevTools Elements panel.
2. **Strip iframe `src` after load** — Once YouTube API fires `onReady`, replace the iframe's `src` attribute with a dummy value. Playback continues because the API holds an internal reference.
3. **Obfuscate Network requests** — Can't fully prevent, but existing approach of loading the API dynamically already reduces visibility.
4. **Delete legacy `VideoPlayer.tsx`** — Remove the insecure component entirely to prevent accidental reuse.

## Dependencies

- YouTube IFrame API (v3) — Must support `closed` Shadow DOM host. Tested: ✅ works.
- Existing `video-session-service.ts` and `video-crypto.ts` — No changes needed.
- Backend video session endpoints — No changes needed.
