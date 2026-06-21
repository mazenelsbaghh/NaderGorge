# Data Model: AI Live Support Agent

All timestamps are UTC. All mutable roots carry optimistic concurrency. Foreign-key deletion is `Restrict` when history/audit references the row. Published records are immutable. Safe JSON is allowlisted and length-bounded.

## Enums

### `LiveSupportAIPolicyStatus`

`Draft`, `Published`, `Superseded`

### `LiveSupportAIMode`

`AiActive`, `HumanQueued`, `HumanAssigned`, `AiResolved`, `Failed`, `Closed`

`HumanQueued`, `HumanAssigned`, `AiResolved`, and `Closed` cannot transition back to `AiActive` in the same conversation.

### `LiveSupportAITurnStatus`

`Queued`, `Processing`, `Completed`, `Failed`, `DiscardedAfterHandoff`, `DiscardedAfterDisable`

### `LiveSupportAIDecisionType`

`Reply`, `ProposeAction`, `RequestVerification`, `ProposeAccountCreation`, `RequestResolution`, `Handoff`

### `LiveSupportAIPendingActionStatus`

`PendingConfirmation`, `Confirmed`, `Cancelled`, `Expired`, `Invalidated`, `Executing`, `Succeeded`, `Failed`

### `LiveSupportAIVerificationStatus`

`AwaitingLookup`, `Challenging`, `Verified`, `Failed`, `Exhausted`, `Ambiguous`, `Cancelled`, `HandedOff`

Extend existing `LiveSupportSenderType` with `AI`. Extend `LiveSupportEventType` with explicit AI policy/turn/reply/proposal/confirmation/verification/handoff/inactivity/resolution events.

## Entities

### `LiveSupportAIPolicyVersion`

| Field | Type | Rules |
|---|---|---|
| `Id` | guid | PK |
| `VersionNumber` | long | Unique, monotonic |
| `Status` | enum | Draft/Published/Superseded |
| `IsEnabled` | bool | Only active published policy admits AI turns |
| `SystemInstructions` | string | 1–20,000; never returned to staff/participant |
| `ReadableDataKeysJson` | safe JSON array | Stable catalog keys, max 100 |
| `ActionKeysJson` | safe JSON array | Stable catalog keys, max 100 |
| `LookupKeysJson` | safe JSON array | Stable safe lookup keys |
| `VerificationQuestionKeysJson` | safe JSON array | Stable non-secret keys |
| `VerificationRequiredCorrect` | int | 1..question count |
| `VerificationMaxAttempts` | int | 1..10, default 3 |
| `PendingActionExpirySeconds` | int | 30..900 |
| `InactivityMinutes` | int | 5..1440 |
| `InactivityWarningGraceSeconds` | int | 30..600 |
| `CreatedByUserId` | guid | Built-in Admin |
| `PublishedByUserId` | guid? | Required when Published |
| `CreatedAt`, `PublishedAt` | datetime | Canonical timestamps |
| `Version` | long | Concurrency token |

Indexes: unique `VersionNumber`; filtered unique row where `Status=Published` and active selector is true. Publication supersedes prior active version atomically.

### `LiveSupportAIKnowledgeEntry`

| Field | Type | Rules |
|---|---|---|
| `Id` | guid | Stable logical entry |
| `Title` | string | 1–200 |
| `CreatedByUserId` | guid | Admin |
| `CreatedAt`, `UpdatedAt` | datetime | |
| `Version` | long | Concurrency token |

### `LiveSupportAIKnowledgeRevision`

| Field | Type | Rules |
|---|---|---|
| `Id` | guid | PK |
| `EntryId` | guid | FK Restrict |
| `RevisionNumber` | int | Unique per entry |
| `Content` | string | 1–50,000 |
| `SourceLabel` | string? | Max 300; no secret URLs |
| `SearchText` | string | Normalized searchable content |
| `ContentHash` | string | SHA-256 unique per entry revision |
| `IsPublished` | bool | Published revision immutable |
| `ValidFrom`, `ValidUntil` | datetime? | Optional publication window |
| `CreatedByUserId`, `PublishedByUserId` | guid | Admin |
| `CreatedAt`, `PublishedAt` | datetime | |

Indexes: unique `(EntryId, RevisionNumber)`; search index on normalized searchable content; filtered published/valid index.

### `LiveSupportAIPolicyKnowledgeRevision`

Join table `(PolicyVersionId, KnowledgeRevisionId)` with composite PK. A turn records the actual subset retrieved.

### `LiveSupportAIConversationState`

| Field | Type | Rules |
|---|---|---|
| `ConversationId` | guid | PK/FK, one state per conversation |
| `Mode` | enum | State machine below |
| `PolicyVersionId` | guid | Immutable version used for admission/current turn |
| `VerifiedStudentUserId` | guid? | Conversation-only verification result |
| `LastParticipantActivityAt` | datetime | Drives inactivity |
| `InactivityWarningSentAt`, `AutoCloseAt` | datetime? | Cleared by participant activity |
| `HandoffReasonCode`, `HandoffSafeSummary` | string? | Safe bounded values |
| `HandedOffAt`, `ResolvedAt` | datetime? | Terminal facts |
| `ResolutionCode` | string? | `ParticipantConfirmed`, `Inactivity`, etc. |
| `SafeSummaryJson` | safe JSON? | No instructions/raw verification/secrets |
| `Version` | long | Concurrency token |

Indexes: `(Mode, AutoCloseAt)`, `(Mode, LastParticipantActivityAt)`, `(PolicyVersionId)`.

### `LiveSupportAITurn`

