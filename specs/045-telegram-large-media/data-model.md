# Data Model Documentation

## Entity Changes

No new database entities or schema modifications are required for this feature. The fix involves altering the runtime execution of an existing HTTP API route.

## Operational Contracts

The `embedUrl` logic inside the Next.js `generateTelegramEmbedHtml` function will no longer wrap the extracted `videoSrc` with the internal `/api/video/stream-proxy?t=...` proxy utility.

Instead, the `videoUrl` passed into the `generateTelegramPlayerWrapper` will directly be the Telegram CDN URL. All encryption definitions (`crypto.createCipheriv` and the `encryptProxyUrl` function) will remain intact if needed elsewhere, or removed if `/api/video/stream-proxy` is fully deprecated. No data payload definitions, response types, or DTOs need explicit changes for the frontend proxy bypass.
