# Anti-Download Proxy Interface

The feature exposes a frontend-to-frontend proxy API used purely by `<video src="...">` tags inside internal iframes.

## Endpoint

`GET /api/video/stream-proxy?t={encryptedCdnUrl}`

### Request Headers

Must strictly contain:
* `Sec-Fetch-Dest: video` (Browser's native media fetcher)
* `Referer` containing the origin matching the application host (e.g. `http://localhost:3000/api/video/embed...`).

### Responses

* `302 Found` with `Location` pointing to the actual CDN Telegram stream when all checks pass.
* `403 Forbidden` if missing required headers or if accessed via direct navigation (`Sec-Fetch-Dest: document`).
