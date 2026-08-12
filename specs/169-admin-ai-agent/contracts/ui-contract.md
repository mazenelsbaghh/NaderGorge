# Admin AI Agent UI Contract

**Route**: /admin/ai-agent
**Audience**: current built-in Admin only
**Direction**: Arabic-first RTL with correct mixed LTR fragments
**Product distinction**: private Admin agent, not human internal chat and not student live-support AI

## Navigation and shell

- Add a standalone primary Admin navigation item named “وكيل الإدارة AI” directly after the Admin home.
- It is visually separate from communication/support:
  - /admin/chat
  - /admin/live-support
  - /admin/live-support/ai
  - /admin/ai-monitor
- Canonical navigation policy in frontend/src/packages/admin/navigation.tsx declares adminOnly: true.
- frontend route policy is UX only; backend remains authoritative.
- Direct URL by any non-Admin uses the existing unauthorized behavior and never flashes protected content.
- Reuse frontend/src/app/admin/layout.tsx, AdminGuard, PermissionGuard, StaffRealtimeBoundary, AdminShellChrome, and AdminPage.
- Do not create another application shell.

Current navigation duplication must be reconciled:

- AdminShellChrome route union/items/title/defaults;
- packages/admin/navigation.tsx canonical policy;
- route-permissions.ts;
- AdminRootPageClient home shortcuts;
- check-route-permission-contracts.mjs;
- route-permission-parity.spec.ts.

If standalone shell items are not currently supported, extend the shell contract and its check rather than hiding the agent inside a misleading one-item support group.

Mark /admin/ai-agent as an expensive route in IntentLink/selective-prefetch rules so the persistent shell does not eagerly load the heavy workspace.

## Planned component boundaries

Route wrappers:

- frontend/src/app/admin/ai-agent/page.tsx
- frontend/src/app/admin/ai-agent/AdminAiAgentPageClient.tsx

Feature module:

- frontend/src/features/admin-ai-agent/AdminAiAgentWorkspace.tsx
- useAdminAiAgentController.ts
- admin-ai-agent-store.ts
- AdminAiConversationList.tsx
- AdminAiConversationHeader.tsx
- AdminAiTranscript.tsx
- AdminAiMessage.tsx
- AdminAiEvidenceDisclosure.tsx
- AdminAiTurnStatus.tsx
- AdminAiActionProposalCard.tsx
- AdminAiStrongConfirmation.tsx
- AdminAiSecureInputOverlay.tsx
- AdminAiExecutionResult.tsx
- AdminAiComposer.tsx
- AdminAiEmptyState.tsx
- AdminAiErrorState.tsx
- AdminAiSkeleton.tsx

Contracts/integration:

- frontend/src/services/admin-ai-agent-contract.ts
- frontend/src/services/admin-ai-agent-service.ts
- frontend/src/hooks/useAdminAiAgentEvents.ts
- frontend/src/lib/admin-ai-agent-client-contract.ts
- controller/store-owned conversation and snapshot state, plus the allowlisted `ADMIN_AI_REFRESH_SCOPE_KEYS` mapping in frontend/src/lib/query-contracts.ts

Do not reuse chat/live-support stores, message DTOs, participant pending-action cards, hubs, room IDs, typing/read-receipt behavior, policy editor, or verification state.

## Information architecture

Desktop at least 1024px:

    Admin Shell
    ┌──────────────────────────────────────────────────────────┐
    │ Conversation history 280–320px │ Transcript + evidence  │
    │ Search / New                    │ Header / connection     │
    │ Active / archived list          │ Messages / proposals    │
    │                                 │ Sticky composer          │
    └──────────────────────────────────────────────────────────┘

- Evidence and proposal details are inline/collapsible in the transcript.
- No permanent third inspector pane.
- Transcript is the one inner scroll region; page/shell should not form nested competing scrolls.

Tablet 768–1023px:

- Conversation list becomes a drawer or list-to-conversation drill-in.
- Header includes an accessible “المحادثات” control and back behavior.
- Proposal/evidence cards use the full available pane.

Mobile 375px:

