# Data Model: Admin AI Agent

**Feature**: 169-admin-ai-agent
**Database**: PostgreSQL 16 through EF Core 9/Npgsql
**Migration policy**: additive, forward-compatible, no production data reset or destructive backfill

## Design rules

1. AdminAI is a separate bounded context. No table has a foreign key to internal chat or live-support conversation/message/policy/action tables.
2. PostgreSQL is authoritative. Redis/BullMQ/SignalR carry delivery hints only.
3. All timestamps are UTC timestamptz and render using existing Cairo-facing helpers.
4. Every mutable aggregate has a bigint Version used for optimistic concurrency. Execution claims also use a database transaction/row lock.
5. Foreign keys to evidence, proposals, executions, and baselines use Restrict/NoAction. There is no cascade delete.
6. Conversations are archived, not hard-deleted through this feature. Action/evidence rows have no update/delete API.
7. JSONB is allowed only for bounded, schema-validated safe summaries/manifests. Raw EF entities, raw provider prompts, hidden instructions, reasoning, secret fields, and unrestricted AuditLog values are forbidden.
8. Protected payloads use purpose-separated authenticated encryption and a keyed digest. Encryption keys remain outside the database.
9. The initiating Admin identity is preserved through proposal and execution. No system actor or arbitrary fallback Admin executes an AdminAI action.

## Relationship overview

    User -> owns many AdminAIConversation
    AdminAIConversation -> contains many AdminAIMessage and AdminAITurn
    AdminAICapabilityBaseline -> governs many AdminAITurn
    AdminAISensitiveDataPolicyVersion -> redacts many AdminAITurn
    AdminAITurn -> advances through many AdminAITurnStep
    AdminAITurnStep -> records many AdminAIReadInvocation
    AdminAITurn -> creates many AdminAIActionProposal
    AdminAIActionProposal -> has zero or one ConfirmationChallenge
    AdminAIActionProposal -> has zero or one SecureInputGrant
    AdminAIActionProposal -> has zero or one ActionExecution
    AdminAIActionExecution -> has many ActionExecutionItem for partial bulk results
    Conversation/Turn/Read/Proposal/Execution -> correlate to append-only AuditEvent

## Enumerations

### AdminAICapabilityBaselineStatus

- Draft
- Active
- Superseded
- Rejected

Only one Active baseline may exist.

### AdminAISensitiveDataPolicyStatus

- Draft
- Active
- Superseded

Only one Active policy may exist.

### AdminAIConversationStatus

- Active
- Archived

### AdminAIMessageRole

- Admin
- Assistant
- Status

Status messages are server-generated safe lifecycle notices only. There is no hidden/system-prompt message persisted as visible conversation content.

### AdminAITurnStatus

- Queued
- Planning
- Retrieving
- Answering
- WaitingClarification
- ProposalReady
- Completed
- CancelRequested
- Cancelled
- Failed
- AccessRevoked

### AdminAITurnStepStatus

- Queued
- Claimed
- ProviderRunning
- ReadsRequested
- ReadsCompleted
- Completed
- Cancelled
- Failed
- Superseded

### AdminAIModelDecisionType

- Answer
- Clarify
- RequestReads
- ProposeActions
- Refuse

### AdminAIReadInvocationStatus

- Pending
- Running
- Succeeded
- Empty
- Truncated
- Rejected
- Cancelled
- Failed

### AdminAICapabilityKind

- Read
- Preview
- Export
- Mutation
- ExternalSideEffect
- SecureContinuation
- Excluded

### AdminAIRiskCategory

- Ordinary
- Destructive
- Financial
- Permission
- Security
- AccountDisable
- Credential
- Bulk
- ExternalSideEffect

If more than one applies, PrimaryRisk stores the strongest category and RiskFlagsJson stores all applicable flags. Every category except Ordinary requires strong confirmation.

### AdminAIConfirmationType

- Explicit
- TypedStrong

### AdminAIProposalStatus

