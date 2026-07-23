# Research: Employee Workflows and Realtime Refresh

## Existing architecture evidence

- `StaffRealtimeChangeDetector` already maps many EF entities to broad scopes and creates a `StaffDataChanged` outbox event, but its payload currently contains only `scopes` and no event ID, operation, entity IDs, or version.
- `OutboxProcessorBackgroundService` dispatches `Role_Staff` group events from PostgreSQL outbox rows with retries and `FOR UPDATE SKIP LOCKED`; changing the event envelope is compatible if old fields remain readable.
- `usePlatformEvents` owns one shared SignalR connection and currently forwards `StaffDataChanged` to callbacks; the general cache registry has 200ms debounce but `invalidate()` stops after the first prefix match, so overlapping stores can remain stale.
- `StaffRealtimeBoundary` increments a revision context but repository search found no production consumer of `useStaffRefresh`; the correct fix is to connect the boundary to query invalidation, not add more revision consumers.
- The frontend has Axios services and Zustand auth/session state but no query-library dependency. `content-service.ts` and `admin-service.ts` contain module-level caching/force calls; only a small set of screens registers cache stores.
- `User.SecurityStampVersion` is already checked during JWT validation in `Program.cs`, included in `TokenService` claims, incremented on password reset, soft delete, and role updates. It is therefore the existing authorization invalidation mechanism and is preferable to a parallel version unless employee/status paths require distinct semantics.

## Decisions

### D1 — Version source: reuse `SecurityStampVersion`

**Decision**: Treat `User.SecurityStampVersion` as the authorization/session version and increment it in every role, permission, active-status, employee assignment, and security mutation that changes effective access.

**Rationale**: JWT validation already rejects stale tokens, and adding a second column would create two versions that can disagree. The session endpoint exposes the current value for the client to reconcile. Password/security changes continue to increment it.

**Alternative**: Add `AuthorizationVersion`. Rejected initially because it adds a migration and duplicate invalidation semantics. Reopen only if security-stamp rotation is intentionally decoupled from authorization changes.

### D2 — Current-session endpoint

**Decision**: Add authenticated `GET /api/auth/session` that returns the current `UserDto` plus `authorizationVersion`; it does not rotate refresh tokens. The frontend calls it on bootstrap, current-user staff events, focus/reconnect reconciliation, and relevant 401/403 recovery.

**Rationale**: Refresh-token rotation is a security/session operation and is unsuitable for every permission event. A read-only snapshot is idempotent and preserves the existing refresh flow.

**Alternative**: Force `/auth/refresh` for every event. Rejected because it rotates cookies, increases race/replay risk, and couples UI freshness to token rotation.

### D3 — Query cache

**Decision**: Prefer `@tanstack/react-query` because the repository has React 19 and many independent server-state consumers; confirm package installation/build before implementation. Keep a compatibility adapter so migrated domains can invalidate by typed query keys and existing legacy screens can register until migrated.

**Rationale**: It provides active-query invalidation, dedupe, cancellation, retries, optimistic rollback, and devtools without recreating query semantics in the current registry.

**Alternative**: Expand `cache-invalidation.ts` into a home-grown query cache. Rejected as the long-term default because it already has first-prefix-only invalidation and only a small registration surface; it remains the fallback if dependency policy blocks installation.

### D4 — Event envelope and delivery

**Decision**: Extend `StaffDataChanged` with `schemaVersion`, `eventId`, `occurredAt`, optional `actorUserId`, `scopes`, `entityType`, `entityIds`, `operation`, and optional `version`, while preserving `scopes` parsing for old events. Generate the event ID when creating the outbox row, not when broadcasting.

**Rationale**: Outbox retries must re-send the same logical event and clients must deduplicate it. The event is an invalidation hint, never a database snapshot.

**Alternative**: Send full entity payloads. Rejected for privacy, authorization leakage, payload size, and stale snapshot risk.

### D5 — Reconnect policy

**Decision**: After SignalR reconnect, rejoin groups, clear only bounded in-memory dedupe state, refresh the current session if staff, and invalidate active critical queries with a 250–500ms debounce. Do not refetch inactive queries.

**Rationale**: Events can be missed during transport downtime; active-query reconciliation restores correctness without a request storm.

**Alternative**: Persist and replay every event sequence. Rejected for the current architecture because the outbox is not a client replay log and retention/authorization rules are not defined. Add sequence support later only if metrics show snapshot reconciliation is insufficient.

### D6 — Concurrency and drafts

**Decision**: Use the existing entity `UpdatedAt`/concurrency conventions where available; add a stable `rowVersion`/ETag value to employee read/update contracts and reject stale writes with a typed conflict response. Realtime events never write directly into dirty form state.

**Rationale**: A conflict response is safer than silent overwrite and preserves user work.

### D7 — Reload allowlist

**Decision**: Replace administrative/student workflow reloads with targeted invalidation. Retain only secure-video recovery if the video security contract cannot renew in-component; enforce an allowlist script and document every retained location.

**Rationale**: Full reload loses local UI state and hides stale-state bugs, but security recovery may be a deliberate exception.

### D8 — Scope mapping and performance

**Decision**: Maintain a typed frontend scope-to-query-key map with domain keys; refetch only active queries, batch/debounce scope bursts, and invalidate all matching keys rather than the first prefix.

**Rationale**: Broad backend scopes are useful for privacy-safe signaling; frontend mapping controls request volume.

## Risks and mitigations

| Risk | Mitigation | Evidence |
|---|---|---|
| Query-library migration causes duplicate fetching | Provider once at root, query-key contract tests, domain-by-domain migration, remove legacy registration only after verification | request-count E2E and network assertions |
| Permission changes race with stale JWT | existing API token validation plus session snapshot plus 403 safe handling | Auth/Program integration tests |
| Event burst causes request storm | event ID dedupe, 250–500ms debounce, active-query filtering, metrics | realtime unit/E2E tests |
| Mutation response and event race | local mutation contract runs before event invalidation; query client deduplicates refetch | same-tab and two-tab tests |
| Draft loss | dirty-form guard and conflict banner; no automatic detail overwrite | Playwright conflict test |
| 217 mutation inventory misses a service | machine-readable inventory and CI contract check | `check-query-contracts` script |

## Open planning choices resolved by implementation tasks

- Exact stale time: 30s for HR/operations, 60s for content lists, 15s for session/permissions, and explicit refetch after mutation; tune from metrics.
- Exact metrics backend: use existing structured logging/telemetry conventions discovered in implementation; do not add a new vendor.
- Feature flag location: reuse platform settings/configuration if available; otherwise a server-returned session capability, never a frontend-only security flag.
- Sequence retention: not required for first slice; snapshot reconciliation is the correctness mechanism.
