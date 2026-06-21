# SignalR Contract: `LiveSupportHub`

## Endpoint and authentication

- Route: `/hubs/live-support`
- Student/staff/admin: existing JWT bearer token.
- Guest: encrypted `massar_support_guest` cookie issued by `POST /api/live-support/guest/session`.
- Hub policy: `LiveSupportParticipantOrStaff`; every join/send method revalidates conversation participation/ownership.
- Redis backplane channel prefix remains deployment-specific and distinct from internal chat event names.

## Group names

- `LiveSupport:Conversation:{conversationId}`: current participant plus current owner and observing/intervening admins.
- `LiveSupport:Participant:Student:{userId}` or `LiveSupport:Participant:Guest:{guestSessionId}`: personal status updates.
- `LiveSupport:Staff:{userId}`: assignments, load and eligibility updates.
- `LiveSupport:Queue`: staff queue-count updates only; participant queue position is sent to its personal group.
- `LiveSupport:Admins`: operational dashboard deltas.

Clients never choose arbitrary groups. Server connection code derives groups from authenticated identity and durable authorization.

## Client-to-server methods

### `JoinConversation(conversationId)`

Validates participant/current owner/admin and adds the connection to the conversation group. Returns `{ conversationId, lastEventSequence }`.

### `LeaveConversation(conversationId)`

Removes only the caller connection. It does not abandon, close, or transfer durable state.

### `Heartbeat()`

Updates ephemeral presence for staff/participant. Client interval: 30 seconds. Server heartbeat TTL: 150 seconds.

### `Typing(conversationId)`

Ephemeral, rate-limited, not persisted. Allowed only for active participant/current owner/admin observer. Server emits `TypingChanged` to others in the group and expires UI state after 3 seconds.

### Message sending

Messages are sent through HTTP `POST .../messages`, not hub invocation, so idempotency, upload references, validation, and durable response semantics are explicit. SignalR delivers the committed event afterward.

## Server-to-client events

All durable events include:

```ts
type LiveSupportEnvelope<T> = {
  eventId: string;
  conversationId?: string;
  sequence?: number;
  occurredAt: string;
  type: string;
  payload: T;
};
```

### `ConversationChanged`

Payload: conversation ID, status, owner summary, queue position, version, terminal reason, and relevant timestamps.

### `MessageCreated`

Payload matches the API `Message` schema. Client deduplicates by `eventId`/message ID.

### `QueuePositionChanged`

Participant payload: conversation ID and current position. Staff/admin payloads never expose guest contact details unless authorized in the selected conversation.

### `ConversationAssigned`

Staff personal payload: assignment ID, conversation summary, active load, capacity. Participant payload: safe staff display name and assigned state.

### `AssignmentReleased`

Staff payload: conversation ID, reason, active load, replacement assignment summary if one was immediately admitted.

### `StudentLinkChanged`

Staff/admin only: conversation version and minimal linked student summary. Participant receives no internal student-search data.

### `StudentActionChanged`

Staff/admin only: execution ID, action key, status, stable failure code, refreshed section keys. No raw password/token payload.

### `RatingSubmitted`

Admin/staff performance delta: conversation ID, stars, affected staff IDs. Participant gets confirmation only.

### `StaffEligibilityChanged`

Staff/admin: checked-in, connected, enabled, active load, capacity, eligible. Presence does not alter durable assignment by itself until disconnect grace expires.

### `DashboardChanged`

Admin-only compact delta for queue count, oldest wait, active count, and staff load. Admin client periodically reconciles through HTTP snapshot.

### `TypingChanged`

Ephemeral `{ conversationId, identityType, displayName, isTyping }` with no durable sequence.

### `SupportUnavailable`

Participant widget: `{ canStart: false, nextAvailableAt?, message }` when the last checked-in employee clocks out. Existing conversations remain accessible/read-only or queued as their durable state dictates.

## Reconnect protocol

1. SignalR automatic reconnect begins.
2. On reconnect, client calls `Heartbeat` and HTTP bootstrap/snapshot with its last durable sequence.
3. Server returns missed events or current snapshot; client replaces derived queue/owner state and deduplicates messages by ID.
4. Staff reconnect within 120 seconds preserves ownership. Later reconnect receives current assignment state after reconciliation.

## Error contract

- Hub methods throw stable codes only: `NOT_PARTICIPANT`, `NOT_OWNER`, `CONVERSATION_TERMINAL`, `RATE_LIMITED`, `SESSION_EXPIRED`.
- Server exceptions and private entity existence are not exposed.
- HTTP remains the source for validation details and recoverable action errors.
