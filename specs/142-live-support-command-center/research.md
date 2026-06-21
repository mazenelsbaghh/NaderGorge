# Research: Live Support Command Center

## Evidence Base

### Repository evidence

- `backend/src/NaderGorge.API/Hubs/ChatHub.cs` and `frontend/src/hooks/useSignalR.ts` prove SignalR plus Redis backplane and automatic reconnect already exist for authenticated internal chat.
- `ChatRoom`, `ChatParticipant`, and `ChatMessage` require a `User` identity and model rooms rather than support ownership, capacity, queue, rating, or guest identity.
- `ClockInCommand`/`ClockOutCommand` and `AttendanceLog` provide the authoritative checked-in interval. `EmployeeProfile` has only one daily start time and target hours, so it cannot represent a complete weekly next-availability schedule.
- `AdminStudentProfileClient`, `admin-service.ts`, `AdminController`, and the Admin Application commands expose the current student read/write surface.
- `OutboxProcessorBackgroundService` already uses PostgreSQL `FOR UPDATE SKIP LOCKED` and a transaction, providing a local pattern for concurrent queue consumers.
- `IIdempotencyService` and `RedisIdempotencyService` already provide request locks/results, but the interface currently lives beside the implementation and must be moved or wrapped behind an Application-owned port when reused.
- `AuditLog`, `IAuditRepository`, and reports already provide central state-change evidence and redaction expectations.
- Docker already deploys PostgreSQL, Redis, one backend, five frontend surfaces, worker, and Nginx. No new process is necessary.

### Primary technical references

- Microsoft SignalR authentication and authorization: `https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz?view=aspnetcore-9.0`
- Microsoft SignalR Redis backplane: `https://learn.microsoft.com/aspnet/core/signalr/redis-backplane?view=aspnetcore-9.0`
- EF Core transactions: `https://learn.microsoft.com/ef/core/saving/transactions`
- PostgreSQL row locking and `SKIP LOCKED`: `https://www.postgresql.org/docs/current/sql-select.html`
- PostgreSQL advisory locks: `https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS`

## Decision 1: Separate live support from internal chat

**Decision**: Create a dedicated LiveSupport aggregate, controllers, and hub. Reuse transport and UI primitives selectively, not internal chat tables.

**Rationale**: Internal chat requires authenticated `User` participants and permits group/workroom semantics. Adding nullable guest senders, queue ownership, capacity, ratings, student links, and elevated actions would weaken its invariants and authorization.

**Alternatives considered**:

- Extend `ChatRoom` with many nullable support fields: rejected because it couples two distinct permission/lifecycle models.
- Put guests into synthetic `User` records: rejected because guest contact claims are unverified and would pollute authentication and student identity.

## Decision 2: Guest identity uses a limited secure support session

**Decision**: Persist a guest session and issue an encrypted HttpOnly support cookie containing only the guest-session identifier and security stamp. Use a dedicated authentication scheme/policy for participant endpoints and `LiveSupportHub`.

**Rationale**: The browser can reconnect without exposing a bearer token to JavaScript. The session is revocable, rate-limitable, and cannot call normal student/admin APIs. Guest phone is never used as authentication.

**Alternatives considered**:

- Store a guest JWT in localStorage/sessionStorage: rejected due token theft exposure and weaker revocation.
- Anonymous hub methods using conversation ID: rejected because guessed IDs would expose messages.
- OTP/WhatsApp: explicitly out of scope.

## Decision 3: PostgreSQL is the durable routing source; Redis is presence only

**Decision**: Persist queue entries, assignments, conversation status, capacity policy, and last-assigned ordering in PostgreSQL. Store active SignalR connection presence and last-seen heartbeat in Redis with TTL.

**Rationale**: Queue and ownership must survive restarts and be auditable. Presence is ephemeral and high-churn. Redis loss may temporarily mark staff offline but cannot lose conversations or ownership history.

**Alternatives considered**:

- Entire queue in Redis: rejected because queue order/assignment/audit would be harder to restore atomically with relational state.
- Presence in PostgreSQL on every heartbeat: rejected due unnecessary write load.

## Decision 4: Serialize assignment-changing operations with a database lock

**Decision**: `LiveSupportAssignmentCoordinator` uses one transaction-scoped PostgreSQL advisory lock for the initial single support pool, then row locks the relevant conversation/queue rows. It selects the least-loaded eligible staff and orders ties by `LastAssignedAt`, with deterministic ID fallback.

