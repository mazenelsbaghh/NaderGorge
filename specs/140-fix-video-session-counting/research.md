# Research: Session-Safe Video View Counting

## Decision 1: Playback session is the server-side counting boundary

**Decision**: Require the existing `VideoPlaybackSession.Id` on every supported student progress update and persist whether that session has registered its one permitted view.

**Rationale**: The current aggregate knows only lifetime seconds/count, so it cannot distinguish seeking/continued playback in one page from a refresh that intentionally starts a new eligible session. Server persistence is required because client-only `viewTracked` can be reset or bypassed.

**Alternatives considered**:

- Client-only flag: rejected because refresh/tampering and duplicate requests bypass it.
- Time-window heuristic on `VideoWatchEvent.UpdatedAt`: rejected because it cannot reliably identify refreshes or tabs.
- New independent watch-session aggregate: rejected because the existing playback-session entity already carries user/video identity and expiry.

## Decision 2: Every successful refresh/open creates a new newest session

**Decision**: Stop reusing active sessions. Mark prior active sessions for the same user/video superseded and retain them for deterministic stale-request errors.

**Rationale**: Product behavior explicitly makes refresh/reopen eligible for the next view and makes the newest concurrent session authoritative.

**Alternatives considered**:

- Reuse the latest unconsumed session: current behavior, rejected because refresh would remain in the already-counted session.
- Delete older sessions: rejected because delayed requests could become indistinguishable from unknown/forged IDs and audit/debug evidence is lost.

## Decision 3: Embed consumption and progress lifecycle remain separate

**Decision**: Keep `IsConsumed` as one-time secret/embed-material consumption. It does not invalidate later progress. Use explicit `IsSuperseded`, expiry, and `HasRegisteredView` for tracking decisions.

**Rationale**: The frontend consumes the session immediately after iframe load, before periodic progress begins. Treating consumption as tracking completion would reject all legitimate progress.

**Alternatives considered**: Delay consumption until playback ends, rejected because it weakens the existing anti-reuse protection and does not model refresh/concurrency.

## Decision 4: Monotonic per-session progress sequence provides idempotency

**Decision**: Add `LastProgressSequence` to the session. Each client flush sends a positive sequence and retries the same sequence/delta until success. Already accepted or older sequences return success without applying seconds again.

**Rationale**: HTTP retry, visibility/unload flushes, and concurrent React effects can repeat a delta. A stable sequence is smaller and more auditable than storing every request ID.

**Alternatives considered**:

- Random idempotency-key row per update: reliable but adds an unbounded table and cleanup policy for a 10-second heartbeat.
- Timestamp only: clock differences and collisions make it unsafe.
- Ignore duplicate protection: rejected by the approved acceptance criteria.

## Decision 5: Discard excess at the first threshold crossed by a session

**Decision**: Cap accepted seconds to the remaining amount needed for the next view. Once crossed, set `HasRegisteredView`; discard the update's excess and all later contributions from that session.

**Rationale**: This directly enforces one view per session. It preserves pre-threshold progress across refreshes while preventing the tail of the counted session from seeding another view.

**Alternatives considered**:

- Preserve excess for the next session: rejected because that time occurred after the current session already consumed its allowed view.
- Apply the whole delta then prevent count increment: rejected because cumulative seconds would cause an immediate count on the next refresh.

## Decision 6: Renew valid active sessions in place

**Decision**: A valid newest-session update extends `ExpiresAt` by five minutes and records `LastProgressAt`; it does not reset counting eligibility.

**Rationale**: This is the clarified product behavior for long videos and retains expiry for abandoned sessions.

**Alternatives considered**: Hard expiry or automatic replacement were rejected because both interrupt legitimate long playback or risk granting new eligibility.

## Decision 7: Sessionless legacy tracking cannot mutate watch state

**Decision**: Keep `/api/tracking/video-event` temporarily but return `SESSION_REQUIRED`; all actual student player counting goes through the session-aware endpoint.

**Rationale**: Allowing a second handler to update `VideoWatchEvent` without a session would bypass every new invariant. Repository search shows the secure player already uses `video-session-service.ts`; the legacy service method has no active player caller.

**Alternatives considered**: Add a synthetic session server-side, rejected because the server cannot know the browser session boundary or retry identity from the old payload.

## Decision 8: PostgreSQL transaction remains aggregate consistency boundary

**Decision**: Validate newest session, sequence, session state, and update both session/watch aggregate in one serializable transaction. Retry uses the same sequence after serialization conflicts.

**Rationale**: The repository already uses serializable transactions for watch updates. Persisted sequence makes retries safe and both rows commit atomically.

**Alternatives considered**:

- Frontend locking: rejected because multiple devices bypass it.
- Distributed Redis lock: unnecessary additional dependency for state already owned by PostgreSQL.
- Separate commits: rejected because session may be marked accepted without applying watch state or vice versa.

## Decision 9: Focused superseded-player recovery UI

**Decision**: HTTP 409 plus `SESSION_SUPERSEDED` stops tracking/playback, shows an Arabic explanation, and offers reload. Other invalid/expired session codes use recoverable error UI.

**Rationale**: The behavior was explicitly approved and avoids silently playing uncounted content.

**Alternatives considered**: Silent no-op and warning-only playback were rejected by the user.

## Deployment and migration risk

The additive migration is backward-compatible at the schema level. Backend must deploy before frontend; old clients become read-only for watch progress through `SESSION_REQUIRED` until reload/new frontend. No worker, provider, pricing, entitlement, or storage integration changes are required.
