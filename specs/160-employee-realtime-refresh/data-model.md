# Data Model: Employee Workflows and Realtime Refresh

## Existing entities reused

### User

- `Id`: stable identity used for user-targeted events.
- `IsActive`, `IsDeleted`, `SuspensionReason`: effective account state; mutations that change these increment `SecurityStampVersion`.
- `SecurityStampVersion`: existing integer used in JWT validation; becomes the exposed `authorizationVersion` for session reconciliation.
- `UserRoles`: role membership used to calculate permissions/domains/navbar.
- `EmployeeProfile`: employee-specific identity and HR linkage.
- `UpdatedAt`: existing base entity timestamp used as a conflict input where supported.

### Role/UserRole/EmployeeProfile/AttendanceLog/EmployeeVacation

These remain durable backend-owned records. Changes emit scopes (`users`, `hr`, `settings`, and relevant domain scopes) and are reflected through query contracts, not copied into a global Zustand server-state store.

### OutboxEvent

The existing transactional outbox remains the source for reliable SignalR dispatch. The `PayloadJson` schema is extended compatibly; processing/retry state remains unchanged.

## New or formalized contracts

### CurrentSessionSnapshot

```text
user: UserDto
authorizationVersion: integer
serverTime: ISO-8601 timestamp
```

`UserDto` retains identity, roles, permissions, profile completion, avatar, allowed domains, and allowed navbar items. No secrets, refresh token, password, or sensitive HR fields are returned.

### DataChangedEvent

```text
schemaVersion: "2"
eventId: UUID
occurredAt: ISO-8601 timestamp
actorUserId?: UUID
scopes: DataScope[]
entityType?: string
entityIds?: UUID[]
operation: "created" | "updated" | "deleted" | "bulk"
version?: integer|string
```

`scopes` is non-empty and contains only allowlisted scope names. Event payloads contain no entity snapshots or private employee fields.

### QueryContract

```text
key: readonly typed segments
domain: DataDomain
fetchPolicy: staleTime/retry/focus/cancel policy
affectedBy: DataScope[]
mutationOperations: operation -> update or invalidate keys
activeOnly: boolean
```

The contract is machine-readable in `frontend/src/lib/query-contracts.ts` and validated by a script/test against service mutation inventory.

### EmployeeEditVersion

```text
employeeId: UUID
rowVersion: ISO timestamp or opaque server version
```

Update commands accept the version and return a typed conflict (HTTP 409 or the project’s equivalent `ApiResponse` error) when it is stale. The implementation must select the repository’s existing concurrency mechanism rather than introduce a second competing token.

## State transitions

1. Mutation begins → pending query/mutation state; duplicate submits disabled.
2. Mutation succeeds → response applied or affected active queries invalidated; success feedback shown.
3. Transaction commits → outbox event created with stable `eventId`.
4. Event delivered → scope map invalidates active queries; duplicate IDs ignored.
5. Connection drops → transport state is reconnecting; no destructive UI reset.
6. Reconnect succeeds → groups rejoined, session/query reconciliation runs.
7. Concurrent edit detected → server returns conflict; local draft remains editable and user chooses reload/merge.
8. Permission revoked → backend returns 401/403 or session event; auth snapshot updates and protected route enters safe state.

## Validation and invariants

- `eventId` is stable across outbox retries and unique per logical event.
- A user-targeted session event is delivered only to the authorized user group or staff group according to existing hub authorization.
- `SecurityStampVersion` is incremented in the same transaction as role/status/permission changes.
- No successful mutation is allowed to depend on a later SignalR event to update the mutating screen.
- Query invalidation never grants access; every query and mutation still uses backend authorization.
