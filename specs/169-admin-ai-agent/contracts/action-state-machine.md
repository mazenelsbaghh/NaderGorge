# Action Proposal, Confirmation, and Execution Contract

**Scope**: Every state-changing AdminAI capability.
**Core invariant**: No business, external, file, notification, queue, financial, security, or account effect may occur before a valid durable confirmation is accepted.

## Actors

- Initiating Admin: the current built-in Admin who owns the conversation and created the request.
- Backend proposal builder: validates capability and constructs the authoritative preview.
- Confirmation service: verifies actor, phrase, expiry, versions, idempotency, and state.
- Capability adapter: invokes the original authoritative MediatR/application service.
- Model/worker: may suggest a capability and safe arguments but cannot determine risk, phrase, preview, execution, or result.

Only the initiating Admin may provide secure input, confirm, or cancel the proposal.

## Proposal creation

For each independent requested mutation:

1. Revalidate current PostgreSQL Admin access and conversation ownership.
2. Resolve the exact capability key/version from the active baseline.
3. Validate the model suggestion against the closed action input schema.
4. Load the target through the authoritative application boundary.
5. Apply the original operation's validation and preview rules without causing a business/external effect.
6. Compute:
   - safe target reference;
   - current and requested safe fields;
   - affected count;
   - money/currency where applicable;
   - downstream/irreversible consequences;
   - validation summary;
   - risk flags and confirmation type;
   - state/bulk membership fingerprint;
   - protected normalized payload and keyed digest;
   - baseline and sensitive-policy versions;
   - expiration;
   - secure-input requirement.
7. Persist proposal, challenge if required, audit event, and owner realtime notification atomically.

The proposal preview may write only AdminAI workflow/evidence rows. A no-side-effect test uses EF command interception and fake queue/storage/provider clients to prove it did not execute the business operation.

## Proposal display contract

Every proposal card shows:

- capability label and stable safe key;
- target safe label and allowlisted deep link;
- current value/state and requested value/state;
- effect and consequence;
- all risk labels with icon and text;
- affected item count;
- exact EGP amount/currency/precision when relevant;
- validation outcomes;
- expiration absolute time and remaining duration;
- confirmation method;
- secure-input requirement/status;
- for bulk: selection rule, candidate/excluded counts, representative sample, Atomic/Partial semantics, and failure behavior;
- authoritative terminal execution result when present.

The UI never parses raw proposal JSON or invents labels/links. It renders typed DTOs.

## Confirmation classification

### Explicit confirmation

Used only when all risk flags are Ordinary.

- UI button names the exact operation, for example “تأكيد إضافة الملاحظة”.
- The button sends proposal version and stable Idempotency-Key.
- A generic “نعم” button is forbidden.

### Typed strong confirmation

Required if any flag is:

- Destructive
- Financial
- Permission
- Security
- AccountDisable
- Credential
- Bulk
- consequential ExternalSideEffect

Server phrase format version 1:

    أؤكد تنفيذ {safeActionLabel} — {challenge}

Challenge:

- eight uppercase unambiguous ASCII characters from a reviewed alphabet that excludes visually confusing values;
- cryptographically random per proposal;
- never model-generated;
- cannot be reused across proposals;
- visible only in the owner proposal DTO;
- plaintext is not persisted; only a purpose-separated HMAC digest.

Normalization before HMAC comparison:

1. Unicode NFC.
2. Trim leading/trailing Unicode whitespace.
3. Collapse each internal Unicode whitespace run to one ASCII space.
4. Do not lowercase/uppercase.
5. Do not change Arabic/Latin digits.
6. Do not remove/change punctuation or diacritics.
7. Do not apply fuzzy/substring/locale matching.

Five failed attempts lock and invalidate the proposal. The UI may locally compare to enable the button, but the server is authoritative.

## Secure continuation

When the authoritative operation needs a password, protected token/answer, or private file:

1. Proposal enters PendingSecureInput.
2. Owner requests a one-time short-lived secure grant.
3. Accessible secure overlay or original approved screen collects the value.
4. Sensitive endpoint disables request-body logging/tracing capture and returns no value.
5. Backend validates type/size/malware/file policy and protects the value/reference with actor/proposal binding.
6. Proposal moves to PendingConfirmation.
7. Final execution consumes the grant once.
8. Protected payload is purged immediately after consume/cancel/expiry/final failure.

The chat message, model, tool result, browser store/cache, realtime event, audit payload, metrics, and logs never contain the value.

## State machine

    PendingSecureInput
      -> PendingConfirmation after valid secure submission
      -> Cancelled
      -> Expired
      -> Invalidated

    PendingConfirmation
      -> Confirming
      -> Cancelled
      -> Expired
      -> Invalidated

    Confirming
      -> Executing
      -> Invalidated
      -> Rejected

    Executing
      -> Succeeded
      -> PartiallySucceeded
      -> Rejected
      -> Failed
      -> RecoveryRequired

Terminal:

- Succeeded
- PartiallySucceeded
- Cancelled
- Expired
- Invalidated
- Rejected
- Failed

RecoveryRequired is non-success and operationally pending until reconciled to a terminal state.

## Confirmation transaction

The confirmation handler must:

1. Begin the original operation's compatible serializable/row-lock transaction boundary.
2. Load proposal with actor/owner predicate and lock/version.
3. Check feature enabled and current live Admin access/account/security version.
4. Check status, expiry, attempts, exact baseline/capability/policy compatibility.
5. Bind Idempotency-Key to actor, proposal, confirmation request, and payload hash.
6. Return an existing compatible execution/result if already claimed/completed.
7. Reject the same key with a different payload/actor/proposal.
8. Verify explicit or typed challenge.
9. Verify secure grant, if any, is submitted, valid, actor-bound, unconsumed, and unexpired.
10. Decrypt and authenticate normalized payload using its keyed digest.
11. Revalidate closed input schema and original business rules.
12. Reload target and recompute state/bulk membership fingerprint.
13. If anything differs, invalidate with zero business effect and return current safe state.
14. Create/claim the unique execution row and set Confirming/Executing.
15. Invoke the exact authoritative command/service using initiating Admin ID and deterministic operation idempotency identity.
16. Persist original operation result/audit plus AdminAI result/audit and realtime event atomically where the original transaction permits.
17. Purge secure payload.

The model, worker callback, client optimistic state, or queue completion can never mark execution success.

## Idempotency contract

Identities:

- Public request Idempotency-Key: stable for retries of one send/confirm/cancel/secure intent.
- Proposal payload hash: canonical actor/capability/target/normalized input/state/baseline/policy digest.
- Execution unique ProposalId.
- Actor plus idempotency digest unique index.
- Original operation idempotency identity derived from execution ID and capability version.

Outcomes:

| Existing state | Incoming match | Result |
|---|---|---|
| No execution | Valid confirmation | Claim one execution |
| Claimed/Executing | Same actor/key/payload | Return current authoritative execution |
| Terminal | Same actor/key/payload | Replay exact terminal result |
| Any | Same key, different actor/proposal/payload | Reject IdempotencyPayloadConflict |
| Cancelled/Expired/Invalidated | Any confirmation | Reject; zero effect |

UI disabling prevents accidental double click but is not part of the guarantee.

## State fingerprint contract

Minimum fingerprint inputs:

- capability key/version;
- target type/stable identifier;
- target concurrency/version value;
- normalized fields that affect validation/effect;
- actor and relevant permission/security version;
- active baseline/policy;
- original operation confirmation/contract version;
- for financial work: period/status/currency/posting/document references;
- for bulk: normalized selector, exact stable membership digest, count, exclusions, relevant row versions;
- for external work: existing job/provider/idempotency state.

Sensitive values are represented by keyed digests, not plaintext.

Any relevant difference invalidates the proposal. The service never silently refreshes the fingerprint and executes the old confirmation.

## Bulk operations