- One view at a time: list or conversation.
- Composer is sticky inside workspace above shell bottom navigation and safe-area inset.
- Last message remains visible above composer.
- No fixed 75vh shortcut, document horizontal scroll, or squeezed desktop columns.
- Definition lists/cards replace wide comparison tables; genuinely tabular data gets a focusable local scroll with guidance.

## State ownership

### Backend/PostgreSQL authoritative

- conversations/titles/archive/version;
- messages/order/pagination;
- turn status/failure/progress;
- evidence;
- proposal/current/requested/risk/phrase/expiry;
- secure-grant status;
- execution/result/items/refresh scopes;
- realtime sequence.

### Authoritative client state

- owner conversation pages and the selected snapshot are controller/store owned;
- proposal detail is reconciled through the selected snapshot and content-free realtime events;
- baseline metadata displayed by the workspace comes from the authoritative conversation snapshot;
- cross-surface refresh is restricted to `ADMIN_AI_REFRESH_SCOPE_KEYS`.

The controller clears its authenticated state on sign-out, role loss, or security-version change. No standalone active-baseline client request/cache exists in the current implementation.

### Feature store, memory only

- selected conversation ID;
- list/conversation responsive view;
- connection/reconciliation state;
- last applied sequence/event dedupe;
- in-flight send/confirm/cancel intent IDs;
- in-memory drafts.

No transcript, proposal, phrase, secret, or secure token in localStorage. Drafts should remain memory-only; if future persistence is approved, it must be admin/conversation-scoped and reject protected content.

### Component local

- list search text;
- expanded evidence sections;
- current typed strong phrase;
- secure overlay transient input;
- message autoscroll-near-bottom state.

Secure and phrase values clear on terminal state, close, role change, and unmount.

## Controller behavior

useAdminAiAgentController owns:

- bootstrap/list/select/snapshot reconciliation;
- create/rename/archive/restore;
- send/stop/retry;
- confirm/cancel proposal;
- secure continuation;
- AbortController/generation guards;
- stable idempotency keys across compatible retries;
- realtime event validation/dedupe/gap handling;
- role/security-version cleanup;
- returned refresh-scope invalidation.

Rules:

- Every service method accepts AbortSignal.
- Generate one Idempotency-Key per user intent and retain it until authoritative terminal/conflict.
- Disable the specific in-flight control immediately; do not apply a global busy state to unrelated reads/proposals.
- Ignore late response from an older selected conversation/request generation.
- REST snapshot wins over optimistic/realtime state.
- Do not show action success until execution row is authoritative.

## Conversation list states

- Loading skeleton with reserved layout.
- Empty: “ابدأ أول محادثة مع وكيل الإدارة” plus a clear new-conversation action.
- Loaded active list sorted by last activity.
- Archived filter/list and restore action.
- Search with zero-result state distinct from no conversations.
- Error with inline retry.
- Reconnecting/offline badge; existing data read-only, no send/confirm.
- Role revoked/feature disabled clears protected content.
- Pagination cursor and stable scroll.

Conversation actions:

- New conversation.
- Rename with version/idempotency.
- Archive with explicit warning that pending non-executing work is cancelled.
- Restore.
- No hard delete.

## Transcript and message contract

- Named region with role=log, aria-live=polite, aria-relevant="additions text".
- Do not announce streaming token fragments. Announce terminal message once.
- Admin/model visible text uses dir=auto inside outer RTL.
- IDs, hashes, phone fragments, ISO dates, codes, and money use bdi dir=ltr and tabular numbers.
- Long words/UUIDs use overflow-wrap:anywhere.
- Markdown is either not used or rendered through a strict allowlist with HTML disabled.
- Model URLs are never clickable. Deep links are built from server routeKey mapping.
- Older messages paginate while preserving scroll anchor.
- Autoscroll only if reader is near bottom; otherwise show “رسائل جديدة”.
- Conversation ownership boundary is not exposed through client-side filtering.

## Grounded answer contract

The answer visibly separates when present:

- “النتيجة” / summary;
- facts;
- calculations;
- inferences;
- limitations/partial/unavailable;
- suggested next actions.

Evidence disclosure shows:

- capability label/version;
- scope;
- filters;
- result count;
- complete/truncated;
- dataAsOf rendered in existing Cairo convention;
- allowlisted drill-down links.

Empty, partial, stale, unavailable, and truncated are explicit. The product label states that answers are based on platform data and may require review; it does not present the agent as infallible.