- PendingSecureInput
- PendingConfirmation
- Confirming
- Executing
- Succeeded
- PartiallySucceeded
- Cancelled
- Expired
- Invalidated
- Rejected
- Failed
- RecoveryRequired

### AdminAIChallengeStatus

- Pending
- Accepted
- Rejected
- Locked
- Expired
- Cancelled

### AdminAISecureInputGrantStatus

- Issued
- Submitted
- Consumed
- Cancelled
- Expired
- Purged

### AdminAIExecutionStatus

- Claimed
- Executing
- Succeeded
- PartiallySucceeded
- Rejected
- Failed
- RecoveryRequired

### AdminAIExecutionItemStatus

- Succeeded
- Skipped
- ValidationFailed
- AuthorizationFailed
- Stale
- DependencyFailed
- SystemFailed

### AdminAIAuditEventType

- ConversationCreated/Renamed/Archived/Restored
- TurnQueued/Claimed/Cancelled/Failed
- ReadStarted/Completed/Rejected
- AnswerCompleted/ClarificationRequested/RequestRefused
- ProposalCreated/Cancelled/Expired/Invalidated
- SecureInputIssued/Consumed
- ConfirmationAccepted/Rejected
- ExecutionStarted/Succeeded/PartiallySucceeded/Rejected/Failed/RecoveryRequired
- AccessRevoked
- BaselineActivated/SensitivePolicyActivated

## Entities

### AdminAICapabilityBaseline

Immutable snapshot of the approved capability contract.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| Version | varchar(64) | Unique human-readable version |
| ManifestHash | char(64) | SHA-256 of canonical manifest; unique |
| SafeManifestJson | jsonb | Bounded reviewed metadata; no secret values |
| SourceRevision | varchar(100) | Sealed source revision/worktree fingerprint |
| RuntimeInventoryHash | char(64) | Runtime backend inventory digest |
| FrontendInventoryHash | char(64) | Reachable frontend graph digest |
| SupportedReadCount | integer | Non-negative evidence value |
| SupportedActionCount | integer | Non-negative evidence value |
| ExcludedCount | integer | Non-negative; every exclusion has a reason |
| Status | enum | Draft/Active/Superseded/Rejected |
| ApprovedByAdminUserId | uuid nullable | Required for Active; FK User Restrict |
| ApprovedAt | timestamptz nullable | Required for Active |
| CreatedAt | timestamptz | Required |

**Constraints/indexes**:

- Unique Version and ManifestHash.
- Partial unique index where Status = Active.
- Check counts are non-negative and Active implies approver/time.
- Snapshot is immutable after activation.

### AdminAISensitiveDataPolicyVersion

Immutable safe classification/redaction contract.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| Version | varchar(64) | Unique |
| PolicyHash | char(64) | Canonical SHA-256; unique |
| SafeRulesJson | jsonb | Field categories/rules, never secret values |
| Status | enum | Draft/Active/Superseded |
| ApprovedByAdminUserId | uuid nullable | Required for Active |
| ApprovedAt | timestamptz nullable | Required for Active |
| CreatedAt | timestamptz | Required |

**Constraints/indexes**:

- Partial unique Active index.
- Immutable after activation.
- Activation requires all secret-sentinel contract tests to pass.

### AdminAIConversation

Private conversation owned by exactly one Admin.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| OwnerAdminUserId | uuid | FK User Restrict; owner predicate on every query |
| Title | varchar(160) | Server-sanitized |
| Status | enum | Active/Archived |
| LastSequence | bigint | Starts at 0; monotonic per conversation |
| LastActivityAt | timestamptz | Sort/list cursor |
| ArchivedAt | timestamptz nullable | Required only when Archived |
| CreatedAt | timestamptz | Required |
| UpdatedAt | timestamptz | Required |
| Version | bigint | Optimistic concurrency, starts at 1 |

**Constraints/indexes**:

