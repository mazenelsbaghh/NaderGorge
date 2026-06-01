# Phase 0: Outline & Research

## Research: Direct Media Streaming for Telegram Provider

**Decision**: The `/api/video/embed` route will bypass our stream proxy and inject the exact direct Telegram CDN URL (`videoSrc`) into the HTML `<video src="...">` element.

**Rationale**: When users tried to load large Telegram videos, the `/api/video/embed` (and consequently `/api/video/stream-proxy`) failed with a 404 or a timeout. Vercel and Next.js API routes carry payload size limits (e.g., 4.5MB for Vercel functions without streaming configs) and execution timeouts. By proxying the video stream, we were unnecessarily burdening our own servers with high bandwidth costs and hitting execution limits for large files. Delivering the direct Telegram CDN URL directly to the browser solves all these issues and shifts the bandwidth consumption entirely to Telegram's infrastructure, fulfilling the specific user requirement.

**Alternatives considered**:
- **Optimizing the Node.js Stream Proxy** to support chunked responses and Range Headers. Rejected because it still consumes our server bandwidth and can still face serverless timeouts.
- **Using a custom backend (C#) streaming service**: Rejected because it also conflicts with the requirement of keeping bandwidth consumption on Telegram.

## Impact on Anti-Download Protections

The direct URL will be written into the `video.src` property inside a closed Shadow DOM, and immediately cleared via `video.removeAttribute('src')` on `loadstart`. Although highly motivated bad actors can theoretically intercept network requests from DevTools, this solution maintains the existing HTML isolation DOM protections (IDM mutators killer, click-overlays) that satisfy the product's piracy deterrence requirements while enabling the application to cleanly play files larger than the proxy constraints allow.
