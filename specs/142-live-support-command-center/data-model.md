# Data Model: Live Support Command Center

All timestamps are UTC `DateTime`; Egypt-local display and weekly schedule calculation use `Africa/Cairo`. Every durable entity uses the existing `BaseEntity` ID/created/updated conventions unless noted.

## Enumerations

### `LiveSupportConversationStatus`

- `Waiting`: durable queue entry exists; no active assignment.
- `Assigned`: active assignment exists; staff has not sent the first response.
- `Active`: active assignment exists and at least one staff response has been sent.
- `Transferred`: transient event label only, not a stored steady conversation status; transition immediately produces `Assigned` or `Waiting`.
- `Closed`: terminal, read-only, capacity released.
- `Abandoned`: terminal when the participant explicitly cancels before service or an admin closes as abandoned.

Allowed steady transitions:

```text
Create → Waiting | Assigned
Waiting → Assigned | Abandoned
Assigned → Active | Waiting | Closed | Abandoned
Active → Assigned (transfer) | Waiting | Closed | Abandoned
Closed → terminal
Abandoned → terminal
```

### `LiveSupportParticipantType`

- `Student`
- `Guest`

### `LiveSupportSenderType`

- `Student`
- `Guest`
- `Staff`
- `Admin`
- `System`

### `LiveSupportMessageType`

- `Text`
- `Image`
- `Pdf`
- `Audio`
- `System`

### `LiveSupportAssignmentEndReason`

- `Closed`
- `ManualTransfer`
- `AttendanceCheckout`
- `DisconnectTimeout`
- `AdminReassignment`
- `CapacityReconciliation`

### `LiveSupportEventType`

`ConversationCreated`, `QueueEntered`, `QueuePositionChanged`, `Assigned`, `FirstStaffResponse`, `MessageSent`, `TransferRequested`, `Transferred`, `StaffDisconnected`, `StaffReconnected`, `AttendanceCheckedIn`, `AttendanceCheckedOut`, `StudentLinked`, `StudentUnlinked`, `StudentLinkReplaced`, `ActionRequested`, `ActionSucceeded`, `ActionFailed`, `ParticipantDisconnected`, `ParticipantReconnected`, `Closed`, `Abandoned`, `RatingSubmitted`, `FollowUpCreated`, `AdminIntervened`.

### `LiveSupportActionStatus`

- `Pending`
- `Succeeded`
- `Failed`
- `Rejected`

## Entities

### `LiveSupportConversation`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `ParticipantType` | enum | Required |
| `StudentUserId` | Guid? | Required when participant is authenticated Student; null for unlinked guest |
| `GuestSessionId` | Guid? | Required for Guest; null for authenticated Student |
| `LinkedStudentUserId` | Guid? | Manual guest link target; for Student equals `StudentUserId` |
| `PreviousConversationId` | Guid? | Optional reference to most recent prior closed/abandoned conversation |
| `Status` | enum | Required; terminal after Closed/Abandoned |
| `CurrentOwnerUserId` | Guid? | Null while queued/terminal; denormalized active owner protected by assignment coordinator |
| `QueuedAt` | DateTime? | Set whenever entering queue |
| `AssignedAt` | DateTime? | Current assignment start |
| `FirstStaffResponseAt` | DateTime? | Set exactly once |
| `ClosedAt` | DateTime? | Required for Closed/Abandoned |
| `ClosedByUserId` | Guid? | Staff/admin actor; null for participant abandonment if allowed |
| `CloseReason` | string? | Max 500; required for admin close/abandonment |
| `Subject` | string? | Max 200; generated from first message or supplied category |
| `LastMessageAt` | DateTime? | Conversation list ordering |
| `Version` | long | Optimistic concurrency token |

Constraints/indexes:

- Check exactly one participant identity path: Student requires `StudentUserId`; Guest requires `GuestSessionId`.
- Filtered unique index on `StudentUserId` for statuses Waiting/Assigned/Active: one open conversation per student.
- Filtered unique index on `GuestSessionId` for statuses Waiting/Assigned/Active: one open conversation per guest session.
- Filtered unique index on `CurrentOwnerUserId` is **not** used because one staff owns many conversations.
- Index `(Status, QueuedAt, Id)` for FIFO; `(CurrentOwnerUserId, Status)` for capacity; `(LinkedStudentUserId, CreatedAt DESC)` for history; `(LastMessageAt DESC)` for lists.
- Closed/Abandoned rows cannot return to non-terminal states; application and database check/trigger guard where feasible.