- Index (OwnerAdminUserId, Status, LastActivityAt DESC, Id).
- Check LastSequence >= 0 and Version > 0.
- Archive/restore only; no chat hard-delete endpoint.
- Owner role loss does not transfer ownership. Data becomes inaccessible until current Admin access is restored or handled outside the agent.

### AdminAIMessage

Visible, sanitized transcript item.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| ConversationId | uuid | FK Conversation Restrict |
| Sequence | bigint | Monotonic, allocated transactionally |
| Role | enum | Admin/Assistant/Status |
| Content | text | Bounded visible text; no raw secure input/provider prompt |
| StructuredContentJson | jsonb nullable | Closed safe answer/status references |
| TurnId | uuid nullable | FK Turn Restrict |
| CreatedAt | timestamptz | Required |

**Constraints/indexes**:

- Unique (ConversationId, Sequence).
- Unique TurnId for assistant terminal output when non-null.
- Check content and structured-payload sizes.
- Stored Admin prompt is the visible user message only; hidden instructions are never stored here.

### AdminAITurn

One Admin request and its orchestration lifecycle.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| ConversationId | uuid | FK Conversation Restrict |
| SourceMessageId | uuid | FK Message Restrict; unique |
| OutputMessageId | uuid nullable | FK Message Restrict; unique when non-null |
| ActorAdminUserId | uuid | FK User Restrict; equals conversation owner |
| CapabilityBaselineId | uuid | FK Baseline Restrict |
| SensitiveDataPolicyVersionId | uuid | FK Policy Restrict |
| ExpectedConversationVersion | bigint | Admission fingerprint component |
| ExpectedSecurityVersion | bigint | Actor security-version snapshot |
| Status | enum | Turn status |
| CurrentStepNumber | integer | Starts at 0 |
| ReadInvocationCount | integer | Max 6 |
| RedactedContextBytes | integer | Max 65,536 |
| CancellationRequestedAt | timestamptz nullable | Durable cancellation intent |
| CallbackIdempotencyDigest | char(64) | Keyed digest; unique |
| Provider | varchar(64) nullable | Safe metadata |
| Model | varchar(128) nullable | Safe metadata |
| ProviderResponseId | varchar(256) nullable | Safe provider ID only |
| InputTokenCount | integer nullable | Non-negative |
| OutputTokenCount | integer nullable | Non-negative |
| FailureCode | varchar(100) nullable | Allowlisted |
| SafeFailureDetail | varchar(500) nullable | No raw provider error |
| QueuedAt | timestamptz | Required |
| StartedAt | timestamptz nullable |  |
| CompletedAt | timestamptz nullable | Terminal time |
| Version | bigint | Optimistic concurrency |

**Constraints/indexes**:

- Unique SourceMessageId and CallbackIdempotencyDigest.
- Partial unique ConversationId while status is Queued/Planning/Retrieving/Answering/CancelRequested. One ordered active turn per conversation.
- Admission query limits active turns across all conversations for one Admin to two.
- Index (Status, QueuedAt), (ActorAdminUserId, Status), and (ConversationId, QueuedAt).
- Check all counters/budgets and terminal timestamps.

### AdminAITurnStep

Durable provider/tool-loop checkpoint.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| TurnId | uuid | FK Turn Restrict |
| StepNumber | integer | 1–3 |
| Status | enum | Step status |
| DecisionType | enum nullable | Closed decision branch |
| CanonicalDecisionHash | char(64) nullable | SHA-256 |
| ExpectedTurnVersion | bigint | Callback concurrency check |
| ToolCallsRequested | integer | 0–4 |
| Provider | varchar(64) nullable | Metadata |
| Model | varchar(128) nullable | Metadata |
| ProviderResponseId | varchar(256) nullable | Metadata |
| InputTokenCount | integer nullable | Non-negative |
| OutputTokenCount | integer nullable | Non-negative |
| LatencyMs | integer nullable | Non-negative |
| FailureCode | varchar(100) nullable | Allowlisted |
| CallbackStatus | varchar(32) | Pending/Delivered/Failed/Discarded |
| CallbackAttemptCount | integer | Non-negative |
| NextCallbackAttemptAt | timestamptz nullable | Recovery |
| StartedAt | timestamptz nullable |  |
| CompletedAt | timestamptz nullable |  |
| Version | bigint | Optimistic concurrency |

