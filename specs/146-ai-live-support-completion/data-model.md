# Data Model: AI Live Support Production Completion

## Migration Strategy

Create one additive migration, tentatively `CompleteAILiveSupportProduction`, after aligning the EF Core integration-test packages. The migration MUST run against a database already containing migrations `AddLiveSupportCommandCenter`, `AddLiveSupportManagePermission`, and `AddAILiveSupportAgent`.

Upgrade order:

1. Add nullable columns and new indexes.
2. Backfill discriminator and safe defaults from existing rows.
3. Validate foreign keys, uniqueness, JSON validity, and terminal-state invariants.
4. Apply new checks only after validation.
5. Preserve all existing columns and rows. Do not drop or recreate live-support tables.

The application must be able to read pre-146 rows after migration. The down migration may remove only unused additive columns and indexes when no post-146 data exists; operational rollback uses AI disablement and prior images, not destructive schema rollback.

## Existing Entities Retained

The following remain authoritative and must preserve identifiers and history:

- `LiveSupportConversation`
- `LiveSupportGuestSession`
- `LiveSupportMessage`
- `LiveSupportEvent`
- `LiveSupportQueueEntry`
- `LiveSupportAssignment`
- `LiveSupportStudentLinkHistory`
- `LiveSupportActionExecution`
- `LiveSupportRating`
- `LiveSupportAIPolicyVersion`
- `LiveSupportAIKnowledgeEntry`
- `LiveSupportAIKnowledgeRevision`
- `LiveSupportAIPolicyKnowledgeRevision`
- `LiveSupportAIConversationState`
- `LiveSupportAITurn`
- `LiveSupportAIPendingAction`
- `LiveSupportAIVerificationPolicyQuestion`
- `LiveSupportAIVerificationSession`
- `LiveSupportAIVerificationAttempt`
- `OutboxEvent`
- `AuditLog`

## Entity Changes

### LiveSupportAIPendingAction

Purpose: one durable participant decision awaiting confirmation, cancellation, expiry, invalidation, execution, or completion.

Changes:

- `DecisionKind` (`smallint`, non-null after backfill): `Action=0`, `Handoff=1`, `AccountCreation=2`, `Resolution=3`.
- `StudentUserId` becomes nullable. It is required only when `DecisionKind=Action` and the action targets a linked student.
- `EncryptedPayload` remains nullable binary and stores only server-protected execution input.
- `SafeProposalJson` remains JSON and contains display-safe fields only.
- `PayloadHash`, `StateFingerprint`, and `ConfirmationNonceHash` remain fixed-length lowercase hex or base64url keyed digests and are mandatory for new `Action` rows.
- `CallbackDecisionHash` (nullable, max 64) records the validated provider decision identity for replay detection.
- `CancelledAt` (nullable timestamp) distinguishes participant cancellation from invalidation/expiry.
- `FailureCode` remains safe and bounded; no provider body or exception text.

Backfill:

- `ActionKey = 'system.handoff'` → `Handoff`.
- `ActionKey = 'system.account-creation'` → `AccountCreation`.
- `ActionKey = 'system.resolution'` → `Resolution`.
- All other rows → `Action`.
- Convert an existing all-zero `StudentUserId` to null before validating the foreign key.

Constraints and indexes:

- Unique `IdempotencyKey` retained.
- Unique partial active decision per `(ConversationId, DecisionKind)` where status is `PendingConfirmation`.
- Check: `Action` requires non-null `StudentUserId`, non-empty `ActionKey`, payload hash, state fingerprint, and encrypted payload.
- Check: non-Action kinds must not require a student target.
- Index `(Status, ExpiresAt)` for expiry recovery.
- Restrictive foreign keys remain for conversation, turn, policy, real user, guest session, and action execution.

### LiveSupportAITurn

Purpose: durable processing of exactly one participant source message.

Changes:

- `DecisionHash` (nullable, max 64): digest of the canonical validated decision.
- `CallbackStatus` (`smallint`): `NotReady=0`, `Pending=1`, `Delivered=2`, `Failed=3`, `Discarded=4`.
- `CallbackAttemptCount` (non-null integer, default 0).
- `NextCallbackAttemptAt` (nullable timestamp).
- `ProviderCompletedAt` (nullable timestamp) separates inference completion from backend callback persistence.
- `LastSafeCallbackErrorCode` (nullable, max 100).

Existing unique `SourceMessageId` and optional unique `OutputMessageId` remain.

Indexes:

- `(Status, QueuedAt)` retained for stale turn recovery.
- `(CallbackStatus, NextCallbackAttemptAt)` for callback delivery recovery.
- `(ConversationId, QueuedAt, Id)` for ordered investigation.

### LiveSupportAIConversationState

Purpose: current AI/human mode and recovery cursor for one conversation.

Changes:

- `LastEventSequence` (non-null bigint, default 0) caches the last state-changing support event used by snapshots.
- `DisableRequestedAt` (nullable timestamp) records emergency-disable reconciliation.
- `LastRecoveryAt` (nullable timestamp) records bounded recovery processing.

Mode remains `AiActive`, `AiResolved`, `HumanQueued`, or `HumanAssigned`. Handoff is irreversible for a conversation.

### LiveSupportAIVerificationSession

Purpose: one privacy-protected visitor verification lifecycle.

Changes:

- `LookupValueHash` is redefined for new rows as a keyed digest including lookup-key domain separation. Existing hashes remain readable as legacy evidence and are never used to reveal identity.
- `CurrentQuestionIndex` (non-null integer, default 0) replaces inference from `CorrectCount`.
- `LastAttemptAt` (nullable timestamp).
- `LockedAt` (nullable timestamp).

Constraints and indexes:

- Replace unconditional unique `ConversationId` with unique active verification where status is active. Historical sessions remain possible after cancellation, expiry, or completion.
- Index `(Status, ExpiresAt)` for expiry recovery.
- Check `0 <= CorrectCount <= AttemptCount <= MaxAttempts` and `CurrentQuestionIndex >= 0`.

### LiveSupportAIKnowledgeRevision

No destructive change. Add a PostgreSQL search-vector expression/index or equivalent normalized search index compatible with the installed provider. Retrieval always filters `IsPublished=true`, policy-linked revision IDs, result count, and character budget.

### OutboxEvent

No new table. Add conventions for queue events:

- `Type = 'LiveSupportAITurnQueued'`.
- `TargetGroup = null` because queue events are not SignalR broadcasts.
- `PayloadJson = { schemaVersion, turnId, conversationId, queuedAt }`.
- `ProcessedAt`, `RetryCount`, `IsDeadLetter`, and existing timestamps retain delivery state.

The processor routes `LiveSupportAITurnQueued` to the queue dispatcher before the generic SignalR branch.

## Logical State Machines

### Conversation AI Mode

```text
AiActive -> AiResolved
AiActive -> HumanQueued -> HumanAssigned
HumanQueued -> HumanAssigned
```

No transition returns `HumanQueued`, `HumanAssigned`, or `AiResolved` to `AiActive` for the same conversation. A new conversation is required.

### AI Turn

```text
Queued -> Processing -> ProviderCompleted -> Completed
Queued -> Processing -> Failed
Queued|Processing|ProviderCompleted -> DiscardedAfterHandoff
Queued|Processing -> Cancelled
```

`ProviderCompleted` may retry callback delivery without retrying inference. Only `Completed` may create participant-visible output.

### Pending Decision

```text
PendingConfirmation -> Confirmed -> Executing -> Succeeded
PendingConfirmation -> Cancelled
PendingConfirmation -> Expired
PendingConfirmation|Confirmed -> Invalidated
Confirmed|Executing -> Failed
```

Only one execution can claim `Confirmed -> Executing`. Terminal rows are immutable except safe audit metadata.

### Verification

```text
AwaitingLookup -> Challenging -> Verified
AwaitingLookup -> Ambiguous -> HandedOff
Challenging -> Exhausted -> HandedOff
AwaitingLookup|Challenging -> Cancelled
AwaitingLookup|Challenging -> Failed
```

Raw lookup values and answers are never persisted.

## Cross-Entity Invariants

1. One conversation has at most one current AI state.
2. One source participant message has at most one AI turn.
3. One conversation has at most one active human assignment and one active queue entry.
4. Human handoff invalidates all active AI turns and pending decisions before queue admission commits.
5. A participant-visible AI message references one completed turn and has deterministic client identity `ai-{turnId}`.
6. A successful AI action references one `LiveSupportActionExecution`; retries return that execution.
7. A policy version and published knowledge revision referenced by historical turns are never deleted.
8. No protected registration, verification, authentication, or provider-secret value appears in message content, safe JSON, events, outbox, or audit metadata.
9. Closed or abandoned conversations accept no messages, decisions, verification, links, actions, or assignments.
10. All state-changing operations increment concurrency versions and emit chronological support/audit evidence.

## Data-Preservation Verification

Before and after migration, capture and compare:

- Row counts and stable IDs for all retained entities.
- SHA-256 checksums of normalized message content and immutable policy/knowledge content in the test fixture only.
- Conversation status, participant identity, current owner, linked student, previous-conversation relation, and rating.
- Published policy identity and exactly one enabled published policy maximum.
- No orphaned conversation, message, turn, decision, verification, link, assignment, action, or rating foreign key.
- No zero-GUID user target remains in pending decisions.

## Retention and Redaction

- Historical support and audit retention follows existing platform policy; this feature does not introduce deletion.
- Provider raw responses, system secrets, passwords, tokens, raw verification answers, and full lookup values are transient only and must not be stored.
- Safe summaries are bounded, user-displayable Arabic text; operational error details are stable codes plus non-sensitive context.
