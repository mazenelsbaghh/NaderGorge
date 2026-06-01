# Research: VK Custom Video Player

## Technical Unknowns and Decisions

### 1. Where to Integrate the VK API
- **Decision**: Integrate `VK.VideoPlayer` inside the server-rendered HTML at `frontend/src/app/api/video/embed/route.ts`.
- **Rationale**: Currently, the platform uses a "Server-Side Embed Approach" for YouTube to conceal the `VideoId`. By extending this proxy route to handle `provider === 'vk'`, we maintain the same level of security (the VK video URL won't be exposed in client-side React code). It also allows us to reuse the exact same `postMessage` interface between the iframe and `SecureVideoPlayer.tsx`, minimizing changes to the complex `SecureVideoPlayer` component.
- **Alternatives considered**: Injecting the VK script directly into `SecureVideoPlayer.tsx`. Rejected because it would expose the VK video ID in the client and require duplicating the DOM shield and watermark logic which is already neatly encapsulated in the embed iframe for external providers.

### 2. Event Handling and Synchronization
- **Decision**: Map VK VideoPlayer events (`timeupdate`, `statechange` equivalents) to our custom `postMessage` (`timeUpdate`, `stateChange`, `ready`, `error`). 
- **Rationale**: `SecureVideoPlayer` is already listening to these exact events from the embedded iframe for YouTube. If the VK embed emits the identical messages, the frontend tracking, progress bar, UI syncing, and watch duration logic will work out of the box without any modification.
- **Alternatives considered**: Exposing VK directly. Rejected as it breaks the `postMessage` abstraction boundary designed for the YouTube integration.

### 3. Masking Native VK Controls
- **Decision**: The `VK.VideoPlayer` loads an iframe. To hide the native VK controls (which sit at the bottom or top of the VK iframe), we will apply CSS masking or use pointer-events trickery in the embed route, and position an overlay over the iframe that intercepts clicks (for play/pause toggling). If VK provides a parameter to hide controls (like `controls=0` for YouTube), we will use it; otherwise, the overlay will prevent the user from interacting with the native VK UI, and they will only interact with our floating React `PlayerControls`.
- **Rationale**: Requirement FR-004 mandates the concealment of VK branding and controls.

### 4. Admin UI Integration
- **Decision**: Update `AddVideoForm.tsx` (and any related admin view) to include "VK" in the provider dropdown list, mapping `provider` to `'vk'`. Also update `urlOrEmbedCode` change handler to recognize `vk.com/video` URLs and auto-select `'vk'`.
- **Rationale**: Needed for FR-010.

## Implementation Path
- Modify `embed/route.ts` to return entirely different HTML when `provider === 'vk'`. The HTML will contain an iframe pointing to `https://vk.com/video_ext.php?oid=...&id=...&js_api=1`, load `videoplayer.js`, init `VK.VideoPlayer`, and handle the events.
- Update Admin components to support `vk`.
- Update `SecureVideoPlayer.tsx` to handle seeking and play/pause for `vk` if its implementation requires differences, but ideally, `postMessage` makes it identical to YouTube.
