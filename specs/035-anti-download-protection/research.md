# Research: Anti-Download DRM & IDM Protection

## IDM Overlay Injection Mechanics
*   **Decision**: Implement a DOM-destruction hook (`video.removeAttribute('src')`) and strict CSS isolation.
*   **Rationale**: IDM and similar extensions rely on `MutationObserver` or interval-based polling of the DOM to find `<video>` tags with valid `src` attributes. By removing the `src` attribute immediately after the `loadstart` or `play` event, the browser internally maintains the media socket open in C++, but the JavaScript DOM (which IDM scans) appears empty.
*   **Alternatives considered**: Using `<canvas>` drawing (too CPU intensive for HD video), using `MediaSource` (incompatible with raw MP4 from Telegram without heavy JS transmuxing).

## Network Interception Defeat
*   **Decision**: Strict `Sec-Fetch-Dest` Validation + Short-Lived Redirects.
*   **Rationale**: IDM frequently hooks browser network requests. If IDM attempts to replay a request outside the browser's native `<video>` parsing execution context, it typically lacks the `Sec-Fetch-Dest: video` header. By aggressively asserting that `referer` must match the platform and `dest` must be `video`, we block standalone re-downloads.
*   **Alternatives considered**: One-Time Tokens (difficult to implement robustly because genuine browsers make multiple `Bytes=...` range requests during scrubbing, requiring the proxy to support multiple hits for the same token).

## DOM Obfuscation
*   **Decision**: Closed Shadow DOM + Transparent Click-Jacking Overlay.
*   **Rationale**: We already use a `mode: 'closed'` Shadow DOM. Adding `pointer-events: none` directly to the `<video>` element prevents hover-based extension logic from triggering overlay coordinates. The `z-index` overlay captures user clicks for play/pause without passing pointer events to the hidden video element.
*   **Alternatives considered**: Deeply nested iframes (adds latency and complicates fullscreen APIs).
