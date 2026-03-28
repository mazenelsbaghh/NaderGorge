# Research: Secure Video Player

**Feature**: 005-secure-video-player
**Date**: 2026-03-25

---

## R1: Video Embedding Strategy — Iframe vs. Direct Playback

### Decision: Server-side proxy endpoint + YouTube IFrame API with obfuscated injection

### Rationale
- YouTube's Terms of Service require using the official IFrame Player API for embedding
- Direct video stream proxying (downloading YouTube content server-side and re-serving) violates YouTube TOS
- The IFrame API can be loaded dynamically via JavaScript, injecting the video ID at runtime from an encrypted/tokenized server response, keeping it out of the initial HTML
- By injecting the iframe dynamically and clearing the DOM attributes post-load, casual inspection is significantly hindered

### Alternatives Considered
1. **youtube-dl/yt-dlp server proxy**: Downloads video server-side and streams to client. Violates YouTube TOS and is resource-intensive. Rejected.
2. **Plyr.js wrapper**: Wraps YouTube iframe with custom controls. Good UX but doesn't obscure the video ID from the DOM. Partially adopted (for custom controls overlay).
3. **Video.js with YouTube plugin**: Similar to Plyr, wraps iframe. Heavier bundle. Rejected for bundle size.

---

## R2: URL Obfuscation Approach

### Decision: Encrypted token endpoint with short-lived, signed video session tokens

### Rationale
- Backend generates a time-limited (5 min) signed token containing the encrypted video ID
- Frontend requests this token endpoint, which validates auth + access + watch limits
- Token is decrypted client-side using a session-specific key delivered via a separate channel (e.g., embedded in the JWT claims or a secondary handshake)
- The YouTube video ID is never sent in plaintext over the network or stored in the DOM

### Implementation Flow
1. Student opens lesson → frontend calls `GET /api/student/video-session/{lessonVideoId}`
2. Backend validates: auth → access → watch limit → generates encrypted token + session key
3. Frontend receives `{ token: "encrypted_blob", key: "session_key" }`
4. Frontend decrypts token client-side → extracts YouTube video ID
5. Frontend dynamically creates YouTube IFrame player with the decrypted ID
6. After iframe loads, frontend clears the decrypted ID from memory and obfuscates DOM

### Alternatives Considered
1. **Plaintext video ID in API response**: Simple but defeats the purpose. Rejected.
2. **Server-side HTML rendering**: Render the iframe server-side. Still visible in page source. Rejected.
3. **WebSocket-based delivery**: Over-engineered for this use case. Rejected.

---

## R3: Anti-Inspection DOM Measures

### Decision: Multi-layer DOM obfuscation

### Techniques
1. **Dynamic injection**: Create iframe via `document.createElement` instead of JSX/HTML
2. **Shadow DOM**: Place the player inside a Shadow DOM to isolate it from the main DOM tree
3. **Attribute cleanup**: After YouTube player loads, overwrite the `src` attribute with a blob URL or remove it
4. **MutationObserver**: Monitor for DevTools modifications and re-obfuscate if detected
5. **Right-click disable**: Prevent context menu on the player container
6. **Drag prevention**: CSS `user-select: none` and `pointer-events` management

### Limitations (Accepted)
- A determined user with advanced DevTools skills can still intercept network requests
- Browser extensions can bypass Shadow DOM isolation
- This is "casual protection" — sufficient to prevent 95%+ of students from extracting URLs

---

## R4: Watch Limit Integration

### Decision: Reuse existing `VideoWatchEvent` entity with server-side enforcement

### Rationale
- The existing `VideoWatchEvent` entity already tracks `WatchCount` and `IsLocked`
- The new video session endpoint will check `WatchCount < MaxWatchCount` before issuing a token
- Watch count increments when the tracking endpoint receives a 30+ second watch event
- No schema changes required

---

## R5: Custom Player Controls

### Decision: Custom HTML/CSS overlay on top of YouTube IFrame

### Rationale
- YouTube IFrame API provides programmatic control methods (play, pause, seekTo, etc.)
- Build custom controls matching the Pharaonic theme using HTML/CSS
- Overlay the controls on top of the iframe (with pointer-events management)
- This allows full control over the player chrome while using YouTube's playback engine

### Controls Needed
- Play/Pause button
- Seek bar (progress indicator)
- Volume control
- Playback speed selector (0.5x, 1x, 1.25x, 1.5x, 2x)
- Fullscreen toggle
- Remaining watches indicator
- Lesson title display
