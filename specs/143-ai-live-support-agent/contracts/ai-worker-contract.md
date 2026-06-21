# AI Worker Contract

## Queue

- Redis stream job type: `live support turn`
- BullMQ queue: `ai-live-support-turns`
- Job name: `respond`
- Deterministic job ID: `live-support-turn:{turnId}`
- Payload: `{ turnId, conversationId, queuedAt, schemaVersion: "1" }`
- No participant text, account data, policy instructions, verification answer, or secret is stored in Redis.

## Context claim

`POST /api/v1/internal/live-support-ai/turns/{turnId}/claim`

Headers: scoped internal token, callback idempotency ID, timestamp. Response is one bounded context packet:

- turn/conversation/policy IDs and expected versions,
- system instructions,
- safe transcript window and summary,
- selected published knowledge revisions,
- allowlisted context sections only,
- allowed action/read/verification catalog keys,
- participant type and safe conversation state,
- deadline and decision schema version.

Claim is idempotent for the same worker attempt and denied after handoff, disable, expiry, or terminal state.

## Completion

`POST /api/v1/internal/live-support-ai/turns/{turnId}/complete`

Body: expected conversation/policy versions, strict decision, provider/model/response ID, token counts, latency, generated timestamp, callback idempotency key. Duplicate identical callback replays success. Conflicting callback fails. Backend may record `discarded` when handoff/disable already won.

## Failure

`POST /api/v1/internal/live-support-ai/turns/{turnId}/fail`

Body: safe failure category/code, provider/model, latency, retry exhausted flag, callback idempotency key. Never send stack traces, prompt, raw output, or credentials. Backend performs durable handoff when unrecoverable.

## Retry and timeout

- Provider deadline: 6 seconds.
- One bounded retry only for a transient category while within turn deadline.
- Callback delivery retries separately without repeating inference.
- Queue-age expiry produces failure/handoff.
- Worker cancellation is advisory; backend mode/version guard is authoritative.
