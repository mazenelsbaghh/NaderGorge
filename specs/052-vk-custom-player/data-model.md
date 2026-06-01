# Data Model Updates: VK Custom Player

The VK Custom Player feature is primarily a frontend UI and Proxy pattern enhancement. It integrates directly into the existing Video Session flow.

## Key Entities Touched

### `LessonVideo`
- **Fields**: No schema changes required.
- **Behavior**: Uses the `Provider` field set to `"vk"` and `ProviderVideoId` storing the specific video identifier for VK (e.g. extracted from `oid=-22822305&id=456241864` as a composite or direct ID).

### `VideoSessionDto` 
- **Fields**: Unchanged.
- **Behavior**: `Provider` returns `"vk"` to the client.

### `VideoPlaybackSession` / `VideoSessionService`
- **Fields**: Client requests session passing `lessonVideoId`. Server encrypts `Provider` and `VideoId` into the token.
- **Behavior**: The encrypted token is passed to `/api/video/embed` in `SecureVideoPlayer.tsx`. The API route decrypts and uses these parameters.