### `LiveSupportGuestSession`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key and cookie claim |
| `DisplayName` | string | Required, trimmed, 2–120 characters |
| `PhoneNumber` | string | Required normalized Egyptian/international phone, 8–20 characters; contact claim only |
| `SecurityStampHash` | string | Required; validates revocation without storing cookie secret |
| `ExpiresAt` | DateTime | Required; sliding renewal capped by policy |
| `RevokedAt` | DateTime? | Null while valid |
| `LastSeenAt` | DateTime | Rate-limit/session recovery evidence |
| `CreatedIpHash` | string | HMAC/hash, never raw long-term IP where avoidable |
| `UserAgentSummary` | string? | Max 300, redacted |

Indexes: `(PhoneNumber, CreatedAt DESC)` for staff-assisted search context only; `ExpiresAt`; `RevokedAt`.

### `LiveSupportStaffConfig`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `UserId` | Guid | Unique FK to employee user |
| `IsEnabled` | bool | Explicit admin opt-in |
| `MaxActiveConversations` | int | 1–50; required when enabled |
| `LastAssignedAt` | DateTime? | Tie rotation ordering |
| `ConfiguredByUserId` | Guid | Admin actor |
| `Version` | long | Concurrency token |

Eligibility is computed, not stored: enabled + active `AttendanceLog` + online Redis presence + active count below capacity.

### `LiveSupportScheduleWindow`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `StaffConfigId` | Guid | Required FK |
| `DayOfWeek` | int | 0–6; use one canonical mapping documented in API |
| `StartLocalTime` | TimeOnly | Required |
| `EndLocalTime` | TimeOnly | Required and later than start; overnight windows split into two rows |
| `IsActive` | bool | Soft enable |

Unique index `(StaffConfigId, DayOfWeek, StartLocalTime, EndLocalTime)`; reject overlaps per staff/day. Schedules inform next availability only; actual assignment still requires attendance check-in.

### `LiveSupportQueueEntry`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `ConversationId` | Guid | Required FK |
| `EnteredAt` | DateTime | FIFO canonical time |
| `Sequence` | long | Database-generated monotonic ordering/tie breaker |
| `DequeuedAt` | DateTime? | Set on assignment/cancel |
| `DequeueReason` | string? | Assignment, abandonment, admin close |

- Filtered unique index on `ConversationId` where `DequeuedAt IS NULL`.
- Index `(DequeuedAt, EnteredAt, Sequence)`.
- Queue position is calculated from active entries; it is never stored as mutable rank.

### `LiveSupportAssignment`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `ConversationId` | Guid | Required FK |
| `StaffUserId` | Guid | Required FK |
| `StartedAt` | DateTime | Required |
| `EndedAt` | DateTime? | Null for active owner |
| `EndReason` | enum? | Required when ended |
| `AssignedByUserId` | Guid? | Null for automatic routing; actor for manual/admin transfer |
| `TransferReason` | string? | Required for manual transfer, max 500 |
| `AssignmentSequence` | int | Starts at 1 per conversation |

- Filtered unique index on `ConversationId` where `EndedAt IS NULL` enforces one active owner.
- Index `(StaffUserId, EndedAt, StartedAt)` supports load and rating attribution.
- Unique `(ConversationId, AssignmentSequence)`.

### `LiveSupportMessage`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key and durable event ID |
| `ConversationId` | Guid | Required FK |
| `SenderType` | enum | Required |
| `SenderUserId` | Guid? | Staff/admin/student user when applicable |
| `SenderGuestSessionId` | Guid? | Guest sender when applicable |
| `ClientMessageId` | string | Required UUID/string from client, max 100 |
| `Type` | enum | Required |
| `Content` | string | Trimmed; 1–5,000 for Text; optional caption for attachment |
| `AttachmentId` | Guid? | Required for attachment message type |
| `SentAt` | DateTime | Canonical server time |

- Unique `(ConversationId, SenderType, SenderUserId, SenderGuestSessionId, ClientMessageId)` using normalized nullable-key strategy to deduplicate retries.
- Index `(ConversationId, SentAt DESC, Id DESC)` for cursor pagination.
- No edit/delete columns. Moderation may hide an attachment while preserving event evidence.

### `LiveSupportAttachment`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `StoragePath` | string | Server-controlled path, never caller path |
| `OriginalFileName` | string | Sanitized display name, max 255 |
| `ContentType` | string | Allowlisted MIME |
| `SizeBytes` | long | Image ≤10MB, PDF ≤15MB, audio ≤20MB and ≤5 minutes where duration known |
| `Sha256` | string | Integrity/dedup evidence |
| `UploadedByIdentity` | string | User/guest opaque identity reference |
| `IsBlocked` | bool | Admin safety block without deleting message evidence |

