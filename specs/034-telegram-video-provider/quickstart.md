# Developer Quickstart: Telegram Video Provider

## Context
We are implementing a secondary video source ("telegram") that operates behind the existing `SecureVideoPlayer` proxy. The `SecureVideoPlayer` listens to `postMessage` events (`ready`, `stateChange`, `timeUpdate`) formatted identically to YouTube's Iframe API.

## Your Task

When building this feature:
1. **Frontend `EmbedProxy`**: Build the HTML generator for Telegram inside `src/app/api/video/embed/route.ts`. The route needs to fetch the `t.me` URL, parse the HTML string for the `<video src="...CDN...">` node, and output a blank HTML skeleton embedding that video. The `<script>` inside this HTML must emit the same events as the YouTube iframe logic (e.g. `1` for PLAYING, `2` for PAUSED) to seamlessly interact with `SecureVideoPlayer.tsx`.
2. **Backend Domain**: Create `TelegramVideoProvider : IVideoProvider` which extracts `channel/id` from a given URL to store minimal data in the database.
3. **Backend Service Registration**: Make sure the backend endpoint logic instantiates the provider cleanly (like a strategy pattern).
4. **Admin UI**: Add a `<Select>` dropdown for Provider ("youtube" / "telegram") next to the URL input in the lesson editor.

## Key Boundaries

- No new endpoints for checking video status. The proxy route fetches it server-side.
- The `SecureVideoPlayer` shouldn't realize there's a difference between providers.
- Handle failures by passing `-1` (UNSTARTED/ERROR) or throwing an exception in the Proxy.