- Independent requested operations do not become an artificial bulk action.
- Only a current authoritative bulk workflow may be one proposal.
- Proposal must disclose Atomic or Partial semantics.
- Membership is frozen by stable digest and re-evaluated at confirmation.
- If membership/count changes, proposal invalidates even if the change appears harmless.
- Atomic workflow returns one success/rejection/failure and rolls back as defined by original service.
- Partial workflow returns per-item Succeeded, Skipped, ValidationFailed, AuthorizationFailed, Stale, DependencyFailed, or SystemFailed.
- Failed incompatible items are not silently retried.
- Summary counts must reconcile with item evidence.

## Financial operations

All financial mutations require TypedStrong and preserve:

- EGP/currency and decimal precision;
- source document/reference;
- ledger/accounting dimensions;
- open/closed period controls;
- posting/reversal rather than destructive alteration where required;
- immutable original financial evidence;
- balance/treasury/teacher liability reconciliation;
- original approval and audit;
- deterministic idempotency and result recovery.

The model never calculates an executable amount from prose alone. Backend preview returns the authoritative amount.

## External side effects

Before catalog inclusion, an external operation must:

- accept deterministic idempotency or a recoverable provider/job identity;
- separate request admission from effect result;
- expose authoritative status/reconciliation;
- never retry ambiguous non-idempotent effects blindly;
- return RecoveryRequired after an ambiguous timeout;
- link queue/file/message/provider evidence safely.

Examples include WhatsApp sends, Bunny operations, AI analysis/mind-map jobs, private uploads/publication, and external wallet/payment behavior.

## Cancellation and race precedence

### Pending proposal

Cancel locks the proposal and commits Cancelled before zero-effect cleanup/purge. Any later confirmation fails.

### Confirmation race

- If cancellation commits before execution claim: zero effect.
- If execution claim commits first: cancellation returns Executing or terminal result; it cannot claim cancellation.
- If access is revoked before claim: invalidate with zero effect.
- If access is revoked after authoritative operation has committed: preserve the committed result/audit and deny further feature access.

### Provider/turn race

A cancelled/access-revoked turn version rejects late callbacks and action suggestions. It cannot create a proposal.

## Failure/result contract

Closed public outcomes:

- Succeeded
- PartiallySucceeded
- ValidationRejected
- StaleRejected
- AuthorizationRejected
- Cancelled
- Expired
- Invalidated
- DependencyFailed
- ProviderFailed before proposal
- RecoveryRequired
- UnknownSafeFailure

Rules:

- Partial is never labeled full success.
- RecoveryRequired is never labeled failure/success until reconciled.
- Raw exceptions/provider bodies/SQL/stack/config are never public.
- A safe trace ID is always present.
- Retry guidance is explicit and allowed only when compatible.
- Terminal result remains visible after refresh through REST snapshot.

## Audit events

At minimum:

- proposal created with safe preview/risk/expiry;
- secure grant issued/submitted/consumed/purged without value;
- every confirmation attempt accepted/rejected/locked;
- cancellation/expiry/invalidation with safe reason;
- execution claim/start/result/recovery;
- original operation audit ID;
- actor, capability, safe target, baseline/policy, timestamps, trace/correlation, payload/evidence hashes.

Private transcript content is not copied into shared action audit.

## Required tests per action

1. Proposal preview produces no business/external effect.
2. Correct original validation/permission/transaction behavior.
3. Ordinary versus strong risk derivation cannot be client/model-downgraded.
4. Correct phrase; empty/wrong/old/case/punctuation/digit/whitespace/fuzzy attempts.
5. Five-attempt lock.
6. Cancel/expiry/stale/baseline/policy/role/security-version invalidation.
7. Matching retry and conflicting idempotency.
8. Two tabs/two Admins/row change/restart/callback retry.
9. Secure input one-time binding/purge/no-leak, when applicable.
10. Financial/bulk/external specialized invariants.
11. Result/audit/refresh-scope parity with original screen.
12. No secret sentinel in provider/transcript/proposal/audit/log/metric/realtime/export.
