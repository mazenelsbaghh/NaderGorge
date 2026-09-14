# Realtime Event Contract

**Transport**: Existing PlatformHub at /hubs/platform
**Target**: Existing owner-specific User_{adminUserId} group
**Authority**: REST conversation list/snapshot and PostgreSQL state
**Purpose**: Notify the owner's open clients that authoritative state changed

## Design decisions

- Do not create or reuse an internal-chat/live-support hub.
- Do not join conversation IDs supplied by the browser.
- Do not broadcast AdminAI events to Role_Admin, Role_Staff, Public, or All.
- Outbox TargetUserId is always the conversation owner for private workflow events.
- Event payload is a minimal invalidation/sequence envelope, never a transcript or proposal body.
- Current PostgreSQL Admin access is rechecked by each REST snapshot after an event/reconnect.
- AccessRevoked is safe and causes immediate cache/store cleanup and navigation away.

## Event names

- AdminAIConversationUpdated
- AdminAIMessageAdded
- AdminAITurnUpdated
- AdminAIProposalUpdated
- AdminAIExecutionUpdated
- AdminAIAccessRevoked
- AdminAIFeatureDisabled
- AdminAIBaselineChanged

## Envelope version 1

    {
      "schemaVersion": "1",
      "eventId": "uuid",
      "eventType": "AdminAITurnUpdated",
      "conversationId": "uuid-or-null",
      "sequence": 42,
      "resourceId": "uuid-or-null",
      "resourceVersion": 8,
      "refreshScopes": ["admin-ai:conversation:uuid"],
      "occurredAt": "ISO-8601 instant"
    }

Rules:

- eventId is stable across Outbox retries.
- conversationId is required for conversation/message/turn/proposal/execution updates and null for global feature/baseline/access events.
- sequence is the conversation LastSequence for private events and 0 for global feature/access events.
- resourceId identifies the changed conversation/message/turn/proposal/execution but is not enough to fetch without owner authorization.
- resourceVersion supports stale-event rejection.
- refreshScopes come from a closed server mapping; model/provider cannot add a scope.
- No content, name, phone, email, address, money, current/requested state, phrase, tool arguments/result, secret, failure detail, or arbitrary URL is allowed.

## Outbox behavior

1. Business/AdminAI transaction writes state and a safe OutboxEvent.
2. Existing lease-aware Outbox processor claims it.
3. Dispatcher validates:
   - event type is an allowed AdminAI event;
   - TargetUserId is a valid owner ID;
   - payload exactly matches envelope version 1;
   - eventId is present and stable;
   - conversation/resource IDs and sequence/version are valid;
   - refresh scopes are allowlisted;
   - payload size is bounded.
4. Send to PlatformHub group User_{TargetUserId}.
5. Outbox acknowledgment follows successful dispatch; duplicate event is safe.

Queue event AdminAITurnQueued is intercepted before SignalR and never broadcast.

## Client algorithm

State retained in memory:

- latest authenticated Admin ID/security version;
- selected conversation ID;
- last applied sequence per loaded conversation;
- bounded eventId dedupe set;
- connection state;
- current snapshot request generation/AbortController.

On event:

1. Validate unknown JSON against the closed envelope before use.
2. Reject unknown schema/event/refresh scope/invalid ID/value.
3. If AccessRevoked or FeatureDisabled:
   - abort in-flight requests;
   - clear AdminAI query cache/store/drafts/typed phrase;
   - stop accepting send/confirm;
   - navigate to existing unauthorized/safe Admin page with clear message.
4. If eventId already applied, ignore.
5. If resourceVersion is older/equal to the currently loaded resource, ignore.
6. If sequence is exactly lastSequence + 1, invalidate/refetch the smallest authoritative resource/snapshot.
7. If sequence is equal/older, treat as replay and ignore after dedupe.
8. If sequence has a gap, pause incremental application and fetch the full authoritative snapshot.
9. After reconnect/tab resume/unknown event, fetch snapshot before enabling actions.

The client never appends model text directly from a realtime payload.

## Event-specific refresh

| Event | Required client action |
|---|---|
| AdminAIConversationUpdated | Refetch conversation list and selected snapshot if matching |
| AdminAIMessageAdded | Refetch selected snapshot/messages; preserve scroll anchor |
| AdminAITurnUpdated | Refetch selected snapshot/turn status |
| AdminAIProposalUpdated | Refetch proposal through snapshot or proposal endpoint |
| AdminAIExecutionUpdated | Refetch proposal/execution and invalidate returned business refresh scopes |
| AdminAIAccessRevoked | Clear all AdminAI state and deny feature |
| AdminAIFeatureDisabled | Stop new work, refetch terminal/history read state if still allowed |
| AdminAIBaselineChanged | Refetch safe baseline and snapshot; pending incompatible proposals invalidate |

## Connection lifecycle

- Reuse the shared PlatformHub connection managed by the existing frontend event layer.
- Do not create a second connection only for AdminAI unless future transport evidence proves it necessary.
- On reconnect, do not trust missed events; snapshot reconciliation is mandatory.
- On sign-out/auth boundary/security-version change, unregister handlers and clear all AdminAI state.
- Do not retry confirm/send based solely on reconnect. The original Idempotency-Key is reused only when the user/client retry policy determines the same intent is still pending.

## Focus and announcements

Realtime updates do not directly move focus or emit repeated toasts.

- A separate polite live region announces stage transitions such as “تم تجهيز اقتراح للمراجعة” or “اكتمل التنفيذ”.
- Expected failures remain inline in their turn/proposal.
- A new-message indicator appears when the reader is not near the transcript bottom; automatic scroll occurs only when already near bottom.
- Access revocation uses an assertive accessible message once, clears protected content, then redirects.

## Security tests

- Non-owner/non-Admin cannot receive another Admin's event through group selection.
- No Role_Admin/Role_Staff/broadcast target for private events.
- Payload schema rejects additional properties and arbitrary refresh scopes.
- Secret/PII sentinel cannot enter Outbox payload, SignalR send, browser handler logs, or telemetry.
- Stable eventId across Outbox lease loss/retry.
- Duplicate/out-of-order/gap/reconnect/tab-resume produce one authoritative UI state.
- Role removal/account disable/security-version change disconnects feature state and prevents subsequent snapshot/action calls.
- Unknown event/schema version fails closed and triggers safe snapshot, never code/dynamic URL execution.