**Rationale**: A single critical section makes capacity, FIFO, tie rotation, checkout, closure, and disconnect recovery correct across multiple API instances. Current expected scale is small enough that this lock is not a bottleneck; all critical sections are short and contain no network calls.

**Alternatives considered**:

- Optimistic retry only: rejected because multiple aggregate counts and FIFO position are involved.
- Redis distributed lock only: rejected because the durable writes still need a database transaction and Redis/database split-brain increases risk.
- Serializable transaction without explicit lock: viable but rejected for less predictable retry behavior in a high-contention queue.

## Decision 5: Attendance gates eligibility; weekly support schedule is separate configuration

**Decision**: A staff member is assignable only when support-enabled, currently checked in, connected, and below capacity. Add normalized weekly schedule windows for next-availability display. Clock-in and clock-out publish routing notifications after their own successful commit.

**Rationale**: This exactly implements the approved attendance rule. Existing `StandardStartTime` and `TargetDailyHours` do not encode weekdays, split shifts, or support enablement, so they cannot reliably produce the next support time.

**Alternatives considered**:

- Treat login as online: rejected by clarification.
- Add manual pause/busy state: rejected by clarification.
- Infer every day from `StandardStartTime`: rejected because it would display false availability on days off.

## Decision 6: Two-minute disconnect grace with periodic reconciliation

**Decision**: Hub disconnect updates Redis last-seen state. A background reconciler checks expired presence every 15 seconds; after 120 continuous seconds offline it invokes the same assignment coordinator to release/requeue owned conversations. Reconnection before expiry cancels recovery.

**Rationale**: SignalR's automatic reconnect can bridge short network changes without unnecessary transfers while enforcing the exact approved timeout.

**Alternatives considered**:

- Delay tasks per connection inside the hub: rejected because process restart loses timers.
- Immediate requeue: rejected by clarification.
- Admin-only recovery: rejected by automation requirement.

## Decision 7: Use transactional outbox for durable client updates

**Decision**: Commands write durable state and an `OutboxEvent` in the same save. Extend outbox dispatch with typed live-support groups and JSON objects rather than raw nested JSON strings. Clients reconcile snapshots after reconnect.

**Rationale**: A committed message or assignment must not disappear because a hub broadcast failed. The existing processor already uses locked batches, retry/backoff, and dead-letter tracking.

**Alternatives considered**:

- Broadcast directly in handlers/hub: rejected because database commit and broadcast cannot be atomic.
- Add a new message broker: rejected because Redis/Outbox already cover the requirement.

## Decision 8: Conversation-bound typed student action catalog

**Decision**: Implement multiple `ILiveSupportStudentActionHandler<TPayload>` adapters registered by stable action key. A catalog query returns metadata and capability state; execution verifies current assignment, linked student, confirmation version, and idempotency before invoking reused business logic.

**Rationale**: The user explicitly grants complete student control to support staff, but the capability must exist only inside an owned conversation and remain auditable. A typed multi-handler registry avoids a giant switch and provides a contract-parity gate.

**Alternatives considered**:

- Grant `users.manage` globally to all support staff: rejected because it exposes admin actions outside live support and cannot bind audit to a conversation.
- Proxy arbitrary AdminController routes: rejected because targets and payloads cannot be validated uniformly and some routes are unrelated to one student.
- Duplicate business logic in new commands: rejected because it creates drift in financial/academic rules.

## Decision 9: Action scope and initial catalog

**Decision**: Initial catalog covers every current student-profile action plus directly student-targeted adjacent workflows: profile update, password reset, account status, note add/delete, device disconnect one/all, package cancellation/refund, balance adjustment, gamification adjustment, video override, watch-count reset/set, watch request approve/reject, lesson/exam manual unlock where currently supported, CRM assignment/note/call entry, and student creation/link. Read context covers the entire `StudentProfileExtendedDto` plus academic/workflow summaries.

**Rationale**: These are the existing student-targeted capabilities evidenced in the repository. Content authoring, global code generation, teacher finance, and platform settings are not “actions on this student” and remain outside the action catalog.

**Alternatives considered**:

- Expose every AdminController endpoint: rejected because many endpoints mutate global content or platform state unrelated to the linked student.

## Decision 10: Audit model combines support events and central audit