**Constraints/indexes**:

- Unique (TurnId, StepNumber).
- Maximum step 3 enforced by service and check constraint.
- No raw decision body or prompt. Canonical decision hash supports callback parity.

### AdminAIReadInvocation

One backend-executed typed read.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| TurnId | uuid | FK Turn Restrict |
| TurnStepId | uuid | FK Step Restrict |
| InvocationSequence | integer | 1–6 per turn |
| CapabilityKey | varchar(160) | Manifest key |
| CapabilityVersion | varchar(64) | Exact version |
| SafeInputJson | jsonb | Allowlisted normalized filters only |
| InputHash | char(64) | Canonical input digest |
| SafeScopeJson | jsonb | Domain/filter scope used in evidence |
| Status | enum | Invocation status |
| ResultCount | integer | Non-negative |
| IsComplete | boolean | Explicit completeness |
| IsTruncated | boolean | Explicit truncation |
| DataAsOf | timestamptz | Read snapshot time |
| SafeEvidenceJson | jsonb | Counts, aggregate facts, allowlisted refs |
| ProtectedResult | bytea nullable | Encrypted redacted tool result for durable resume |
| ProtectedResultHash | char(64) nullable | Keyed digest |
| ProtectedResultExpiresAt | timestamptz nullable | At most 24h after terminal turn |
| LatencyMs | integer | Non-negative |
| FailureCode | varchar(100) nullable | Allowlisted |
| TraceId | varchar(64) | Correlation |
| CreatedAt | timestamptz | Required |
| CompletedAt | timestamptz nullable |  |

**Constraints/indexes**:

- Unique (TurnId, InvocationSequence).
- Index (TurnId, InvocationSequence) and (CapabilityKey, CreatedAt).
- ProtectedResult is redacted before encryption and can never contain a prohibited category.
- Recovery can replay the exact bounded result while it exists. A purge service clears bytes/digest after the turn is terminal and at most 24 hours old, retaining safe evidence.

### AdminAIActionProposal

Server-built reviewable action with zero business effect until confirmed.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| ConversationId | uuid | FK Conversation Restrict |
| TurnId | uuid | FK Turn Restrict |
| ActorAdminUserId | uuid | FK User Restrict; owner/initiator |
| CapabilityBaselineId | uuid | FK Baseline Restrict |
| SensitiveDataPolicyVersionId | uuid | FK Policy Restrict |
| CapabilityKey | varchar(160) | Manifest key |
| CapabilityVersion | varchar(64) | Exact version |
| PrimaryRisk | enum | Strongest risk |
| RiskFlagsJson | jsonb | Closed flags |
| ConfirmationType | enum | Explicit/TypedStrong |
| SafeTargetType | varchar(100) | Allowlisted target family |
| SafeTargetReference | varchar(200) | Safe display/deep-link token |
| ProtectedNormalizedPayload | bytea | Authenticated encryption |
| PayloadHash | char(64) | Keyed canonical digest |
| StateFingerprint | char(64) | Target/concurrency/bulk membership digest |
| SafeCurrentStateJson | jsonb | Field allowlist |
| SafeRequestedStateJson | jsonb | Field allowlist |
| SafeEffectJson | jsonb | Consequences/count/amount/currency |
| ValidationSummaryJson | jsonb | Safe validation outcomes |
| BulkSemanticsJson | jsonb nullable | Selection/count/exclusions/atomicity/preview |
| SecureInputGrantId | uuid nullable | FK SecureInputGrant Restrict |
| Status | enum | Proposal status |
| ExpiresAt | timestamptz | Default 5 min; max 15 min |
| ConfirmedAt | timestamptz nullable |  |
| CancelledAt | timestamptz nullable |  |
| CompletedAt | timestamptz nullable |  |
| InvalidatedReasonCode | varchar(100) nullable | Allowlisted |
| FailureCode | varchar(100) nullable | Allowlisted |
| CreatedAt | timestamptz | Required |
| Version | bigint | Optimistic concurrency |

