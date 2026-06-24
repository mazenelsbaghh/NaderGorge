# Contract: AI Live Support State and Race Precedence

## Canonical Modes

- `AiActive`: participant may send text; AI turns may be queued; no human assignment or queue entry exists.
- `AiResolved`: terminal AI resolution for this conversation; read-only except rating when eligible.
- `HumanQueued`: AI is permanently disabled for this conversation; one active human queue entry may exist.
- `HumanAssigned`: AI is permanently disabled; one current staff owner may send and act.
- Conversation `Closed` or `Abandoned`: terminal and read-only.

## Race Precedence

Highest precedence wins:

1. Conversation closed or abandoned.
2. Human handoff committed.
3. Emergency AI disable reconciliation.
4. Confirmed business effect already committed.
5. Participant cancellation or expiry.
6. Provider callback completion.
7. New AI turn admission.

The losing operation must not mutate participant/student state and records a stable safe outcome code.

## Required Safe Outcome Codes

- `CONVERSATION_TERMINAL`
- `AI_DISABLED`
- `AI_HANDOFF_COMMITTED`
- `TURN_ALREADY_COMPLETED`
- `TURN_STALE_VERSION`
- `DECISION_SCHEMA_INVALID`
- `POLICY_VERSION_INACTIVE`
- `ACTION_REVOKED`
- `ACTION_STATE_CHANGED`
- `CONFIRMATION_EXPIRED`
- `CONFIRMATION_CANCELLED`
- `IDEMPOTENCY_PAYLOAD_CONFLICT`
- `VERIFICATION_EXHAUSTED`
- `PROVIDER_TIMEOUT`
- `PROVIDER_UNAVAILABLE`
- `CALLBACK_DELIVERY_FAILED`
- `RECOVERY_RECONCILED`

## Handoff Transaction

The handoff operation must atomically:

1. Lock routing and conversation state.
2. Re-read conversation and AI state.
3. Return the current result for an already committed handoff.
4. Set `HumanQueued` or `HumanAssigned`.
5. Invalidate active turns, pending decisions, and verification.
6. Create at most one active queue entry.
7. Attempt normal oldest-first assignment.
8. Persist reason, safe summary, actor, event, audit, and outbox.
9. Commit before publishing client updates.

Late callback behavior: return an idempotent success with outcome `AI_HANDOFF_COMMITTED`, mark the turn discarded, and create no message or proposal.

## Disable Transaction Family

Emergency disable first publishes a disabled policy state that blocks admission. Bounded reconciliation then hands off or safely closes all `AiActive` conversations. Repeating disable is idempotent. Re-enable permits new turns only for conversations still in `AiActive`; it never reverses handoff.

## Snapshot Contract

Every participant and operator snapshot includes:

- `conversationId`
- conversation status and AI mode
- current owner and queue position where authorized
- `lastSequence`
- composer permission
- AI processing state and deadline where applicable
- one active pending decision, if any
- active verification state without candidate identity or expected answer
- handoff/closed/rating state

SignalR events with sequence `<= lastSequence` are ignored. A sequence gap forces snapshot refresh.
