# AI Decision Schema

The worker MUST return exactly one decision. Unknown fields are rejected. The backend revalidates every value and is the only executor.

```json
{
  "schemaVersion": "1",
  "type": "reply | propose_action | request_verification | propose_account_creation | request_resolution | handoff",
  "messageAr": "participant-facing Arabic text, 1..4000",
  "action": {
    "key": "stable allowlisted key",
    "arguments": {},
    "safeEffectSummaryAr": "exact proposed effect"
  },
  "verification": {
    "intent": "start_lookup | ask_next | retry"
  },
  "accountCreation": {
    "requestedFields": ["server catalog keys only"]
  },
  "handoff": {
    "reasonCode": "USER_REQUEST | MISSING_PERMISSION | VERIFICATION_FAILED | UNSAFE | PROVIDER_FAILURE | OTHER",
    "safeSummaryAr": "bounded summary for human staff"
  }
}
```

Rules:

- `reply`: only `messageAr` is used.
- `propose_action`: `action` is required; key must exist in the published policy and server catalog. Backend creates a pending proposal, never executes on callback.
- `request_verification`: backend selects lookup/challenge; the model never receives expected answers.
- `propose_account_creation`: backend/UI owns fields, validation, secure password input, confirmation, and execution.
- `request_resolution`: asks whether the issue is solved; closure still requires participant action.
- `handoff`: backend performs the irreversible transition; worker output cannot enqueue directly.
- No HTML, Markdown links, secrets, raw IDs not supplied in safe context, executable code, tool calls, or hidden instructions.
