# Implementation Quickstart: Dynamic Watermark

Follow these steps to effectively implement the dynamic moving watermark for videos:

## 1. Backend Updates (Next.js & C# Payload)

### Update `IVideoEncryptionService.cs`
- Modify `EncryptVideoInfo(string providerName, string providerVideoId, string sessionKey, string studentName, string studentPhone)`.
- Ensure all calls to this method inject `user.Name` and `user.PhoneNumber` into the anonymous object before JSON serialization.

### Update `CreateVideoSessionCommand.cs`
- Fetch the student's name and phone number querying the database or from their JWT claims/Identity.
- Pass it to the Encryption service when building `SessionToken`.

## 2. Frontend Embed Updates (`route.ts`)

- Inside `embed/route.ts`, parse the newly decrypted JSON fields.
- Modify the `generateTelegramPlayerWrapper` and `generateEmbedHtml` helper strings to accept two new parameters: `studentName` & `studentPhone`.

## 3. Watermark DOM Logic (Inside IFrame HTML)

Inject this specific CSS block and structure into your `embed/route.ts` HTML template:

```html
<style>
  .watermark {
    position: absolute;
    z-index: 99; /* Below the click-overlay but above video */
    pointer-events: none; /* Crucial: Do not block clicks */
    color: rgba(255, 255, 255, 0.2); /* 20% opacity */
    font-size: 1.5rem;
    font-family: Arial, sans-serif;
    text-shadow: 1px 1px 3px rgba(0, 0, 0, 0.4);
    user-select: none;
    transition: all 1.5s ease-in-out; /* Smooth moving */
    text-align: center;
  }
</style>

<div class="watermark" id="video-watermark">
  [Student Name]<br>
  [Phone Number]
</div>
```

Inject this JavaScript interval script next to the YouTube or Telegram initializing scripts:
```javascript
const watermark = document.getElementById('video-watermark');
// Ensure it's not destroyed by domKiller: add "video-watermark" to `allowedIds`
setInterval(() => {
    if (!watermark) return;
    // Keep it padded by 10% on edges preventing off-screen clipping
    const top = Math.random() * 80 + 10; 
    const left = Math.random() * 80 + 10;
    watermark.style.top = top + '%';
    watermark.style.left = left + '%';
}, 12000); // 12 seconds
```

## 4. Shield Constraints
- Add `video-watermark` to the `allowedIds` array in the `domKiller` inside `embed/route.ts` so it doesn't accidentally vaporize our own watermark!