## Turn states and copy intent

| Backend state | UI treatment |
|---|---|
| Queued | “تم استلام سؤالك” and stop control |
| Planning | “بيحدد البيانات المطلوبة…” |
| Retrieving | “بيقرأ بيانات المنصة…” plus bounded activity, no fake percentage |
| Answering | “بيجهز الإجابة…” |
| WaitingClarification | Focused clarification card/options; composer remains available |
| ProposalReady | One/more proposal cards; no business-success copy |
| Completed | Answer/evidence terminal |
| CancelRequested | “جاري إيقاف الطلب…” |
| Cancelled | Inline durable cancelled state and retry-as-new option |
| Failed | Safe code-specific inline failure/retry guidance |
| AccessRevoked | Clear protected state and redirect after assertive notice |

Also distinguish:

- rate limited with retry-after;
- provider timeout/failure;
- dependency/queue failure;
- invalid/unsafe decision refusal;
- empty/partial/truncated result.

## Composer contract

- Autosize with bounded maximum height.
- Enter sends, Shift+Enter adds newline.
- IME composition events never send.
- Send button has a clear Arabic accessible name.
- Stop button replaces/appears beside send only for the active turn.
- Escape does not cancel a proposal/action.
- Draft remains during recoverable send failure and clears only after durable admission.
- Composer regains focus after send unless a secure overlay or explicit keyboard navigation requires otherwise.
- It never asks the Admin to paste passwords/tokens/codes/private files into chat.

## Proposal card contract

Structured card displays:

- exact action and safe capability key;
- safe target and original-screen deep link;
- current to requested changes;
- effect/consequence;
- risk text/icon;
- affected count;
- EGP amount/currency when applicable;
- validation summary;
- expiry;
- secure-input status;
- bulk selection/count/exclusions/sample/Atomic or Partial behavior;
- terminal execution outcome.

Expected failures and expiry remain in the card after refresh. Toast may supplement but never replace durable state.

### Ordinary confirmation

- Concrete CTA such as “تأكيد تحديث بيانات الطالب”.
- Separate cancel action.
- Confirmation button disabled only for this proposal while in flight.

### Strong confirmation

- Explain why stronger confirmation is required.
- Display the exact server phrase in a selectable, direction-safe block.
- Input is in the same proposal card with visible label/instructions.
- Local exact comparison may enable CTA; server remains authority.
- Paste is allowed because the user still deliberately reviews/types/copies a proposal-specific challenge.
- Wrong/expired/stale/locked outcomes are inline and create zero effects.
- Confirmation focus moves to status/result then back to composer; do not unexpectedly jump a reader viewing older messages.

### Multiple proposals

- Independent cards and confirmations.
- No “confirm all” unless a single original authoritative bulk operation produced one card.

## Secure input overlay

- Built on existing accessible Admin modal/overlay pattern.
- Title names the original protected operation, not “AI input.”
- Body states the value will not enter chat or AI context.
- Correct autocomplete/input type and original validation.
- File flow uses existing private upload restrictions, progress, cancel, type/size/malware errors.
- Overlay traps focus, labels controls, closes safely, and restores trigger.
- Raw value is component state only and clears immediately after submit/close/unmount.
- If safe parity cannot be achieved inside chat, deep-link to the original secure screen and resume proposal from authoritative status.

## Execution result

Render distinct:

- full success;
- partial success with succeeded/skipped/failed counts and safe item details;
- validation rejected;
- stale rejected;
- authorization rejected;
- cancelled/expired/invalidated;
- dependency failed;
- recovery required;
- unknown safe failure.

RecoveryRequired never uses success or final-failure styling. It explains that the platform is reconciling the original operation.

After terminal success/partial:

- invalidate only server-returned allowlisted refresh scopes;
- keep the recorded result in transcript;
- provide original-screen link;
- do not rerun the action on refresh.

## Realtime behavior

Use the shared PlatformHub connection through useAdminAiAgentEvents.

- Validate closed event envelope before use.
- Deduplicate eventId.
- Apply only next sequence; gap/reconnect/tab resume triggers snapshot.
- Never append message/proposal content from event payload.
- On role/security-version change: abort requests, clear cache/store/drafts/phrases, stop handlers, and navigate unauthorized.
- Do not create a chat/live-support group or connection.