**Constraints/indexes**:

- Index (ActorAdminUserId, Status, ExpiresAt), (ConversationId, CreatedAt), and (Status, ExpiresAt).
- A turn may create several proposals, but an independent action has one proposal.
- Confirmation type is derived from catalog risk; model/client cannot lower it.
- Bulk StateFingerprint includes normalized selector, exact candidate IDs or stable membership digest, candidate count, relevant versions, and data snapshot.
- No direct route/SQL/type name is stored as executable authority; CapabilityKey resolves through the compiled registry.

### AdminAIConfirmationChallenge

Strong-confirmation verifier.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| ProposalId | uuid | FK Proposal Restrict; unique |
| PhraseDigest | char(64) | HMAC of normalized phrase |
| ChallengeVersion | varchar(16) | Normalization/format version |
| Status | enum | Challenge status |
| FailedAttemptCount | integer | 0–5 |
| LastAttemptAt | timestamptz nullable |  |
| AcceptedAt | timestamptz nullable |  |
| ExpiresAt | timestamptz | Not after proposal |
| Version | bigint | Concurrency |

**Constraints/indexes**:

- Unique ProposalId.
- Phrase text is generated from safe proposal fields/challenge material and never stored in plaintext.
- Five failed attempts lock the challenge and invalidate the proposal; a new proposal is required.

### AdminAISecureInputGrant

Short-lived secure material invisible to the agent.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| ProposalId | uuid | FK Proposal Restrict; unique |
| ActorAdminUserId | uuid | FK User Restrict |
| InputKind | varchar(64) | Allowlisted Password/File/ProtectedToken/etc. |
| TokenDigest | char(64) | One-time opaque token HMAC; unique |
| ProtectedPayload | bytea nullable | Authenticated encryption; never model-visible |
| PayloadHash | char(64) nullable | Keyed digest |
| SafeMetadataJson | jsonb | Filename/type/size or field presence only |
| Status | enum | Grant status |
| IssuedAt | timestamptz |  |
| SubmittedAt | timestamptz nullable |  |
| ConsumedAt | timestamptz nullable |  |
| ExpiresAt | timestamptz | Maximum 10 minutes |
| PurgedAt | timestamptz nullable | Required after purge |
| Version | bigint | Concurrency |

**Constraints/indexes**:

- Unique ProposalId and TokenDigest.
- Actor/proposal binding is checked on submit and consume.
- ProtectedPayload is cleared immediately after consume/cancel/expiry/final failure; row metadata remains for safe audit.
- File bytes remain in existing private storage; protected payload carries only a private object reference.

### AdminAIActionExecution

Durable at-most-one logical execution.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| ProposalId | uuid | FK Proposal Restrict; unique |
| ActorAdminUserId | uuid | FK User Restrict |
| CapabilityKey | varchar(160) | Exact catalog key |
| CapabilityVersion | varchar(64) | Exact version |
| IdempotencyDigest | char(64) | Actor-bound HMAC |
| PayloadHash | char(64) | Must equal proposal payload |
| AuthoritativeOperation | varchar(200) | Reviewed command/service identifier |
| Status | enum | Execution status |
| SafeResultJson | jsonb | Closed result union; no raw exception |
| AffectedCount | integer nullable | Non-negative |
| SucceededCount | integer nullable | Non-negative |
| SkippedCount | integer nullable | Non-negative |
| FailedCount | integer nullable | Non-negative |
| RefreshScopesJson | jsonb | Allowlisted query scopes |
| OriginalAuditLogId | uuid nullable | FK AuditLog Restrict |
| ExternalOperationId | varchar(200) nullable | Safe idempotent provider ID |
| FailureCode | varchar(100) nullable | Allowlisted |
| TraceId | varchar(64) | Correlation |
| ClaimedAt | timestamptz |  |
| StartedAt | timestamptz nullable |  |
| CompletedAt | timestamptz nullable | Terminal time |
| Version | bigint | Concurrency |