Allowed MIME: JPEG, PNG, WebP, PDF, MP3, M4A, OGG/WebM audio. Downloads require conversation authorization.

### `LiveSupportStudentLinkHistory`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `ConversationId` | Guid | Required FK |
| `PreviousStudentUserId` | Guid? | Null for initial link |
| `NewStudentUserId` | Guid? | Null for unlink |
| `ChangedByUserId` | Guid | Current staff owner/admin |
| `Reason` | string | Required, 3–500 characters |
| `ChangedAt` | DateTime | Required |

Current link remains denormalized on conversation; history is immutable.

### `LiveSupportEvent`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `ConversationId` | Guid | Required FK |
| `Type` | enum | Required |
| `ActorUserId` | Guid? | Staff/admin/student |
| `ActorGuestSessionId` | Guid? | Guest |
| `RelatedEntityType` | string? | Max 100 |
| `RelatedEntityId` | Guid? | Assignment/message/action/link/etc. |
| `SafeMetadataJson` | jsonb? | Redacted, schema per event contract |
| `OccurredAt` | DateTime | Canonical ordering time |
| `Sequence` | long | Monotonic per conversation |

- Unique `(ConversationId, Sequence)`.
- Index `(ConversationId, Sequence)` and `(Type, OccurredAt)`.
- Append-only through application policy; no update/delete route.

### `LiveSupportActionExecution`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `ConversationId` | Guid | Required FK |
| `StudentUserId` | Guid | Must equal current linked student at request time |
| `StaffUserId` | Guid | Current owner or intervening admin |
| `ActionKey` | string | Required catalog key, max 100 |
| `IdempotencyKey` | string | Required client UUID, max 100 |
| `PayloadHash` | string | Detect key reuse with different payload |
| `SafeRequestJson` | jsonb | Allowlisted/redacted summary |
| `SafeResultJson` | jsonb? | Allowlisted/redacted summary |
| `Status` | enum | Required |
| `FailureCode` | string? | Stable code, no stack trace |
| `AuditLogId` | Guid? | Required on succeeded state-changing action |
| `StartedAt` | DateTime | Required |
| `CompletedAt` | DateTime? | Required for terminal status |

- Unique `(StaffUserId, IdempotencyKey)`.
- Index `(ConversationId, StartedAt DESC)`, `(StudentUserId, StartedAt DESC)`, `(ActionKey, Status, StartedAt)`.

### `LiveSupportRating`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | Primary key |
| `ConversationId` | Guid | Unique FK; conversation must be Closed |
| `Stars` | int | Required 1–5 |
| `Comment` | string? | Optional, max 1,000 |
| `SubmittedByUserId` | Guid? | Student participant |
| `SubmittedByGuestSessionId` | Guid? | Guest participant |
| `SubmittedAt` | DateTime | Required |

One immutable row per conversation. Performance aggregation joins distinct `LiveSupportAssignment.StaffUserId` values; the same stars count once for each owner.

## Existing Entity Changes

### `AuditLog`

Do not duplicate full support data. Add optional `ConversationId` only if repository conventions accept the FK; otherwise store the support conversation ID in a structured correlation field and link via `LiveSupportActionExecution.AuditLogId`. Existing action, entity, actor, old/new, IP, and correlation fields remain authoritative.

### `OutboxEvent`

No schema change required. Add typed live-support event names and target groups. Payload must be a single JSON object, not double-serialized content.

### `PlatformSetting`

Add `LiveSupportEnabled` boolean setting. Disabled state blocks participant conversation creation but keeps admin history available.

## Redis Ephemeral Keys

| Key | Value | TTL/Behavior |
|---|---|---|
| `live-support:presence:staff:{userId}` | connection count, last heartbeat UTC | heartbeat TTL 150s |
| `live-support:presence:participant:{identity}` | connection count, last heartbeat UTC | heartbeat TTL 150s |
| `live-support:disconnects` | sorted set userId → disconnect timestamp | reconciler removes on reconnect or after processing at 120s |
| `live-support:idempotency:{staffId}:{key}` | processing/result envelope | 24h |

Redis is not the source of message, queue, owner, or audit truth.

## Delete and Retention Behavior

- Conversation, message, assignment, event, link, action, and rating records use restrict/soft-retention behavior; normal APIs never hard-delete them.
- Deleting/deactivating a user does not cascade-delete support history; display falls back to stored safe names/IDs.
- Guest sessions may expire/revoke while conversation history remains available to admin and authorized staff.
- Attachments follow platform retention and can be blocked; message/event evidence remains.
