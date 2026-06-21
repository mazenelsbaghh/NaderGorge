# AI Live Support Hub Events

Use existing authenticated SignalR groups and durable snapshot reconciliation. Events are notifications, never source of truth. Clients deduplicate by entity/message IDs and fetch missed state after reconnect.

| Event | Audience | Payload |
|---|---|---|
| `AITurnStarted` | participant, admins | conversationId, turnId, startedAt |
| `AIMessageCreated` | participant, assigned staff if any, admins | standard message DTO with senderType `AI` |
| `AIActionProposed` | participant, admins | proposal ID, safe effect, expiry, confirm/cancel flags |
| `AIActionStateChanged` | participant, assigned staff, admins | proposal ID, terminal status, safe outcome |
| `AIVerificationStateChanged` | participant, admins | generic state, prompt text/keys, remaining attempts; never candidate or expected answer |
| `AIHandoffChanged` | participant, queue/staff, admins | mode, safe reason, queue/owner, next availability |
| `AIInactivityWarning` | participant, admins | warningAt, autoCloseAt, cancelAllowed |
| `AIConversationClosed` | participant, admins | resolution code, closedAt, canStartNew |
| `AIPolicyChanged` | admins | published version, enabled state, actor, time |

No event may contain system instructions, raw provider output, raw verification answers, hidden candidate data, passwords, tokens, or disallowed context.
