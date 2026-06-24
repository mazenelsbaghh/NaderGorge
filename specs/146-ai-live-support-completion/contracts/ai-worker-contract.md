# Contract: AI Live Support Worker

## Queue

- Queue: `ai-live-support-turns`
- Job name: `respond`
- Job ID: `turn:{turnId}`
- Payload: `{ "schemaVersion": "1", "turnId": "uuid", "conversationId": "uuid", "queuedAt": "ISO-8601" }`
- Payload contains no transcript, prompt, account data, policy text, token, password, or verification answer.
- Configurable concurrency is isolated from video, mind-map, essay, and notification workers.
- Stale jobs older than the configured maximum queue age are claimed only so the backend can reconcile or hand off; they do not call the provider blindly.

## Internal Authentication

All claim, complete, and fail requests include `X-Internal-Token` using `AI_CALLBACK_SECRET`. Startup and readiness fail when the secret is absent, shorter than 32 characters, or equal to a documented placeholder. Response bodies and logs never echo it.

## Claim

`POST /api/v1/internal/callbacks/live-support-ai/turns/{turnId}/claim`

Success returns:

```json
{
  "schemaVersion": "1",
  "turnId": "uuid",
  "conversationId": "uuid",
  "policyVersionId": "uuid",
  "expectedConversationVersion": 12,
  "callbackIdempotencyKey": "uuid-or-digest",
  "deadlineAt": "ISO-8601",
  "systemInstructions": "bounded string",
  "knowledgeDocuments": [
    { "revisionId": "uuid", "title": "safe title", "content": "bounded untrusted content" }
  ],
  "studentContext": { "allowed.category": {} },
  "messages": [
    { "senderType": "Student", "content": "bounded string", "sentAt": "ISO-8601" }
  ],
  "allowedActions": [
    { "key": "stable.key", "descriptionAr": "...", "argumentsSchema": {} }
  ],
  "allowedDecisionTypes": ["reply", "propose_action", "request_verification", "propose_account_creation", "request_resolution", "handoff"]
}
```

Claim outcomes:

- `200`: claimed or replayed processing context.
- `404`: unknown turn.
- `409`: terminal, stale, disabled, handed off, or already claimed by a live lease. Body contains a stable safe code.
- `413`: context cannot be bounded safely; backend initiates defined recovery/handoff.

## Decision Union

All decisions require `schemaVersion: "1"`, `type`, and optional bounded `messageAr`. Extra top-level or branch fields are rejected.

### Reply

```json
{ "schemaVersion": "1", "type": "reply", "messageAr": "1..4000 chars" }
```

### Propose action

```json
{
  "schemaVersion": "1",
  "type": "propose_action",
  "messageAr": "optional",
  "action": {
    "key": "published catalog key",
    "arguments": {},
    "safeEffectSummaryAr": "1..1000 chars",
    "safeConsequenceAr": "optional, <=1000 chars"
  }
}
```

### Request verification

```json
{
  "schemaVersion": "1",
  "type": "request_verification",
  "messageAr": "optional",
  "verification": { "intent": "existing_account" }
}
```

The model never receives or selects expected answers.

### Propose account creation

```json
{
  "schemaVersion": "1",
  "type": "propose_account_creation",
  "messageAr": "optional",
  "accountCreation": { "requestedFields": ["server-catalog-key"] }
}
```

Passwords and form values are not part of this decision.

### Request resolution

```json
{
  "schemaVersion": "1",
  "type": "request_resolution",
  "messageAr": "optional",
  "resolution": { "reasonCode": "resolved", "safeSummaryAr": "1..1000 chars" }
}
```

### Handoff

```json
{
  "schemaVersion": "1",
  "type": "handoff",
  "messageAr": "optional",
  "handoff": { "reasonCode": "stable-code", "safeSummaryAr": "1..2000 chars", "forced": false }
}
```

The model may propose normal handoff. Only backend-classified failure/lockout can force handoff.

## Provider Rules

- Use existing provider gateway operation `live-support`.
- Primary and optional quota fallback follow installed provider configuration.
- Provider deadline is enforced with abort semantics.
- Only classified transient provider failure permits one inference retry.
- Schema, authorization, unsafe output, and callback failures never trigger inference retry.
- Store only provider name, model, safe response ID, token counts, latency, decision hash, and safe outcome code.

## Complete Callback

`POST /api/v1/internal/callbacks/live-support-ai/turns/{turnId}/complete`

Body includes expected conversation/policy versions, canonical validated decision, decision hash, provider metadata, callback idempotency key, and latency. Maximum body is bounded. The worker persists this callback payload in its job result before attempting delivery so network retry does not repeat inference.

Outcomes:

- `200`: completed, replayed, or safely discarded; response includes stable outcome.
- `409`: same callback key with different decision hash.
- `413`: rejected body.
- `422`: schema/catalog validation failed.

## Fail Callback

`POST /api/v1/internal/callbacks/live-support-ai/turns/{turnId}/fail`

Body contains only stable `failureCode`, provider/model where safe, latency, and callback idempotency key. It never contains raw exception message, prompt, response, URL with secrets, or personal data.

## Telemetry

Allowed dimensions: queue name, job/turn ID, conversation ID, safe failure code, provider, model, decision type, retry count, queue age bucket, latency bucket, callback outcome. Forbidden dimensions: prompt, message text, name, phone, student code, password, verification answer, token, raw provider body.