**Constraints/indexes**:

- Unique ProposalId.
- Unique (ActorAdminUserId, IdempotencyDigest).
- Duplicate equal payload returns this row; unequal payload rejects with IdempotencyPayloadConflict.
- Counts must reconcile for bulk results.
- RecoveryRequired never renders as success. A reconciler inspects the original operation by idempotency/external identity.

### AdminAIActionExecutionItem

Safe per-item evidence for authoritative bulk workflows with partial semantics.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key |
| ExecutionId | uuid | FK Execution Restrict |
| ItemSequence | integer | Stable within execution |
| SafeItemReference | varchar(200) | Allowlisted reference |
| ItemReferenceHash | char(64) | Prevent duplicate |
| Status | enum | Item outcome |
| SafeResultJson | jsonb | Bounded outcome |
| FailureCode | varchar(100) nullable | Allowlisted |

**Constraints/indexes**:

- Unique (ExecutionId, ItemSequence) and (ExecutionId, ItemReferenceHash).
- Do not create rows when original workflow is atomic and returns one outcome.

### AdminAIAuditEvent

Append-only, redacted lifecycle evidence.

| Field | Type | Rules |
|---|---|---|
| Id | uuid | Primary key/event ID |
| EventType | enum | Closed event type |
| ActorAdminUserId | uuid nullable | FK User Restrict |
| ConversationId | uuid nullable | FK Conversation Restrict |
| TurnId | uuid nullable | FK Turn Restrict |
| ReadInvocationId | uuid nullable | FK ReadInvocation Restrict |
| ProposalId | uuid nullable | FK Proposal Restrict |
| ExecutionId | uuid nullable | FK Execution Restrict |
| CapabilityKey | varchar(160) nullable | Safe key |
| SafeTargetReference | varchar(200) nullable | Safe reference |
| SafeEvidenceJson | jsonb | Allowlisted event body |
| EvidenceHash | char(64) | Canonical digest |
| CorrelationId | varchar(100) | Required |
| TraceId | varchar(64) | Required |
| RequestId | varchar(100) nullable |  |
| IpAddressHash | char(64) nullable | Keyed digest |
| OccurredAt | timestamptz | Required |

**Constraints/indexes**:

- Index (ConversationId, OccurredAt, Id), (ProposalId, OccurredAt), (ExecutionId, OccurredAt), (CapabilityKey, OccurredAt), and (ActorAdminUserId, OccurredAt).
- Application exposes insert/query only. No update/delete repository or agent capability.
- Database role/application tests reject mutation/deletion paths.
- Existing AuditLog receives a redacted summary linked by entity ID and CorrelationId.

## Existing Outbox integration

No new outbox table is required. Existing OutboxEvent carries:

- Type AdminAITurnQueued or an AdminAI safe realtime event type.
- TargetUserId owner Admin ID for realtime; null for queue dispatch.
- PayloadJson versioned minimal envelope with stable eventId/turnId/sequence and no transcript/proposal/tool content.
- Stable job identity derived from turn ID and queue name ai-admin-agent-turns.

## State transitions

### Conversation

    Active -> Archived -> Active

- Archive requests cancellation for active non-executing turns and cancels pending proposals.
- Archive cannot undo an authoritative operation already executing/committed.
- Archived conversation is read-only until restored.

### Turn

    Queued -> Planning -> Retrieving -> Planning
    Planning -> Answering -> Completed
    Planning -> WaitingClarification
    Planning -> ProposalReady
    Any active -> CancelRequested -> Cancelled
    Any active -> Failed
    Any active -> AccessRevoked

