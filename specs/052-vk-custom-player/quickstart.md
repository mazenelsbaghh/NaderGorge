# Quickstart: VK Custom Video Player

## Goal
Implement a fully branded video playback experience for videos hosted on VKontakte (VK), hiding the native VK controls and using our own React `PlayerControls`.

## Architecture Flow

1. **Admin Setup**: Admin chooses "VK" in the `AddVideoForm` and enters the VK URL or unique identifier configuration. (e.g. `oid=-22822305&id=456241864`). This is saved as `ProviderVideoId`.
2. **Session Creation**: Student opens a lesson. `SecureVideoPlayer` requests a session token. The backend creates an AES-encrypted token containing the VK `ProviderVideoId` and `Provider` = "vk".
3. **Embed Routing**: React sets the iframe `src` to `/api/video/embed?t=<token>&k=<key>`.
4. **Server Rendering HTML**: 
   - `route.ts` decrypts the token. Seeing `provider === 'vk'`, it generates a specialized HTML document.
   - The document contains the VK iframe `<iframe id="vk-player" src="https://vk.com/video_ext.php?oid=...&id=...&js_api=1" ...></iframe>`.
   - The document loads the `VK.VideoPlayer` API script.
   - It attaches event listeners and forwards `ready`, `stateChange`, and `timeUpdate` events back to `SecureVideoPlayer` via `window.parent.postMessage`.
5. **UI Rendering**: The `SecureVideoPlayer` natively interprets these `postMessage` events identically to YouTube, updating its custom control bar, the progress tracker, and ensuring chapters are synced.

## Development Checklist

1. Review `frontend/src/app/api/video/embed/route.ts` and add a new template generator `generateVkEmbedHtml`.
2. Inside the HTML template, implement the `VK` iframe API bridging to `postMessage()`.
3. Update `frontend/src/components/admin/AddVideoForm.tsx` to handle "VK" and parse its IDs (specifically detecting VK URLs and defaulting to `vk` provider).
4. Verify custom events like `seekTime`, `play`, `pause` translate accurately from our external controls.
