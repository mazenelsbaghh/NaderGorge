# Research: Bunny Video Provider

**Date**: 2026-06-17  
**Scope**: Official Bunny Stream API, current Bunny Stream pricing defaults, Massar provider architecture, upload strategy, usage/cost reporting, and AI compatibility.

## Decisions

### R-001: Use Bunny Stream, not generic Bunny Storage

**Decision**: Integrate Bunny through Bunny Stream.

**Rationale**: The feature requires video upload, processing, playback, and AI-adjacent video metadata. Bunny Stream exposes video library, create/upload/fetch, embed player, processing status, storage size, statistics, transcription, and smart action endpoints.

**Alternatives considered**:
- Bunny Storage only: rejected because it does not provide Stream player/transcoding workflow as the primary product feature.
- Replace YouTube/VK: rejected by explicit requirement; Bunny is additive.

### R-002: Authenticate all Bunny API calls in backend infrastructure

**Decision**: Backend uses Bunny API keys and exposes only safe upload/playback metadata to frontend.

**Rationale**: Bunny API calls require `AccessKey` authentication. TUS uploads can be authorized with a short-lived SHA256 signature generated server-side from library ID, API key, expiry, and video ID. This keeps the API key out of browser code.

**Official source**: Bunny TUS documentation specifies the TUS endpoint `https://video.bunnycdn.com/tusupload`, required upload headers, and the SHA256 signature formula.

### R-003: Local file uploads use TUS resumable upload

**Decision**: Implement local Bunny uploads with TUS resumable upload.

**Rationale**: User selected the recommended large-file flow. Bunny's HTTP API documentation recommends TUS for files over 2 GB or unreliable connections because direct PUT cannot resume.

**Implementation note**: The backend first creates a Bunny video record, stores local metadata, returns TUS upload instructions, then the frontend uploads directly to Bunny and confirms completion/status refresh with the backend.

### R-004: Remote URL uploads use Bunny Fetch Video with strict platform validation

**Decision**: Implement URL uploads through Bunny's Fetch Video endpoint and track fetch status locally.

**Rationale**: Bunny provides `POST /library/{libraryId}/videos/fetch` with URL, optional headers, title, collection ID, and response success/status. The endpoint response does not document a returned video GUID, so the platform must either create/find the resulting video deterministically or reconcile by polling/listing with a platform-generated title/meta tag where supported.

**Implementation constraint**: The first implementation must not mark a URL-fetched video as playable until the Bunny video GUID is resolved and linked locally.

### R-005: Store Bunny metadata separately from `LessonVideo`

**Decision**: Add `BunnyVideoAsset` linked one-to-one with `LessonVideo`.

**Rationale**: YouTube/VK videos only need provider and provider video ID. Bunny needs upload method, processing status, library ID, ownership, package/lesson attribution, sync timestamps, errors, size, traffic, and cost metadata.

**Alternative considered**: Nullable columns on `LessonVideo`; rejected due to schema pollution and higher risk of leaking cost fields.

### R-006: Embed Bunny playback with Bunny Stream player URL pattern

**Decision**: Generate Bunny embed HTML using Bunny Stream player URL pattern `https://player.mediadelivery.net/embed/{video_library_id}/{video_id}` inside the existing protected embed route.

**Rationale**: This keeps playback inside the current video-session/encrypted embed flow and avoids exposing Bunny credentials.

### R-007: Use admin-editable pricing settings with Bunny official defaults

**Decision**: Seed settings with Bunny Stream official defaults as of 2026-06-17: storage from `$0.01/GB`, CDN/bandwidth from `$0.005/GB`; admins can edit stored rates.

**Rationale**: User requested admin-set storage/bandwidth prices and asked us to discover real Bunny defaults from Bunny's site. Bunny pricing can vary by account, volume, replication, or future changes, so stored settings are the calculation source.

### R-008: Monthly usage/cost reports use snapshots

**Decision**: Add dated `BunnyUsageSnapshot` rows keyed by video and reporting window. Store usage bytes, rate values, calculated costs, sync source, and whether bandwidth is estimated.

**Rationale**: User required monthly filters and saved snapshots for storage, bandwidth, and cost. Snapshots preserve auditability after settings change.

### R-009: Bandwidth source is library-level unless per-video bytes become available

**Decision**: Treat storage as directly measurable per video; treat bandwidth as source-labeled snapshot data. If Bunny only exposes library-level `TrafficUsage` and per-video watch time/views, allocate per-video bandwidth proportionally by `totalWatchTime` during sync and mark it as estimated.

**Rationale**: Bunny Get Video/List Videos expose `storageSize`, `views`, `averageWatchTime`, and `totalWatchTime`. Bunny Get Video Library exposes `TrafficUsage` and `StorageUsage` at library level. The researched Stream statistics endpoint focuses on views/watch-time breakdowns, not documented per-video byte traffic. Reports therefore must preserve a `BandwidthSource`/`IsEstimated` marker.

**Risk**: Estimated per-video bandwidth is operational reporting, not an invoice-grade billing ledger. Platform-level bandwidth can use Bunny library traffic directly.

### R-010: Bunny AI integration follows existing AI workflows first

**Decision**: Keep existing platform AI pipeline as source of truth for chapters/mindmaps, and add Bunny readiness/media-access checks before enqueueing. Also record available Bunny smart actions as optional provider metadata for future enhancement.

**Rationale**: Bunny supports transcribing and smart actions including title, description, chapters, and moments, but the platform already has Gemini/worker-based chaptering and mindmap generation. Replacing that workflow would be larger and risk regressions.

**Implementation note**: If Bunny media access is unavailable, AI requests return a clear processing/unsupported state. If media access is available, existing AI jobs receive provider `bunny` and the Bunny playback/download reference needed by the worker.

## Official Bunny Sources Reviewed

- Bunny Stream API reference: `https://docs.bunny.net/api-reference/stream`
- Create Video: `https://docs.bunny.net/api-reference/stream/manage-videos/create-video`
- Upload Video: `https://docs.bunny.net/api-reference/stream/manage-videos/upload-video`
- Fetch Video: `https://docs.bunny.net/api-reference/stream/manage-videos/fetch-video`
- TUS Resumable Uploads: `https://docs.bunny.net/stream/tus-resumable-uploads`
- Embedding Videos: `https://docs.bunny.net/stream/embedding`
- Get Video: `https://docs.bunny.net/api-reference/stream/manage-videos/get-video`
- List Videos: `https://docs.bunny.net/api-reference/stream/manage-videos/list-videos`
- Get Video Storage Size Info: `https://docs.bunny.net/api-reference/stream/manage-videos/get-video-storage-size-info`
- Get Video Library: `https://docs.bunny.net/api-reference/core/stream-video-library/get-video-library`
- Trigger Smart Actions: `https://docs.bunny.net/api-reference/stream/manage-videos/trigger-smart-actions`
- Bunny Stream pricing: `https://bunny.net/pricing/stream/`
