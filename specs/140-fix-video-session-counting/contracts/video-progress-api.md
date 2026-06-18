# Contract: Session-Aware Video Progress

## Create playback session

`POST /api/student/video-session`

Request remains:

```json
{ "lessonVideoId": "uuid" }
```

Each successful call creates a new session and supersedes older sessions for the same authenticated student/video. Response shape remains compatible and returns the new `sessionId`.

## Track progress

`POST /api/student/video-session/{lessonVideoId}/track-progress`

Request:

```json
{
  "sessionId": "uuid",
  "progressSequence": 1,
  "secondsWatched": 10,
  "totalDurationSeconds": 600
}
```

Rules:

- `sessionId` and positive `progressSequence` are required.
- A retry sends the exact same sequence and seconds.
- A successful new sequence returns HTTP 200 and current watch state.
- An already accepted/lower sequence returns HTTP 200 with unchanged watch state and `viewRegistered: false`.
- After this session registers a view, later new sequences return HTTP 200 without adding seconds.
- Valid requests renew the same session expiry.

Success data:

```json
{
  "currentCount": 1,
  "maxCount": 3,
  "isLocked": false,
  "viewRegistered": true,
  "totalTrackedSeconds": 180,
  "thresholdSeconds": 180,
  "sessionExpiresAt": "2026-06-18T12:05:00Z"
}
```

Errors:

| HTTP | Code | Meaning | Mutation |
|---:|---|---|---|
| 400 | `DURATION_REQUIRED` | Duration is missing/non-positive. | None |
| 400 | `PROGRESS_SEQUENCE_REQUIRED` | Sequence is non-positive. | None |
| 404 | `SESSION_INVALID` | Session unknown or does not match authenticated owner/video. | None |
| 409 | `SESSION_EXPIRED` | Session expired without valid renewal. | None |
| 409 | `SESSION_SUPERSEDED` | A newer session exists. | None |
| 400 | `WATCH_LIMIT_REACHED` or existing locked success contract | Existing maximum reached; controller must preserve current player compatibility. | Lock flag repair only if needed |

Security note: ownership/video mismatch uses the same generic `SESSION_INVALID` response as an unknown ID to avoid exposing another student's session.

## Legacy sessionless endpoint

`POST /api/tracking/video-event`

Returns a non-mutating 400 response with `SESSION_REQUIRED`. It cannot update `VideoWatchEvent` because it cannot prove a playback-session boundary or update identity.

## Superseded-player UI contract

When `SESSION_SUPERSEDED` is received, the player must:

1. clear progress timers and pending seconds;
2. pause the embedded player;
3. stop retrying that session;
4. display: `تم فتح الفيديو في تبويب أو جهاز أحدث. أعد تحميل الفيديو للمتابعة هنا.`;
5. expose a reload action that creates a new session.
