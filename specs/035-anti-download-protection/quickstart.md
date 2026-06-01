# Anti-Download Implementation Quickstart

When building the frontend proxy logic:

1. Look inside `frontend/src/app/api/video/embed/route.ts` where the shadow DOM script is constructed.
2. Locate the line: `video.src = '${videoUrl}';`
3. Just below it, attach an event listener to strip the `src`:
   ```javascript
   video.addEventListener('loadstart', function() {
       video.removeAttribute('src'); // IDM Killer
   });
   ```
4. Verify `pointer-events: none;` is strictly applied.
5. In `frontend/src/app/api/video/stream-proxy/route.ts`, verify that `request.headers.get('sec-fetch-dest')` fails fast on `'document'`, and effectively kills the connection if IDM tries to sniff the response header payloads to guess the raw Telegram `cdnUrl`.
