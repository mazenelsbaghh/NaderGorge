# Contracts

## `API /api/video/embed` (Next.js route)

**Updates:**
When decoding the token:
1. `Provider` determines the template.
2. If `Provider === 'youtube'`, inject YouTube iframe wrapper (current behavior).
3. If `Provider === 'telegram'`:
   - Makes a server-side fetch to `https://t.me/${VideoId}?embed=1`.
   - Parses the HTML to extract the actual CDN `<video src="...">`.
   - Generates an HTML document that injects a standard `<video>` element with the CDN URL.
   - Includes custom Javascript that hooks into `<video>` events (`play`, `pause`, `timeupdate`, `ended`, `error`).
   - Adapts these `<video>` events into `postToParent` messages mimicking the YouTube iframe events (`ready`, `stateChange`, `timeUpdate`) so `SecureVideoPlayer.tsx` behaves seamlessly without modifications!

## `API /admin/packages/{packageId}/lessons/{lessonId}/videos` (Admin Endpoint)

**Updates:**
- Payload now strictly allows `"telegram"` as a provider.
- Admin passes the full URL `https://t.me/channel/123` into the `ProviderVideoId` input.
- The backend `TelegramVideoProvider.ExtractVideoId` normalizes it to just `channel/123` and saves it!
