# Technical Research: Rutube.ru Iframe API

## Needs Clarification Resolution

### Unknown: Rutube Player API `postMessage` Details
- **Decision**: We will interface with Rutube's native HTML5 Player API via `postMessage`.
- **Rationale**: Rutube provides a well-documented Javascript API built exactly for embedding frames in third-party applications without exposing the direct video URL.
- **Alternatives considered**: Parsing Rutube HTML for the direct `.m3u8` link. Rejected due to CORS issues, token expiration, and unnecessary maintenance overhead.

### Technical Implementation Details
The Rutube Player API uses `window.postMessage`.

#### Initialization
The iframe must point to `https://rutube.ru/play/embed/{video_id}`.

#### Incoming Messages (From Rutube to Parent)
Rutube emits JSON strings. When parsed, they follow this format:
```json
{
  "type": "player:changeState",
  "data": { "state": "playing" | "paused" | "stopped" }
}
```
```json
{
  "type": "player:currentTime",
  "data": { "time": 12.34 }
}
```

#### Outgoing Messages (From Parent to Rutube)
We must send JSON strings to the iframe's `contentWindow`:
- Play: `{"type": "player:play", "data": {}}`
- Pause: `{"type": "player:pause", "data": {}}`
- Seek: `{"type": "player:setCurrentTime", "data": {"time": 100}}`

These events map perfectly to our `video-embed-play`, `video-embed-pause`, and `video-embed-seek` internal payloads in `SecureVideoPlayer.tsx`.
