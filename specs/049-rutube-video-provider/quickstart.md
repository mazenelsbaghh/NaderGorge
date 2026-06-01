# Quickstart: Rutube Video Provider

## Context
This feature replaces VK with Rutube as the custom secure video player proxy backing.

## Key Integration Point
The main logic sits inside `frontend/src/app/api/video/embed/route.ts`. 

Because Rutube uses a standard HTML5 `postMessage` architecture, you must:
1. Detect `provider === 'rutube'`.
2. Return a custom secure HTML block holding the `<iframe src="https://rutube.ru/play/embed/VIDEO_ID">`.
3. Inside the returned HTML block, inject a `<script>` tag that attaches `window.addEventListener('message')`.
4. The injected script parses events coming from the Rutube iframe (`player:currentTime`).
5. The injected script translates those native Rutube events into our own Secure Player payload: `window.parent.postMessage({ type: "video-embed-progress", currentTime: X }, "*")`.

This translation allows `SecureVideoPlayer.tsx` to remain completely oblivious to whether the provider is YouTube or Rutube. It just receives consistent `video-embed-progress` messages!

## Testing
1. Get a sample Rutube link (e.g., `https://rutube.ru/video/1a2b3c4d/`).
2. Add it to a lesson through the Admin Panel.
3. Open the Lesson viewport.
4. Verify the player controls hide the Rutube branding.
5. Click play and ensure the `WatchStatusBar.tsx` progresses alongside the video.
