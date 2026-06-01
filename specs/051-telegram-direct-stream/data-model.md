# Data Model: Telegram Direct Stream

## Entities

### 1. `LessonVideo` (Existing)
*   **Modifications**: 
    *   The `Provider` ENUM / string must stop accepting `"okru"` and start accepting `"telegram"`.
    *   The `VideoId` field will store the FULL redirectable URL provided by the Telegram Direct Link Bot (e.g., `https://some-bot-domain.com/dl/...`).

## Logic Changes

### C# Domain Changes
*   Remove `OkVideoProvider.cs`.
*   If `TelegramVideoProvider.cs` does not exist, create it to validate and return the Telegram VideoId cleanly.

### Next.js API Routes (Proxy)
*   `GET /api/video/stream-proxy?token=...`
    *   **Input**: Decrypts the token to get the `VideoId` (which is the direct bot URL).
    *   **Logic**: Performs a `fetch(botUrl, { redirect: 'manual' })` (or similar HTTP library call without following redirects).
    *   **Output**: Extracts the `Location` header and returns a Next.js `302 Found` with the `Location` set to the Telegram CDN endpoint.
    *   **Fallback**: If the bot directly streams, we pipe the stream (ideally not needed if the bot provides a correct 302).
