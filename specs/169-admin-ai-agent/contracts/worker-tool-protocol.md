# Worker and Tool Protocol

**Version**: 1
**Queue**: ai-admin-agent-turns
**Outbox event**: AdminAITurnQueued
**Authentication**: existing AI_CALLBACK_SECRET through an internal-token authorization attribute
**Authority**: the backend owns all data and effects; the worker owns provider inference only

## Non-negotiable boundaries

- The worker has no AdminAI PostgreSQL access.
- The worker cannot call public Admin controllers, MediatR commands, storage, WhatsApp, Bunny, finance, HR, or any state-changing service.
- The internal tool endpoint supports read capabilities only.
- Model-proposed actions return to the backend as untrusted suggestions. The backend independently builds proposals.
- No internal payload contains a password/hash, token, key, session material, verification answer/code, secure input, unredacted entity, unrestricted audit values, raw configuration, or connection data.
- Hidden instructions, provider reasoning, and raw provider errors are not returned to the browser or ordinary logs.
- All JSON objects use additionalProperties=false semantics and a schemaVersion discriminator.

## Internal routes

The planned controller uses the base route:

    /api/v1/internal/admin-ai

Routes:

| Method | Route | Purpose | Max request |
|---|---|---|---:|
| GET | /readiness | Backend protocol/baseline readiness | none |
| POST | /turns/{turnId}/claim | Claim or replay the durable turn/step | 1 KiB |
| POST | /turns/{turnId}/lease/renew | Renew backend orchestration lease | 2 KiB |
| POST | /turns/{turnId}/steps/{stepNumber}/reads | Validate and execute one bounded read batch | 64 KiB |
| POST | /turns/{turnId}/complete | Deliver one terminal closed decision | 256 KiB |
| POST | /turns/{turnId}/fail | Deliver a safe terminal worker/provider failure | 16 KiB |

All routes:

- require the exact internal token;
- have the admin-ai-internal distributed rate policy;
- reject browser cookies/bearer identity as authority;
- use strict content type and request-size limits;
- expose safe codes only;
- are not routed through the public frontend proxy.

## Queue job

Job name: respond

Job data:

    {
      "schemaVersion": "1",
      "turnId": "uuid",
      "conversationId": "uuid",
      "queuedAt": "ISO-8601 instant",
      "completion": null
    }

Rules:

- Stable BullMQ job ID is derived from queue name and turn ID.
- A persisted completion payload may be placed in job data before callback delivery so a callback retry does not invoke Gemini again.
- Queue age above the configured maximum fails the turn with AI_QUEUE_STALE; it does not silently run against an old actor/state.
- Worker concurrency is configured independently from live-support AI.
- Cancellation is checked before claim, before/after each provider call, before each read batch, and before completion callback.

## Claim

Request:

    POST /turns/{turnId}/claim
    {
      "schemaVersion": "1",
      "workerInstanceId": "bounded safe identifier"
    }

Successful response:

    {
      "schemaVersion": "1",
      "turnId": "uuid",
      "conversationId": "uuid",
      "actorAdminUserId": "uuid",
      "stepNumber": 1,
      "expectedTurnVersion": 4,
      "expectedConversationVersion": 12,
      "expectedSecurityVersion": 8,
      "capabilityBaseline": {
        "id": "uuid",
        "version": "2026-08-11.1",
        "manifestHash": "sha256"
      },
      "sensitiveDataPolicy": {
        "id": "uuid",
        "version": "2026-08-11.1",
        "policyHash": "sha256"
      },
      "leaseToken": "opaque one-time/replay-safe token",
      "leaseExpiresAt": "ISO-8601 instant",
      "callbackIdempotencyKey": "opaque bounded key",
      "deadlineAt": "ISO-8601 instant",
      "systemInstructions": "bounded hidden instructions",
      "messages": [
        {
          "role": "user|model",
          "content": "visible bounded text",
          "createdAt": "ISO-8601 instant"
        }
      ],
      "readTools": [
        {
          "key": "students.search",
          "descriptionAr": "bounded reviewed description",
          "parametersJsonSchema": {},
          "maxResultRecords": 25,
          "timeoutMs": 5000
        }
      ],
      "actionTools": [
        {
          "key": "student.note.add",
          "descriptionAr": "proposal-only reviewed description",
          "parametersJsonSchema": {},
          "confirmationType": "Explicit"
        }
      ],
      "budgets": {
        "maxModelSteps": 3,
        "maxReadCalls": 6,
        "maxReadCallsPerStep": 4,
        "remainingReadCalls": 6,
        "maxRedactedContextBytes": 65536,
        "remainingRedactedContextBytes": 65536
      }
    }