- Retrieving may repeat only within step/read budgets.
- WaitingClarification and ProposalReady are terminal for that turn; the next Admin message creates a new turn.
- A late callback for Cancelled, Failed, AccessRevoked, or superseded version is recorded as discarded and cannot add messages/proposals.

### Proposal and execution

    PendingSecureInput -> PendingConfirmation
    PendingConfirmation -> Confirming -> Executing -> Succeeded
                                                 -> PartiallySucceeded
                                                 -> Rejected
                                                 -> Failed
                                                 -> RecoveryRequired
    PendingSecureInput or PendingConfirmation -> Cancelled
    PendingSecureInput or PendingConfirmation -> Expired
    PendingSecureInput or PendingConfirmation or Confirming -> Invalidated

- Confirming begins only after actor/challenge/idempotency admission.
- Executing begins only after final state/capability/business revalidation and unique execution claim.
- Terminal execution result mirrors to proposal terminal state.
- RecoveryRequired means an ambiguous external outcome. UI cannot claim failure or success until reconciliation.

## Race and precedence rules

1. **Access revocation before execution claim**: invalidate proposal and create zero effect.
2. **Cancellation versus confirmation**: serializable transaction/row lock decides. If cancellation commits first, confirmation fails. If execution claim commits first, cancellation reports already executing/completed.
3. **Cancellation versus model callback**: committed cancellation/version change discards the callback.
4. **Duplicate confirmation**: matching idempotency/payload returns existing result; conflicting payload fails.
5. **State change versus confirmation**: fingerprint mismatch invalidates before execution.
6. **Bulk membership change**: membership/count/version difference invalidates and requires a new preview.
7. **Baseline/policy supersession**: pending proposal invalidates unless exact compatibility is explicitly recorded; default is invalidate.
8. **Provider success versus callback failure**: step stays provider-completed/pending callback and retries the canonical decision, not inference.
9. **External timeout**: execution becomes RecoveryRequired; only authoritative reconciliation resolves it.
10. **Concurrent conversations**: at most two active turns per Admin globally and one active turn per conversation.

## Retention and purge

- Conversations/messages: archive only; no automatic hard delete in v1. Future retention must preserve action-linked evidence and requires separate policy approval.
- Capability and sensitive-policy versions: immutable while referenced.
- Proposals/executions/audit: retained at least as long as existing AuditLog and the longer underlying domain rule; never deleted through chat.
- Protected read results: purge bytes/digest no later than 24 hours after terminal turn; retain safe scope/count evidence.
- Secure input: purge immediately after consume/cancel/expiry/failure, with a 10-minute absolute lifetime.
- Provider metadata: IDs/counts/latency/failure codes only; no raw prompt/response/reasoning/error.
- Metrics/logs: low-cardinality safe codes only; no message/tool/proposal content.

## Migration plan

1. Create AdminAI enums/tables in dependency order.
2. Add restricted foreign keys to User and AuditLog only; no link to live support/internal chat.
3. Add unique, partial, check, and lookup indexes above.
4. Extend IAppDbContext/AppDbContext and model snapshot.
5. Seed no conversation, capability, policy, secret, or user in the migration.
6. Activate baseline/policy through reviewed application workflow after contract gates, not hard-coded migration data.
7. Validate on a clean database, a representative current database, and old application binaries against the additive schema.
8. Migration failure blocks rollout; never delete volumes or reset data.

## Data-model test obligations

- EF mapping tests for required fields, lengths, enums, and delete behavior.
- PostgreSQL tests for partial uniques, row locks, serialization, optimistic concurrency, and bulk invalidation.
- Compatible/conflicting idempotency tests.
- Role removal/account disable/security-version race tests.
- Challenge normalization/HMAC/attempt-lock tests.
- Secure payload encryption, purpose separation, expiry, one-time consume, and purge tests.
- Secret sentinel tests across provider/transcript/audit/realtime/export.
- Recovery tests for queued, claimed, provider-completed, callback-pending, executing, and recovery-required rows.
- Migration tests from clean and representative existing snapshots.
