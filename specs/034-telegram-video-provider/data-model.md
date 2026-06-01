# Data Model: Telegram Video Provider

## Existing Entities Modified

### 1. `LessonVideo`

- **Field `Provider`**: Valid values are now `"youtube"` or `"telegram"`.
- **Field `ProviderVideoId`**:
  - For YouTube: Contains the literal ID (e.g., `dQw4w9WgXcQ`).
  - For Telegram: Contains the full channel/post path (e.g., `channelname/123`). This is extracted from `https://t.me/channelname/123`.

## New Services

### `TelegramVideoProvider` (Implements `IVideoProvider`)

This is a backend component that implements extraction:

- `ExtractVideoId(string url)`: Parses `https://t.me/channel/123` and returns `channel/123`.
- `GetEmbedUrl(string videoId)`: For Telegram, it might fetch the HTML from `https://t.me/channel/123?embed=1`, parse the `<video src="...">`, and return the direct MP4 URL, or pass the request logic to the frontend embed API.

Wait, if we pass the `channel/123` to the frontend, the frontend Next.js API can fetch the MP4 url securely, encrypt the video tag, and give it to the user. This saves backend processing!
Actually, `VideoPlaybackSession` shouldn't care. It stores `Provider` and `ProviderVideoId`.
The `EmbedProxy` in the frontend will fetch the actual file location.

So the domain model remains completely unchanged structurally, just new logic added to implementations!
