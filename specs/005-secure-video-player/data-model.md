# Data Model: Secure Video Player

**Feature**: 005-secure-video-player
**Date**: 2026-03-25

---

## Existing Entities (No Changes Required)

### LessonVideo (existing)
| Field            | Type     | Notes                           |
|------------------|----------|---------------------------------|
| Id               | Guid     | PK                              |
| Title            | string   | Video title                     |
| Provider         | string   | "youtube", "vimeo", etc.        |
| ProviderVideoId  | string   | YouTube video ID (e.g., "dQw4w9WgXcQ") |
| Order            | int      | Display order within lesson     |
| MaxWatchCount    | int      | Hard-lock limit (default: 3)    |
| LessonId         | Guid     | FK → Lesson                     |

### VideoWatchEvent (existing)
| Field                | Type     | Notes                           |
|----------------------|----------|---------------------------------|
| Id                   | Guid     | PK                              |
| UserId               | Guid     | FK → User                       |
| LessonVideoId        | Guid     | FK → LessonVideo                |
| TimeWatchedInSeconds | int      | Cumulative watch time           |
| WatchCount           | int      | Number of distinct watch sessions |
| IsLocked             | bool     | Whether limit has been reached  |

---

## New Entity

### VideoPlaybackSession
| Field          | Type      | Notes                                         |
|----------------|-----------|-----------------------------------------------|
| Id             | Guid      | PK                                            |
| UserId         | Guid      | FK → User                                     |
| LessonVideoId  | Guid     | FK → LessonVideo                              |
| SessionToken   | string    | Encrypted, short-lived token                  |
| EncryptionKey  | string    | Session-specific key for decryption           |
| CreatedAt      | DateTime  | When the session was created                  |
| ExpiresAt      | DateTime  | CreatedAt + 5 minutes                         |
| IsConsumed     | bool      | Whether the token has been used               |
| IpAddress      | string?   | Client IP for audit/security                  |

**Validation Rules**:
- Token expires after 5 minutes (`ExpiresAt = CreatedAt + 5 min`)
- Token can only be consumed once (`IsConsumed` flag)
- User must have access to the lesson's package
- `WatchCount < MaxWatchCount` must be satisfied

**State Transitions**:
```
Created → Consumed (player loaded successfully)
Created → Expired (5 min elapsed without consumption)
```

---

## Relationships

```
User (1) ──→ (N) VideoPlaybackSession
LessonVideo (1) ──→ (N) VideoPlaybackSession
LessonVideo (1) ──→ (N) VideoWatchEvent
User (1) ──→ (N) VideoWatchEvent
```
