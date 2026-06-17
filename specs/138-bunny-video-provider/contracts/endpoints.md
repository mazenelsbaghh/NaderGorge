# API Contracts: Bunny Video Provider

All endpoint paths are relative to the existing backend API base.

## Existing Video Create/Edit

### POST `/admin/content/lessons/{lessonId}/videos`

Extends existing create video command.

**Request changes**:

```json
{
  "title": "Lesson intro",
  "provider": "bunny",
  "urlOrEmbedCode": "bunny-video-guid-or-player-url",
  "order": 1,
  "limit": 3,
  "isActive": true
}
```

**Behavior**:
- Accepts `YouTube`, `vk`, and `bunny`.
- For Bunny manual link, validates a Bunny GUID or supported Bunny player URL.
- Does not require upload metadata unless creating through Bunny upload endpoints.

### PUT `/admin/videos/{id}`

Same provider acceptance rules as create.

## Bunny Uploads

### POST `/admin/bunny/uploads/tus`

Creates a Bunny video, local `LessonVideo`, local `BunnyVideoAsset`, and returns signed TUS upload instructions.

**Authorization**: Admin, Teacher. Teacher must own/manage selected lesson.

**Request**:

```json
{
  "teacherId": "00000000-0000-0000-0000-000000000000",
  "packageId": "00000000-0000-0000-0000-000000000000",
  "lessonId": "00000000-0000-0000-0000-000000000000",
  "title": "Chapter 1",
  "order": 1,
  "maxWatchCount": 3,
  "fileName": "chapter-1.mp4",
  "fileSizeBytes": 123456789
}
```

**Teacher behavior**:
- `teacherId` may be omitted or ignored and derived from the authenticated teacher.
- `packageId`/`lessonId` must belong to that teacher.

**Admin behavior**:
- `teacherId`, `packageId`, and `lessonId` are required.

**Response**:

```json
{
  "lessonVideoId": "00000000-0000-0000-0000-000000000000",
  "bunnyVideoAssetId": "00000000-0000-0000-0000-000000000000",
  "bunnyVideoGuid": "bunny-guid",
  "libraryId": 123,
  "tusEndpoint": "https://video.bunnycdn.com/tusupload",
  "authorizationSignature": "sha256-signature",
  "authorizationExpire": 1781725200,
  "uploadHeaders": {
    "AuthorizationSignature": "sha256-signature",
    "AuthorizationExpire": "1781725200",
    "LibraryId": "123",
    "VideoId": "bunny-guid"
  },
  "status": "Created"
}
```

### POST `/admin/bunny/uploads/{assetId}/complete`

Marks a TUS upload as uploaded and refreshes Bunny status.

**Authorization**: Admin or owning teacher.

**Response**:

```json
{
  "assetId": "00000000-0000-0000-0000-000000000000",
  "lessonVideoId": "00000000-0000-0000-0000-000000000000",
  "status": "Processing",
  "encodeProgress": 0
}
```

### POST `/admin/bunny/uploads/fetch`

Requests Bunny to fetch a remote video URL and creates/links the resulting Bunny asset when a GUID is resolved.

**Authorization**: Admin, Teacher. Same ownership rules as TUS.

**Request**:

```json
{
  "teacherId": "00000000-0000-0000-0000-000000000000",
  "packageId": "00000000-0000-0000-0000-000000000000",
  "lessonId": "00000000-0000-0000-0000-000000000000",
  "title": "Remote lesson",
  "order": 2,
  "maxWatchCount": 3,
  "sourceUrl": "https://example.com/video.mp4"
}
```

**Response**:

```json
{
  "bunnyVideoAssetId": "00000000-0000-0000-0000-000000000000",
  "lessonVideoId": "00000000-0000-0000-0000-000000000000",
  "status": "Processing",
  "message": "Bunny fetch accepted"
}
```

**Failure rule**: If no Bunny video GUID can be resolved, response must not create a playable lesson video.

### POST `/admin/bunny/videos/{assetId}/refresh-status`

Refreshes Bunny processing status, duration, size, progress, and error details.

**Authorization**: Admin or owning teacher. Teacher response excludes cost fields.

## Cost Settings

### GET `/admin/settings`

Includes admin-editable Bunny pricing settings for admins:

```json
{
  "BunnyStreamStorageRateUsdPerGb": "0.01",
  "BunnyStreamBandwidthRateUsdPerGb": "0.005"
}
```

Teacher settings responses must not expose billing settings unless the existing settings endpoint is already admin-only.

## Cost Reports

### POST `/admin/bunny/usage/sync`

Creates or refreshes monthly snapshots.

**Authorization**: Admin only.

**Request**:

```json
{
  "periodStart": "2026-06-01T00:00:00Z",
  "periodEnd": "2026-07-01T00:00:00Z",
  "teacherId": null,
  "packageId": null,
  "forceRefresh": false
}
```

**Response**:

```json
{
  "periodStart": "2026-06-01T00:00:00Z",
  "periodEnd": "2026-07-01T00:00:00Z",
  "snapshotsCreated": 12,
  "snapshotsUpdated": 2,
  "estimatedBandwidthCount": 14,
  "storageRateUsdPerGb": 0.01,
  "bandwidthRateUsdPerGb": 0.005
}
```

### GET `/admin/bunny/reports/costs?month=2026-06&teacherId=&packageId=`

Returns admin-only monthly cost report.

**Authorization**: Admin only.

**Response**:

```json
{
  "periodStart": "2026-06-01T00:00:00Z",
  "periodEnd": "2026-07-01T00:00:00Z",
  "platformTotalCostUsd": 12.34,
  "platformStorageBytes": 9876543210,
  "platformBandwidthBytes": 1234567890,
  "estimatedBandwidthCount": 4,
  "videos": [
    {
      "lessonVideoId": "00000000-0000-0000-0000-000000000000",
      "title": "Chapter 1",
      "teacherId": "00000000-0000-0000-0000-000000000000",
      "packageId": "00000000-0000-0000-0000-000000000000",
      "storageBytes": 1000,
      "bandwidthBytes": 2000,
      "totalCostUsd": 0.01,
      "isBandwidthEstimated": true
    }
  ],
  "teachers": [],
  "packages": []
}
```

## Protected Playback

### GET `/api/video/embed?s={sessionId}` (Next App Router)

Extends existing provider branch:
- `vk`: current VK player behavior.
- `youtube`: current YouTube player behavior.
- `bunny`: generate Bunny iframe using `https://player.mediadelivery.net/embed/{libraryId}/{videoId}`.

**Security**:
- The route still requires valid video session embed material.
- It does not expose Bunny API keys or cost data.
