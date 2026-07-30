# Contract: Upload And Asset Security

## Admin Resource Upload

**Endpoint**: `POST /api/admin/resources/upload`  
**Auth**: authenticated user with `content.manage` permission  
**Input**: multipart form field `file`, max 10 MB  
**Accepted categories**:

- Images decoded by trusted image parser and converted/stored as safe public image only when the endpoint is explicitly for public images.
- Documents/resources: PDF, DOC/DOCX, XLS/XLSX, ZIP when extension and magic bytes match.

**Required behavior**:

- Reject empty files, files >10 MB, unsupported extension, unsupported magic bytes, extension/signature mismatch, path traversal names, HTML/SVG/script-capable content.
- Store resource uploads outside public direct static exposure, or under a protected path that is reachable only through the signed resource download controller.
- Response returns `{ Success: true, Data: { Url: "<logical protected path>" } }` or equivalent existing `ApiResponse`.
- Failure returns 400 with stable user-safe message.

## Public Resource Download

**Endpoint**: `GET /api/public/resources/{resourceId}/download?token=...`  
**Auth**: signed short-lived token, existing behavior  
**Required behavior**:

- Missing/invalid/expired token returns 400/401-style failure without file bytes.
- Path resolution rejects absolute paths and traversal.
- Protected resources are sent as attachment:
  - `Content-Disposition: attachment; filename="<sanitized filename>"`
  - `Content-Type: application/octet-stream` unless a safe explicit type is required.
- Docker/Nginx path uses `X-Accel-Redirect` only for protected internal locations.
- Direct static access to the same protected file path must not expose bytes.

## Content Image Uploads

**Endpoints**:

- `POST /api/admin/content/{contentType}/{id}/image`
- `POST /api/admin/questions/image`
- `POST /api/admin/sales/templates/background-image`

**Required behavior**:

- Do not trust client `ContentType` alone.
- Decode image bytes with trusted parser before accepting.
- Convert accepted images to generated `.webp` or preserve only known safe raster formats where existing behavior requires it.
- Reject SVG/HTML/unknown image-like payloads.

## Live Support Attachments

**Endpoints**:

- `POST /api/live-support/participant/conversations/{conversationId}/attachments`
- `GET /api/live-support/participant/conversations/{conversationId}/attachments/{attachmentId}`

**Required behavior**:

- Upload requires resolved participant identity.
- Store outside public web root.
- Normalize filename, detected content type, size, and hash.
- Reject unsafe browser-interpretable content or force attachment-safe response.
- Download requires participant authorization and returns no private bytes for unrelated participants.
