# Data Model: Session-Safe Video View Counting

## VideoPlaybackSession (modified)

Existing identity and access fields remain unchanged: `Id`, `UserId`, `LessonVideoId`, `SessionToken`, `EncryptionKey`, `CreatedAt`, `ExpiresAt`, `IsConsumed`, and `IpAddress`.

New fields:

| Field | Type | Default | Purpose |
|---|---|---:|---|
| `HasRegisteredView` | boolean | `false` | Server authority that this session has already consumed its single view-registration opportunity. |
| `LastProgressSequence` | 64-bit integer | `0` | Highest accepted client progress sequence; lower/equal requests are idempotent no-ops. |
| `LastProgressAt` | nullable timestamp | `null` | Last accepted valid actual-playback activity, for renewal and diagnostics. |
| `IsSuperseded` | boolean | `false` | Marks sessions invalidated by a newer session for the same user/video. |

Indexes:

- Existing primary key on `Id`.
- Add composite index `(UserId, LessonVideoId, CreatedAt)` to resolve the newest session efficiently.
- Add supporting index `(UserId, LessonVideoId, IsSuperseded, ExpiresAt)` if query-plan inspection shows the newest-session index alone is insufficient; do not add both without evidence.

Validation/invariants:

- Session owner and video must match the authenticated request and route.
- Only the newest non-superseded session may mutate progress.
- `LastProgressSequence >= 0`.
- `HasRegisteredView` never changes from true to false during that session.
- Valid activity renews `ExpiresAt`; renewal does not change `HasRegisteredView`.
- `IsConsumed` protects embed material only and is not a progress validity condition.

State transitions:

```text
Created -> EmbedConsumed (optional flag, tracking still active)
Created/EmbedConsumed -> ActiveProgress (sequence accepted, expiry renewed)
ActiveProgress -> ViewRegistered (first threshold reached; later seconds ignored)
Any non-expired state -> Superseded (newer session created; progress rejected)
Any inactive state -> Expired (no valid renewal before ExpiresAt; progress rejected)
```

## VideoWatchEvent (unchanged schema, protected semantics)

Fields used: `UserId`, `LessonVideoId`, `TimeWatchedInSeconds`, `WatchCount`, `IsLocked`, `CustomMaxWatchCount`, timestamps.

Invariants:

- Unique logical aggregate per `(UserId, LessonVideoId)`.
- Partial accepted time remains cumulative across sequential sessions.
- A session may advance the aggregate only to the next threshold boundary.
- `WatchCount` cannot increase by more than one from a single session.
- At maximum/custom maximum, `WatchCount` is capped and `IsLocked = true`.
- Existing negative-time reset signal is normalized before boundary calculation.

## Progress Update (request value object)

| Field | Type | Rule |
|---|---|---|
| `SessionId` | UUID | Required; must belong to authenticated user and route video. |
| `ProgressSequence` | 64-bit integer | Required and positive; retry reuses the same value. |
| `SecondsWatched` | number | Non-negative; sanitized and capped by current anti-inflation rules. |
| `TotalDurationSeconds` | integer | Positive; used with configured threshold percentage. |

No standalone persistence table is added for updates; idempotency is bounded per session by `LastProgressSequence`.
