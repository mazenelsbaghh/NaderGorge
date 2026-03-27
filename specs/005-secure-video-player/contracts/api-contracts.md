# API Contracts: Secure Video Player

**Feature**: 005-secure-video-player
**Date**: 2026-03-25

---

## Endpoint 1: Create Video Playback Session

Creates an encrypted, time-limited session token for video playback.

### Request
```
POST /api/student/video-session
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "lessonVideoId": "uuid"
}
```

### Response — Success (200)
```json
{
  "success": true,
  "data": {
    "sessionId": "uuid",
    "token": "base64_encrypted_blob_containing_video_provider_and_id",
    "key": "base64_session_decryption_key",
    "expiresAt": "2026-03-25T23:05:00Z",
    "provider": "youtube",
    "watchInfo": {
      "currentCount": 1,
      "maxCount": 3,
      "isLocked": false
    },
    "videoTitle": "الدرس الأول - مقدمة"
  },
  "message": null,
  "errors": null
}
```

### Response — Watch Limit Reached (400)
```json
{
  "success": false,
  "data": null,
  "message": "لقد استنفدت عدد المشاهدات المتاحة",
  "errors": ["WATCH_LIMIT_REACHED"]
}
```

### Response — No Access (403)
```json
{
  "success": false,
  "data": null,
  "message": "ليس لديك صلاحية الوصول لهذا الفيديو",
  "errors": ["ACCESS_DENIED"]
}
```

### Response — Video Not Found (404)
```json
{
  "success": false,
  "data": null,
  "message": "الفيديو غير موجود",
  "errors": ["VIDEO_NOT_FOUND"]
}
```

---

## Endpoint 2: Consume Video Session (Validate Token)

Called by the frontend after the player successfully loads. Marks the token as consumed.

### Request
```
POST /api/student/video-session/{sessionId}/consume
Authorization: Bearer {jwt_token}
```

### Response — Success (200)
```json
{
  "success": true,
  "message": "Session consumed successfully"
}
```

---

## Modified Endpoint: Get Lesson Detail

The existing `GET /api/content/lessons/{lessonId}` response is modified:

### Before (current — INSECURE)
```json
{
  "videos": [
    {
      "id": "uuid",
      "title": "Lesson Video",
      "provider": "youtube",
      "embedUrl": "https://www.youtube.com/embed/VIDEO_ID",
      "order": 1,
      "limit": 3,
      "watched": 1,
      "isLocked": false
    }
  ]
}
```

### After (SECURE)
```json
{
  "videos": [
    {
      "id": "uuid",
      "title": "الدرس الأول",
      "provider": "youtube",
      "order": 1,
      "limit": 3,
      "watched": 1,
      "isLocked": false
    }
  ]
}
```

**Key Change**: `embedUrl` field is **removed**. The frontend must call the video session endpoint to get an encrypted token instead.

---

## Frontend Token Flow

```
1. Frontend renders lesson page without video URLs
2. Student clicks "Play" → frontend calls POST /api/student/video-session
3. Backend validates auth + access + watch limit → returns encrypted token + key
4. Frontend decrypts token → extracts video ID
5. Frontend dynamically creates YouTube IFrame player
6. Player loads → frontend calls POST /video-session/{id}/consume
7. Frontend clears decrypted video ID from memory + obfuscates DOM
8. After 30s of playback → frontend calls POST /api/tracking/video-event (existing)
```