## Visual system

Sources:

- PRODUCT.md
- DESIGN.md
- frontend/src/app/globals.css
- existing Admin theme/components

Rules:

- Tajawal Arabic and current Montserrat mixed-Latin behavior.
- Deep navy #0A1D3D for authority/headings/primary action.
- Teal #0E8F8F for progress/interaction/focus.
- Gold #D4A017 only as sparse achievement/emphasis, not AI gradient.
- Off-white/soft-gray canvas and white cards via current admin tokens.
- Use var(--admin-*) rather than hard-coded feature colors.
- Moderate 12–16px radius, quiet borders, limited shadow.
- No purple/blue AI gradients, glassmorphism, glowing bot orb, decorative chat bubbles, generic SaaS card grid, or excessive rounded pills.
- Motion 150–250ms only for meaningful state/reveal; transform/opacity; honor reduced motion.
- Loading reserves layout and avoids shifts.

## Accessibility

- Named regions: conversation history, transcript, composer.
- Turn stage live region is separate from transcript and announces transitions, not every token.
- Proposal is a titled section/region; effect/consequence/expiry connected with aria-describedby.
- Icon plus text; never risk/status by color alone.
- Visible focus for every control.
- Minimum 44px touch target.
- Keyboard can create/select/send/stop/open evidence/confirm/cancel/close secure flow.
- Overlay focus trap and restoration.
- After send: composer focus.
- After confirmation: proposal status/result receives programmatic focus only when user initiated it, then composer is reachable.
- Access revocation: one assertive notice then safe redirect.
- WCAG 2.1 AA contrast in light/dark.
- 200% zoom and long Arabic/English/UUID/currency content without loss.

## Error policy

- Expected errors are inline beside their resource.
- Global feature bootstrap failure uses AdminAiErrorState with retry.
- Avoid duplicate global api-client toast plus inline message; AdminAI service/controller owns known error presentation.
- Unknown errors show safe Arabic text and trace ID only.
- Preserve draft/proposal/result where compatible.
- Retry reuses idempotency only for the same intent and never auto-confirms.

## Responsive acceptance matrix

Test 375, 768, 1024, and 1440 pixels with:

- light/dark;
- reduced motion;
- 200% zoom;
- keyboard only;
- screen-reader status;
- long Arabic paragraph;
- English identifier and UUID;
- EGP amounts/dates;
- large evidence/bulk result;
- mobile bottom navigation and safe area;
- no document horizontal scroll;
- no composer overlap;
- one stable transcript scroll.

## Frontend test obligations

### Contract/unit

- closed DTO/enum/error/realtime validation and unknown rejection;
- service AbortSignal and Idempotency-Key behavior;
- store event dedupe/gap/reconnect/generation guards;
- routeKey allowlist and unsafe model link rejection;
- mixed-direction formatter;
- strong phrase local behavior without treating it as authority;
- role/security cleanup and no storage persistence.

### Browser/E2E

Create frontend/tests/e2e/admin-ai-agent.spec.ts covering:

- Admin nav/direct route and every non-Admin denial/no flash;
- conversation create/list/rename/archive/restore/pagination;
- record/aggregate/cross-domain/empty/ambiguous/truncated answers/evidence;
- send/stop/retry/provider/rate/dependency states;
- ordinary and strong proposal/cancel/expiry/stale/partial/recovery;
- wrong/old phrase and double-click/idempotency;
- secure continuation and no secret in DOM/storage/network capture beyond secure request;
- reconnect/gap/duplicate/out-of-order events;
- role revocation during turn/confirm;
- owner/non-owner transcript;
- prompt-injected text and deep-link allowlist;
- accessibility/responsive matrix on Chromium and WebKit.

Update:

- route-permission-parity.spec.ts;
- persistent-shell-navigation.spec.ts;
- platform-accessibility.spec.ts;
- resilient-ui-states.spec.ts;
- selective-prefetch.spec.ts;
- frontend/scripts/check-accessibility.mjs;
- frontend/scripts/check-route-permission-contracts.mjs.

axe critical WCAG A/AA/2.1AA findings are release-blocking for this route.
