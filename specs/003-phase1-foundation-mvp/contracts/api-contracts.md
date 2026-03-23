# API Contracts: Phase 1 — Foundation and MVP Launch

> All endpoints require `Authorization: Bearer <JWT>` unless marked **PUBLIC**.  
> Standard error response: `{ "error": { "code": "ERROR_CODE", "message": "Human-readable" } }`

---

## Auth Module

### POST /api/auth/register (PUBLIC)
**Request**: `{ "phone": "string", "password": "string", "fullName": "string", "grade": "string", "track?": "string" }`
**Response 201**: `{ "userId": "uuid", "accessToken": "jwt", "refreshToken": "string" }`
**Errors**: `PHONE_ALREADY_EXISTS` (409), `VALIDATION_ERROR` (400)

### POST /api/auth/login (PUBLIC)
**Request**: `{ "phone": "string", "password": "string", "deviceFingerprint": "string" }`
**Response 200**: `{ "accessToken": "jwt", "refreshToken": "string", "registrationStep": 1|2 }`
**Errors**: `INVALID_CREDENTIALS` (401), `DEVICE_LIMIT_REACHED` (403), `ACCOUNT_DISABLED` (403)

### POST /api/auth/refresh (PUBLIC)
**Request**: `{ "refreshToken": "string" }`
**Response 200**: `{ "accessToken": "jwt", "refreshToken": "string" }`
**Errors**: `TOKEN_EXPIRED` (401), `TOKEN_REVOKED` (401)

### POST /api/auth/logout
**Response 204**: No content. Revokes refresh token.

### PUT /api/auth/complete-profile
**Request**: `{ "parentPhone": "string", "governorate": "string", "cityDistrict": "string", "schoolName": "string" }`
**Response 200**: `{ "registrationStep": 2 }`
**Errors**: `VALIDATION_ERROR` (400)

---

## Users & Devices Module (Admin)

### GET /api/admin/users?page&size&role&search
**Response 200**: Paginated list of users with roles.

### GET /api/admin/users/:userId/devices
**Response 200**: `{ "devices": [{ "id": "uuid", "friendlyName": "string", "lastUsedAt": "datetime" }], "maxAllowed": 2 }`

### DELETE /api/admin/users/:userId/devices/:deviceId
**Response 204**: Device removed. Generates audit log.

### PATCH /api/admin/users/:userId/status
**Request**: `{ "isActive": boolean }`
**Response 200**: Updated user.

---

## Content Module

### GET /api/packages (PUBLIC listing, details require auth)
**Response 200**: Array of published packages with section counts.

### GET /api/packages/:packageId/sections
**Response 200**: Array of content sections under the package.

### GET /api/sections/:sectionId/lessons
**Response 200**: Array of lessons with lock/unlock status for the current student.

### GET /api/lessons/:lessonId
**Response 200**: Full lesson detail: summary, videos (with watch limit status), resources, linked exam ID.
**Errors**: `ACCESS_DENIED` (403), `LESSON_LOCKED` (403)

### Admin CRUD: POST/PUT/DELETE for /api/admin/packages, /api/admin/sections, /api/admin/lessons, /api/admin/videos, /api/admin/resources
Standard CRUD. All generate audit log entries.

---

## Video Tracking Module

### POST /api/tracking/video-event
**Request**: `{ "lessonVideoId": "uuid", "eventType": "STARTED|HEARTBEAT|COMPLETED|PAUSED", "watchSeconds": number, "playbackSpeed": number, "replayNumber": number }`
**Response 200**: `{ "totalWatched": number, "maxAllowed": number, "isLocked": boolean }`
**Errors**: `VIDEO_LOCKED` (403)

### PUT /api/admin/videos/:videoId/students/:studentId/reset-watch
**Response 204**: Resets watch limit for student. Generates audit log.

---

## Code Engine Module

### POST /api/admin/code-groups
**Request**: `{ "name": "string", "codeType": "PACKAGE|LESSON", "targetId": "uuid", "totalCount": number, "validityDays?": number }`
**Response 202**: `{ "codeGroupId": "uuid", "status": "GENERATING" }` (Job pushed to BullMQ queue)

### GET /api/admin/code-groups/:groupId/codes?page&size
**Response 200**: Paginated list of codes (with status). Supports CSV export via `Accept: text/csv`.

### POST /api/codes/activate
**Request**: `{ "code": "string" }`
**Response 200**: `{ "grantType": "PACKAGE|LESSON", "targetName": "string", "expiresAt": "datetime" }`
**Errors**: `INVALID_CODE` (404), `CODE_ALREADY_USED` (409), `PROFILE_INCOMPLETE` (400 — triggers Step 2)

---

## Exam Module

### GET /api/exams/:examId/start
**Response 200**: `{ "attemptId": "uuid", "questions": [{ "id": "uuid", "text": "string", "options": [...] }], "timeLimitMinutes?": number }`
**Errors**: `EXAM_NOT_AVAILABLE` (403), `LESSON_NOT_UNLOCKED` (403)

### POST /api/exams/:examId/submit
**Request**: `{ "attemptId": "uuid", "answers": [{ "questionId": "uuid", "selectedOptionId": "uuid" }] }`
**Response 200**: `{ "score": number, "passed": boolean, "threshold": number, "correctCount": number, "totalCount": number }`

### PUT /api/admin/lessons/:lessonId/students/:studentId/unlock
**Response 204**: Manually unlocks next lesson. Generates audit log.

---

## Student Dashboard Module

### GET /api/student/dashboard
**Response 200**: `{ "activePackages": [...], "currentLesson": {...}, "upcomingExams": [...], "progressPercentage": number, "recentCodes": [...], "notifications": [...] }`

### GET /api/student/progress
**Response 200**: Array of lesson progress records for the current student.