| Field | Type | Rules |
|---|---|---|
| `Id` | guid | PK and deterministic worker identity |
| `ConversationId` | guid | FK Restrict |
| `SourceMessageId` | guid | Unique, one turn per participant message |
| `PolicyVersionId` | guid | Captured at enqueue |
| `ExpectedConversationVersion` | long | Callback precondition |
| `Status` | enum | Turn lifecycle |
| `DecisionType` | enum? | Set on validated completion |
| `OutputMessageId` | guid? | Unique optional AI message |
| `ContextCategoryKeysJson` | safe JSON | Keys only |
| `KnowledgeRevisionIdsJson` | safe JSON | IDs only |
| `Provider`, `Model`, `ProviderResponseId` | string? | Safe operational metadata |
| `InputTokenCount`, `OutputTokenCount`, `LatencyMs` | int? | Metrics |
| `FailureCode`, `SafeFailureDetail` | string? | No raw provider output |
| `QueuedAt`, `StartedAt`, `CompletedAt` | datetime | |
| `Version` | long | Concurrency token |

Indexes: unique `SourceMessageId`; unique nullable `OutputMessageId`; `(Status, QueuedAt)`; `(ConversationId, QueuedAt)`.

### `LiveSupportAIPendingAction`

| Field | Type | Rules |
|---|---|---|
| `Id` | guid | Proposal identity shown to participant |
| `ConversationId`, `TurnId` | guid | FK Restrict |
| `StudentUserId` | guid | Must equal linked/verified account |
| `PolicyVersionId` | guid | Capability version |
| `ActionKey` | string | Stable action catalog key |
| `SafeProposalJson` | safe JSON | Exact displayed target/effect; no secret |
| `EncryptedPayload` | bytes? | Only when necessary; app-key protected, never logs/provider |
| `PayloadHash`, `StateFingerprint` | string | SHA-256 |
| `ConfirmationNonceHash` | string | Never store raw nonce |
| `IdempotencyKey` | guid | Unique |
| `Status` | enum | State machine below |
| `ExpiresAt`, `ConfirmedAt`, `CompletedAt` | datetime? | |
| `ConfirmedByUserId`, `ConfirmedByGuestSessionId` | guid? | Exactly one participant identity |
| `ActionExecutionId` | guid? | Unique link to existing execution/audit |
| `FailureCode` | string? | Safe code |
| `Version` | long | Concurrency token |

Indexes: unique `IdempotencyKey`; unique nullable `ActionExecutionId`; filtered unique pending proposal per conversation if product UI permits one at a time; `(ConversationId, Status)`.

### `LiveSupportAIVerificationPolicyQuestion`

| Field | Type | Rules |
|---|---|---|
| `Id` | guid | PK |
| `PolicyVersionId` | guid | FK Restrict |
| `QuestionKey` | string | Server allowlist key |
| `PromptText` | string | 1–300, no expected answer/hint |
| `SourceFieldKey` | string | Non-secret server catalog key |
| `ComparisonMode` | enum | `ExactNormalized`, `Date`, `PhoneDigits`, catalog-controlled |
| `Order` | int | Unique per policy |

### `LiveSupportAIVerificationSession`

| Field | Type | Rules |
|---|---|---|
| `Id` | guid | PK |
| `ConversationId` | guid | Unique: one session per conversation |
| `PolicyVersionId` | guid | FK Restrict |
| `CandidateStudentUserId` | guid? | Never exposed before success |
| `LookupKey` | string | Key only |
| `LookupValueHash` | string | HMAC, never raw lookup value |
| `SelectedQuestionKeysJson` | safe JSON | No answers |
| `RequiredCorrect`, `CorrectCount`, `AttemptCount`, `MaxAttempts` | int | Bounded |
| `Status` | enum | Verification lifecycle |
| `ExpiresAt`, `VerifiedAt`, `CompletedAt` | datetime? | Conversation-only |
| `Version` | long | Concurrency token |

### `LiveSupportAIVerificationAttempt`

Append-only: `Id`, `SessionId`, `QuestionKeysJson`, `OutcomeCodesJson`, `SubmittedAt`, `AttemptNumber`. It MUST NOT contain submitted or expected raw answers.

## State Transitions

### Conversation AI mode

```text
AiActive -> HumanQueued -> HumanAssigned -> Closed
AiActive -> AiResolved -> Closed
AiActive -> Failed -> HumanQueued
```

No transition returns to `AiActive`. Disable, user request, missing permission, unsafe output, provider exhaustion, verification failure/exhaustion, or action requiring unavailable capability enters handoff.

### Turn

```text
Queued -> Processing -> Completed
Queued|Processing -> Failed
Queued|Processing -> DiscardedAfterHandoff
Queued|Processing -> DiscardedAfterDisable
```

### Pending action

```text
PendingConfirmation -> Confirmed -> Executing -> Succeeded|Failed
PendingConfirmation -> Cancelled|Expired|Invalidated
Confirmed -> Invalidated (only before execution when revalidation fails)
```

### Verification

```text
AwaitingLookup -> Challenging -> Verified
AwaitingLookup|Challenging -> Failed|Exhausted|Ambiguous|Cancelled|HandedOff
```

Verified is valid only for the same `ConversationId` and cannot create an authentication token.

## Redaction and Retention

- Never persist raw verification answers, expected answers in turn/audit, passwords, tokens, provider credentials, full hidden system instructions in staff evidence, or disallowed account fields.
- Preserve published policy/knowledge revisions, turn decisions, safe action proposals, verification outcomes, and handoff evidence as audit history while referenced.
- Provider request/response bodies are not logged. Store category keys, revision IDs, response IDs, token counts, latency, decision type, and safe error codes.
- Use bounded safe summaries for long conversations; the full durable chat remains the user-visible record.
