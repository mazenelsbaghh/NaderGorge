# Phase 0: Research & Technical Context

## Technical Requirements for Telegram Video Provider

### 1. Telegram Bot API for Files

**Decision**: Use the Telegram Bot API `getFile` method to retrieve the CDN URL for a given `file_id`.
**Rationale**: Telegram provides a straightforward API endpoint: `https://api.telegram.org/bot<token>/getFile?file_id=<file_id>`. This returns a `file_path`. The actual file can then be downloaded from `https://api.telegram.org/file/bot<token>/<file_path>`.
**Alternative**: MTProto clients (TDLib). Rejected because it requires complex state management, session handling, and C++ bindings. The simple HTTP Bot API is sufficient for files under 20MB, but wait — video files are usually large. 
**Important Constraint**: The standard Telegram Bot API has a download limit of 20MB. To support larger files (up to 2GB), we MUST use a Local Bot API Server. 
*Correction*: Actually, public CDN URLs for public channels can be derived differently, or if we use a local bot API server, we can get larger files.
**Revised Approach for V1**: The spec says "public channel post or private file link" and the admin pastes `https://t.me/channel/postid`. Wait, if they paste a post link, grabbing the video directly without MTProto or a bot as a channel admin is tricky.
Wait, can a bot read a public channel post without being an admin? Yes, if it's a public channel, but you need to resolve the message to a `file_id`.
Actually, if the admin pastes `https://t.me/channel/postid`, how does the server get the `file_id`?
Using MTProto or web scraping. Web scraping the `https://t.me/channel/postid?embed=1` page reveals a `<video>` tag with a direct CDN URL! 
Let's verify this. If you open a public telegram post embed like `https://t.me/durov/1?embed=1`, there is a `<video>` tag with `src` pointing to a Telegram CDN (e.g., `https://cdn4.cdn-telegram.org/...`).
This means we do NOT need a bot token for public channel links if we just scrape the embed URL.
**Final Decision for Telegram URLs**: The backend will fetch `https://t.me/channelname/postid?embed=1`, parse the HTML using Regex/HtmlAgilityPack to extract the `<video src="...">` URL, and serve that to the frontend.
**Why**: 
1. No bot token required.
2. No 20MB limit.
3. No local Bot API server needed.
4. Exactly matches the spec (Public channels only).

### 2. Provider Abstraction in Backend

**Decision**: Enhance `IVideoProvider` or the session generation logic. Wait, currently `VideoPlaybackSession` creates a token. The token embeds the `Provider` and `ProviderVideoId`.
**Rationale**: In `CreateVideoSessionCommand`, we encrypt the video details and send them to the frontend. The frontend calls `/api/video/embed?t=...`. 
The Next.js `/api/video/embed` route decrypts the token. If `Provider == "telegram"`, it should output a standard HTML5 `<video>` player instead of the YouTube iframe.

### 3. Frontend Embed HTML

**Decision**: The `/api/video/embed` route will generate HTML containing a standard `<video>` tag with `controlsList="nodownload"` and CSS to hide native controls, while sending/receiving `postMessage` events identically to the YouTube iframe API wrapper.
**Rationale**: `SecureVideoPlayer` expects `postToParent` messages like `ready`, `stateChange`, `timeUpdate`. We will write a small JS wrapper for the `<video>` element inside the embed HTML that mimics the YouTube Iframe API events, so `SecureVideoPlayer.tsx` doesn't need to change its event handling!

## Constitution Validation

All approaches adhere to the constitution:
- **Provider Abstraction First**: Extending the existing provider field.
- **Security**: Telegram links are still hidden behind the encrypted token. The frontend gets a proxy token. The embed route runs in a shadow DOM.
- **Premium Design**: The native `<video>` tag will be hidden behind our existing `PlayerControls`. 
