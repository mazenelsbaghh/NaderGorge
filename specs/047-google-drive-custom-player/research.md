# Research: Google Drive Custom Video Player

## Technical Unknowns Resolved

### 1. Can we stream Google Drive videos directly to the client while keeping the Custom UI without proxying?
**Decision**: No, it is technically impossible without proxying through a backend or facing immediate browser blockades.
**Rationale**: Google Drive's CDN returns the `Content-Disposition: attachment` header for media exports. The Chrome browser explicitly blocks raw HTML5 `<video>` elements from playing `attachment` streams due to security constraints (Same-Origin Policy & MIME sniffing). To circumvent this, the stream must either be wrapped in Google's official `iframe` player (which disables our custom UI and progress tracking) or proxied through a server backend that systematically strips the `attachment` header (which doubles bandwidth usage).
**Alternatives considered**: 
- **Service Workers**: Cannot parse or intercept opaque cross-origin streams due to CORS restrictions, meaning the `Content-Disposition` header cannot be manipulated on the client side.
- **Client-side blob streaming**: Fetching the 400MB video into RAM completely locks up student devices, especially mobile phones.

### 2. Can we use Google Drive's UI but place our UI floating over it?
**Decision**: Discarded.
**Rationale**: Google Drive's preview iframe does not publish a Cross-Origin API standard (like the YouTube Iframe API `postMessage`). Because of the browser's Sandbox and Same-Origin policy, our platform cannot inject commands into the Google Window to click "Play", nor can we read the `currentTime` to increment the student's watched seconds reliably.
**Alternatives considered**: Polling DOM changes visually or attempting heuristic measurements. They are exceptionally flaky and fail cross-origin validation checks.

### 3. What is the optimal architecture going forward for 0 bandwidth overhead?
**Decision**: Migrate towards S3-compatible cheap Object Storage (e.g., BunnyCDN or Cloudflare R2).
**Rationale**: Object Storage seamlessly supports CORS and direct video streaming byte-ranges out of the box with `Content-Disposition: inline`. This enables maximum platform control via our `SecureVideoPlayer`, uses absolutely zero server bandwidth, and protects against DMCA/Copyright strikes that affect YouTube.
**Alternatives considered**: Vimeo Premium (Too expensive per terabyte for scaled educational platforms), Telegram Proxy (Bandwidth Intensive on VPS), YouTube Hidden Mode (Copyright Takedown risks).