Claim behavior:

- Recheck feature enabled, active/non-deleted Admin role from PostgreSQL, ownership, security version, active baseline/policy, turn state, queue age, cancellation, and budgets.
- Claim is idempotent for the same active lease and returns the durable current step.
- A different live claimant receives TURN_LEASE_CONFLICT.
- Cancelled/access-revoked/terminal turns return 409/410 safe code and no context.
- System instructions are configuration/policy, not conversation messages, and are never persisted as visible transcript.
- Conversation messages are paged/bounded and contain only visible safe content.
- Tool schemas come from the exact active baseline; no route/type/SQL is included.

## Gemini function loop

1. Create an @google/genai request using only claim content and declared functions.
2. Mark all user messages and tool results as untrusted data.
3. Do not enable automatic function execution, MCP, Google Search, code execution, URL retrieval, or filesystem tools.
4. Parse response into exactly one closed decision.
5. For function calls, send a read batch to the backend. Convert each result to a FunctionResponse and continue.
6. Never synthesize a tool result when the backend rejects/fails it.
7. Stop when a terminal decision is obtained or a budget/deadline/cancellation is reached.

The model is never asked to compute final monetary totals or authoritative counts from raw rows when a backend aggregate capability exists.

## Read batch

Request:

    POST /turns/{turnId}/steps/{stepNumber}/reads
    {
      "schemaVersion": "1",
      "leaseToken": "opaque",
      "expectedTurnVersion": 4,
      "expectedBaselineVersion": "2026-08-11.1",
      "expectedSensitivePolicyVersion": "2026-08-11.1",
      "batchIdempotencyKey": "bounded stable key",
      "calls": [
        {
          "callId": "provider call identifier",
          "capabilityKey": "students.search",
          "arguments": {
            "query": "safe bounded value"
          }
        }
      ]
    }

Backend checks per batch and per call:

- lease, current turn/step/version, deadline, cancellation;
- current PostgreSQL Admin role/account/security version and conversation ownership;
- active baseline/policy and exact capability version;
- call count, total call count, byte/query/time budgets;
- strict input JSON schema, allowed filters/sorts/page size, identifier format;
- capability kind is Read/Preview/Export-safe, never Mutation;
- read projection field allowlist and prohibited-field policy;
- output shape/size/completeness and safe drill-down route mapping.

Successful response:

    {
      "schemaVersion": "1",
      "turnId": "uuid",
      "stepNumber": 1,
      "turnVersion": 5,
      "leaseToken": "renewed opaque token",
      "leaseExpiresAt": "ISO-8601 instant",
      "remainingBudgets": {
        "readCalls": 5,
        "redactedContextBytes": 62000
      },
      "results": [
        {
          "callId": "provider call identifier",
          "capabilityKey": "students.search",
          "status": "Succeeded|Empty|Truncated|Rejected|Failed",
          "data": {},
          "evidence": {
            "scope": [],
            "filters": [],
            "resultCount": 1,
            "isComplete": true,
            "isTruncated": false,
            "dataAsOf": "ISO-8601 instant",
            "drillDownReferences": []
          },
          "safeErrorCode": null
        }
      ]
    }

Rules:

- data is a closed, already-redacted capability DTO.
- A provider result never contains executable URLs. Drill-down is a routeKey plus safe parameters.
- Matching batch replay returns the same protected redacted result while retained.
- Same idempotency key with a different batch hash returns IDEMPOTENCY_PAYLOAD_CONFLICT.
- A rejected call consumes no result bytes but is audited.
- An Empty result is a successful authoritative result, not provider failure.

## Closed model decision schema

Common fields:

    {
      "schemaVersion": "1",
      "type": "answer|clarify|request_reads|propose_actions|refuse"
    }

### answer

    {
      "schemaVersion": "1",
      "type": "answer",
      "answer": {
        "summaryAr": "visible answer",
        "facts": ["..."],
        "calculations": ["..."],
        "inferences": ["..."],
        "limitations": ["..."],
        "suggestions": ["..."],
        "evidenceInvocationIds": ["uuid"]
      }
    }

Backend validation:

- Every data-backed claim references successful read evidence from this turn.
- Invocation IDs belong to the turn and active policy/baseline.
- No arbitrary link, HTML/script, hidden instruction, or success claim for an action.
- Answer/evidence lengths are bounded.
- Calculations requiring authority are present in capability result evidence, not invented.

### clarify

    {
      "schemaVersion": "1",
      "type": "clarify",
      "clarification": {
        "questionAr": "one focused question",
        "reasonCode": "AMBIGUOUS_TARGET|AMBIGUOUS_SCOPE|AMBIGUOUS_PERIOD|AMBIGUOUS_METRIC|MISSING_REQUIRED_INPUT",
        "options": [
          {
            "labelAr": "safe label",
            "value": "opaque safe choice"
          }
        ]
      }
    }

Maximum three safe options. Do not reveal whether a protected record exists through an unauthorized/partial lookup.

### request_reads

This branch is represented by Gemini function calls. If a JSON decision is also returned, it must contain only the exact call IDs and capability keys present in the provider function-call parts. The worker rejects discrepancies.

### propose_actions

    {
      "schemaVersion": "1",
      "type": "propose_actions",
      "messageAr": "safe proposal-intent summary",
      "actions": [
        {
          "clientActionId": "bounded identifier",
          "capabilityKey": "student.note.add",
          "arguments": {},
          "safeIntentAr": "what the Admin asked for"
        }
      ]
    }

Rules:

- Maximum five suggestions per turn.
- Each key and argument schema must exist in the claim action catalog.
- The worker/model cannot set risk, confirmation type, current/requested values, challenge phrase, expiry, state fingerprint, affected count, money, audit, execution status, or secure grant.
- Backend may reject, split, or require clarification; it independently previews and builds proposals.
- An existing authoritative bulk capability may remain one action. Independent actions are separate proposals.

### refuse

    {
      "schemaVersion": "1",
      "type": "refuse",
      "refusal": {
        "reasonCode": "PROHIBITED_SECRET|UNKNOWN_CAPABILITY|POLICY_BYPASS|RAW_DATABASE|INFRASTRUCTURE|UNSAFE_ATTACHMENT|OUT_OF_SCOPE",
        "messageAr": "safe explanation"
      }
    }

## Complete callback

Request:

    POST /turns/{turnId}/complete
    {
      "schemaVersion": "1",
      "leaseToken": "opaque",
      "expectedTurnVersion": 7,
      "expectedStepNumber": 2,
      "expectedBaselineVersion": "2026-08-11.1",
      "expectedSensitivePolicyVersion": "2026-08-11.1",
      "decision": {},
      "decisionHash": "sha256 canonical decision",
      "callbackIdempotencyKey": "opaque stable key",
      "provider": "gemini-developer",
      "model": "configured model",
      "providerResponseId": null,
      "inputTokenCount": null,
      "outputTokenCount": null,
      "latencyMs": 1200
    }

Backend behavior:

- Verify all expected versions, lease, decision schema/hash, actor access, cancellation, baseline/policy, evidence ownership, action keys, and budgets.
- Canonical identical replay returns the original callback outcome.
- Same callback key with a different decision hash conflicts.
- A cancelled/access-revoked/superseded turn discards the callback and creates no visible message/proposal.
- answer/clarify/refuse creates one safe assistant message.
- propose_actions creates only server-built proposals plus a safe status/assistant message.
- Persist message/proposals/audit/outbox realtime events atomically.

## Failure callback

Request:

    POST /turns/{turnId}/fail
    {
      "schemaVersion": "1",
      "leaseToken": "opaque",
      "callbackIdempotencyKey": "opaque stable key",
      "failureCode": "AI_PROVIDER_TIMEOUT|AI_PROVIDER_FAILURE|AI_INVALID_DECISION|AI_QUEUE_STALE|TOOL_BUDGET_EXCEEDED|CALLBACK_UNAVAILABLE|CANCELLED",
      "provider": null,
      "model": null,
      "latencyMs": 1200
    }

- Raw provider exception/message/body is not accepted.
- Retryable callback transport errors throw so BullMQ retries.
- Permanent provider/schema errors are recorded once and return a safe terminal state.
- Cancellation/access revocation outrank a late generic failure.

## Lease renewal

Renew requests contain schemaVersion, leaseToken, expectedTurnVersion, and workerInstanceId. The backend rechecks access/cancellation/baseline/deadline before extending a short lease. A lease is never renewed beyond the turn deadline. Losing the lease stops provider/tool work and prevents callback acceptance.

## Safe error codes

- TURN_NOT_FOUND
- TURN_NOT_CLAIMABLE
- TURN_LEASE_CONFLICT
- TURN_LEASE_EXPIRED
- TURN_CANCELLED
- ACCESS_REVOKED
- BASELINE_CHANGED
- SENSITIVE_POLICY_CHANGED
- STEP_VERSION_CONFLICT
- READ_CAPABILITY_NOT_ALLOWED
- READ_ARGUMENTS_INVALID
- READ_BUDGET_EXCEEDED
- REDACTED_CONTEXT_LIMIT
- READ_TIMEOUT
- DECISION_SCHEMA_INVALID
- DECISION_HASH_INVALID
- DECISION_EVIDENCE_INVALID
- ACTION_NOT_ALLOWED
- IDEMPOTENCY_PAYLOAD_CONFLICT
- CALLBACK_DISCARDED
- INTERNAL_RATE_LIMITED

No error includes SQL, stack traces, configuration values, connection details, provider body, or raw business data.

## Telemetry

Allowed low-cardinality metrics:

- queue age and stale count;
- claim/lease outcomes;
- model latency/provider/model/decision type;
- read latency/capability key/status/result size bucket;
- tool/model step counts and budget-exceeded count;
- callback delivered/retried/discarded;
- turn terminal state;
- proposal count/risk category;
- redaction-policy rejection count.

Forbidden labels/log fields:

- prompts/messages;
- tool arguments/results;
- person/entity names, phones, emails, addresses;
- proposal current/requested values;
- secure input metadata beyond safe size/type buckets;
- full conversation/turn/proposal IDs as high-cardinality metric labels;
- tokens, keys, raw IPs, raw provider errors.

Trace/correlation IDs are returned as safe opaque values and stored in redacted evidence.

## Protocol tests

- Exact worker/backend schema parity and canonical hashing.
- Unknown/extra field, excessive nesting/size, invalid enum, and invalid schema version rejection.
- Manual function loop with multiple reads, empty/truncated results, and backend rejection.
- No automatic/MCP/web/code tools present in provider request.
- Actor role removal, account disable, security-version change, cancellation, lease expiry, baseline/policy change at every callback boundary.
- Matching callback/read replay and conflicting payload rejection.
- Worker crash after provider completion and before callback; callback replay without a second inference.
- Secret sentinel absence from claim, provider request, function response, callback, logs, metrics, traces, and failure details.
- Deadline, maximum steps/calls/bytes, and per-read timeout.
- Prompt injection in user/stored text cannot change declared tools or system policy.