**Decision**: `LiveSupportEvent` is the immutable conversation lifecycle timeline. Every student write also creates an `AuditLog` with `ConversationId` in correlation metadata and a `LiveSupportActionExecution` pointing to the audit record. Sensitive fields are allowlisted/redacted before storage.

**Rationale**: Conversation metrics require a specialized event stream; platform governance requires the existing central audit trail. Linking them provides both without duplicating full payloads.

**Alternatives considered**:

- Put all messages/events into `AuditLog`: rejected because volume and query patterns differ.
- Store raw action request/response JSON: rejected due password/token/private-data leakage.

## Decision 11: Rating is one immutable conversation record, attributed by query

**Decision**: Store one 1–5 rating per closed conversation. Performance queries join distinct staff assignment owners and attribute the same rating once to each owner. The participant may submit once; admin can see but not rewrite it.

**Rationale**: This directly implements the clarification and prevents duplicated rating rows from drifting.

**Alternatives considered**:

- Duplicate one row per employee: rejected due update/consistency risk.
- Attribute only to final owner or weight by time: rejected by clarification.

## Decision 12: Closed conversations never reopen

**Decision**: `Closed` is terminal and read-only. The widget exposes “Start new conversation”; the new conversation may reference `PreviousConversationId` for history but has a new queue/assignment lifecycle and rating.

**Rationale**: This is the explicit clarified behavior and keeps operational metrics bounded.

**Alternatives considered**: Time-window reopening and permanent threads were explicitly rejected.

## Decision 13: Attachments reuse approved storage with strict limits

**Decision**: Support text plus image, PDF, and short audio attachments through the existing authenticated asset storage abstraction. Enforce MIME allowlist, size limits, normalized server filenames, access-controlled download, and attachment metadata. Attachment failure does not block text retry.

**Rationale**: Support commonly needs screenshots/documents/voice notes, and the existing chat DTO already anticipates media. Explicit limits avoid an unbounded file subsystem.

**Alternatives considered**:

- Text only: rejected as too restrictive for support evidence.
- Arbitrary file upload: rejected for security and storage risk.

## Decision 14: Frontend uses separate participant, staff, and admin compositions

**Decision**: Shared typed service/hub/store plus three role-specific compositions. Staff command center is a three-pane desktop product layout; mobile/tablet progressively drill into list, chat, and student context. Participant widget is mobile-first.

**Rationale**: The information hierarchy differs materially by role. Reusing one giant component would create conditional complexity and accessibility failures.

**UI evidence and constraints**:

- Keep Massar Deep Navy/Teal/Gold tokens and Tajawal/Montserrat defined in current PRODUCT/DESIGN context.
- Use a restrained data-dense product layout, not the unrelated dark green palette/font output from generic UI research.
- Use Lucide icons, ≥44px mobile targets, clear focus, semantic live regions, reduced motion, lazy context panels, and no horizontal overflow at 320px.
- Avoid decorative glass, nested cards, oversized radii, gradient text, and motion unrelated to status.

## Decision 15: Testing must include real PostgreSQL concurrency and WebKit

**Decision**: Keep fast Application unit tests, add PostgreSQL-backed assignment concurrency tests, and add Playwright E2E covering participant, two staff, admin, guest privacy, queue capacity, disconnect, actions, and ratings. Add WebKit/mobile coverage for the participant widget.

**Rationale**: EF InMemory cannot validate advisory locks, `SKIP LOCKED`, unique filtered indexes, or transaction isolation. Chromium-only UI tests miss Safari behavior.

**Alternatives considered**:

- InMemory tests only: rejected for false confidence on routing correctness.
- Manual testing only: rejected because capacity/idempotency races are repeatable regression risks.

## Decision 16: Rollout is gated and reversible

**Decision**: Add a cached platform setting `LiveSupportEnabled`. Deploy schema/code disabled, configure staff/schedules, smoke admin/staff, then enable student and guest entry. Disabling the setting hides new entry while preserving history and admin access.

**Rationale**: This feature changes privacy, finance-adjacent actions, and real-time operations. A kill switch reduces rollout risk without destructive rollback.

**Alternatives considered**:

- Immediate all-user launch: rejected due operational and privacy risk.
- Roll back migration after data: rejected as destructive.

## Resolved Unknowns

No `NEEDS CLARIFICATION` items remain. Scale targets are planning assumptions, not product promises beyond the measurable success criteria. Exact attachment limits and support schedule editing details are fixed in contracts/tasks rather than left to implementer inference.
