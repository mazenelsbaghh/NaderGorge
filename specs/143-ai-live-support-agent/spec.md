# Feature Specification: AI Live Support Agent

**Feature Branch**: `143-ai-live-support-agent`  
**Created**: 2026-06-21  
**Status**: Draft  
**Input**: User-approved Arabic feature brief for an AI-first live-support agent with admin-selected context and actions, explicit participant confirmation, guest account verification, account creation, and irreversible human handoff.

## Clarifications

### Session 2026-06-21

- Q: هل يعمل AI عندما لا يوجد موظف دعم مسجل حضور؟ → A: نعم؛ يعمل AI طوال اليوم، وعند الحاجة لبشر يخبر المستخدم أن الموظفين غير متاحين حاليًا وسيتواصلون معه، ثم يضع المحادثة في طابور الوردية التالية ويتوقف نهائيًا.
- Q: إلى متى يظل تحقق الزائر صالحًا بعد نجاحه؟ → A: للمحادثة الحالية فقط؛ كل محادثة جديدة تحتاج تحققًا جديدًا ولا ينشأ تسجيل دخول دائم.
- Q: كيف تنتهي المحادثة التي حلها AI؟ → A: يغلقها AI بعد تأكيد المستخدم أن المشكلة حُلّت، أو تلقائيًا بعد مدة خمول يحددها الأدمن مع تنبيه مسبق وإتاحة إلغاء الإغلاق.
- Q: كيف يعثر الزائر على حسابه قبل أسئلة التحقق؟ → A: يختار الأدمن مفاتيح بحث آمنة، ويدخل الزائر القيمة كاملة دون عرض نتائج أو تلميحات.
- Q: من يملك تعديل ونشر إعدادات AI؟ → A: دور Admin الأساسي فقط؛ لا يمكن تفويض النشر لدور مخصص، بينما يرى موظف الدعم سياق AI الآمن للمحادثة المسندة إليه فقط.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Receive immediate AI support (Priority: P1)

A student or guest starts live support and receives a clearly identified AI response based only on published platform knowledge, admin instructions, and the data categories the admin allowed. The conversation remains durable across refreshes and reconnects.

**Why this priority**: Immediate, bounded assistance is the core value; no actions or handoff matter until the AI can hold a reliable support conversation.

**Independent Test**: Enable AI support, publish one knowledge answer, open one student and one guest conversation, exchange messages, reconnect, and verify labeling, permitted context, ordering, and history restoration.

**Acceptance Scenarios**:

1. **Given** AI support is enabled, **When** a participant opens support and sends a question, **Then** the system clearly identifies the responder as AI and returns a durable answer derived only from active instructions, published knowledge, and allowed data.
2. **Given** a conversation has AI messages, **When** the participant reconnects, **Then** the same transcript and AI-active state are restored without duplicate messages.
3. **Given** a requested data category is disabled by the admin, **When** the participant asks about it, **Then** the AI does not read, infer, expose, or claim access to that category.
4. **Given** a user message or knowledge entry attempts to override system boundaries, **When** the AI evaluates it, **Then** configured permissions and privacy rules remain authoritative.
5. **Given** the participant confirms that the issue is resolved, **When** AI records that confirmation, **Then** the conversation closes with an AI-resolved outcome and offers a new-conversation action.
6. **Given** an AI-active conversation reaches the admin-configured inactivity threshold, **When** the warning is delivered and the participant does not cancel before the grace period expires, **Then** the conversation closes with an inactivity outcome.

---

### User Story 2 - Execute an allowed action after confirmation (Priority: P1)

An authenticated or successfully verified participant asks the AI to perform an account action. The AI may propose only an admin-allowed action, explains the target and effect, requests explicit confirmation, and executes the exact proposal once after valid confirmation.

**Why this priority**: The requested business outcome is an agent that can resolve issues, not only answer questions, while preserving account and financial safety.

**Independent Test**: Allow one low-risk and one sensitive student action, deny another, then verify proposal, confirmation, execution, idempotency, revocation, denial, audit, and handoff behavior.

**Acceptance Scenarios**:

1. **Given** an allowed action is requested for the linked account, **When** the AI proposes it, **Then** the participant sees the target, intended change, material consequence, and an explicit confirm or cancel choice.
2. **Given** a pending action has not been confirmed, **When** the conversation continues, **Then** no business change occurs.
3. **Given** the participant confirms the unchanged pending action before expiry, **When** execution succeeds, **Then** exactly one business change occurs and the result is visible in the transcript and audit history.
4. **Given** the action permission, payload, linked account, relevant account state, or conversation mode changed after proposal, **When** confirmation is submitted, **Then** the stale confirmation produces no business change.
5. **Given** the AI lacks permission for a requested action, **When** it evaluates the request, **Then** it explains that human support is required, hands off, and never attempts the action.
6. **Given** a participant cancels a pending action, **When** cancellation is accepted, **Then** the proposal becomes unusable and no business change occurs.

---

### User Story 3 - Hand off permanently to human support (Priority: P1)

A participant can request a person at any time. Unsupported requests, exhausted verification, unsafe requests, or AI/provider failures also hand off. The full context enters the existing human queue and AI permanently stops in that conversation.

**Why this priority**: Human escalation is the safety boundary for unavailable capabilities and AI failures.

**Independent Test**: Trigger handoff by user request, missing permission, verification failure, and provider failure; verify one transition, queue durability, complete staff context, and zero later AI messages even under concurrency.

**Acceptance Scenarios**:

1. **Given** AI is active, **When** the participant asks for a human, **Then** the conversation is queued or assigned once with a visible reason and AI stops permanently.
2. **Given** no employee is currently available, **When** handoff occurs, **Then** the conversation remains durable in the human queue and shows the next support availability without resuming AI.
3. **Given** an AI response races with handoff, **When** handoff becomes authoritative, **Then** no AI message created afterward is delivered or persisted.
4. **Given** staff opens the handed-off conversation, **When** the workspace loads, **Then** staff sees the full transcript, safe AI summary, account-link and verification state, pending or failed actions, and handoff reason.
5. **Given** AI/provider output is unavailable, malformed, timed out, or rejected as unsafe, **When** recovery cannot safely answer, **Then** the participant receives a clear handoff state without losing prior messages.

---

### User Story 4 - Verify a guest or create an account (Priority: P2)

A guest can receive general help, create a new account through the existing validated workflow, or claim an existing account only after answering admin-configured questions derived from that account's stored non-secret data.

**Why this priority**: Guests are a major source of account-access problems, but private data and account actions require proof of ownership.

**Independent Test**: As a guest, ask a general question, create an account, locate an existing account without disclosure, pass configured verification, fail or exhaust verification, and test ambiguous matches.

**Acceptance Scenarios**:

1. **Given** an unverified guest, **When** they ask a general question, **Then** the AI can answer without exposing candidate account data.
2. **Given** a guest wants a new account, **When** required data passes existing validation and the guest confirms the complete proposal, **Then** one account is created even if the request is retried.
3. **Given** a guest claims an existing account, **When** candidate lookup begins, **Then** names, phone numbers, balances, enrollment, and other private candidate data remain undisclosed.
4. **Given** the admin published safe lookup keys, **When** a guest starts account recovery, **Then** the guest supplies a complete value for one allowed key and receives no candidate list, partial match, existence signal, or hint.
5. **Given** exactly one account can be safely challenged, **When** the guest answers the configured non-secret questions correctly, **Then** the current conversation becomes linked to that account and account actions become subject to the AI allowlist and confirmation policy.
6. **Given** answers are wrong, missing, exhausted, stale, or match multiple accounts, **When** verification cannot establish one owner, **Then** no account is linked, no hints or expected answers are exposed, and the conversation hands off.
7. **Given** a configured question references a password, token, secret, empty field, or prohibited category, **When** the admin tries to publish it, **Then** publication is rejected.

---

### User Story 5 - Configure and supervise AI from admin (Priority: P2)

An admin uses a dedicated AI support tab to enable the agent, edit instructions, publish knowledge, select readable data and executable actions, configure guest verification, run a write-free preview, and investigate every AI decision and outcome.

**Why this priority**: The AI must remain governed by explicit product-owner choices and reconstructable evidence.

**Independent Test**: Configure a minimal policy, preview it, publish it, run conversations, change or revoke capabilities, and reconstruct answers, actions, verification, failures, and handoffs from the admin view.

**Acceptance Scenarios**:

1. **Given** an admin edits instructions or knowledge, **When** changes are not published, **Then** production conversations continue using the last published version.
2. **Given** readable data and action catalogs exist, **When** the admin changes selections and publishes, **Then** new AI decisions use that version and prior decisions retain their historical version reference.
3. **Given** preview mode is open, **When** the admin tests prompts and actions, **Then** the UI shows intended context and decisions without sending participant messages, creating accounts, changing business data, or consuming human capacity.
4. **Given** AI activity exists, **When** the admin investigates it, **Then** the admin can distinguish AI-active, AI-resolved, handed-off, failed, verified, account-created, and action-executed outcomes.
5. **Given** an admin disables AI, **When** the change becomes active, **Then** no new AI turn starts; conversations already awaiting AI work safely hand off and all history remains available.

### Edge Cases

- AI is disabled or its permissions are revoked while a response, verification, confirmation, or action is in progress.
- A participant sends duplicate messages, repeats confirmation, refreshes, or uses multiple tabs during the same pending action.
- No human staff member is checked in when AI hands off.
- A guest's answers are formatted differently from stored data, the source data changes mid-verification, or multiple accounts share the same answer set.
- An account is disabled, deleted, merged, or becomes inaccessible during verification or action execution.
- A knowledge entry is edited or withdrawn during generation, or a conversation exceeds the safe context size.
- User content, stored knowledge, or account data contains prompt-injection text or attempts to request hidden configuration.
- AI returns empty, malformed, unsafe, overlong, or contradictory output; the provider times out, rate-limits, or exhausts quota.
- Human handoff and a final AI response race; handoff must win and AI cannot resume.
- Account creation is retried after an uncertain response; at most one account is created.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: As admin on the admin surface, configure instructions, publish knowledge, allow one data category and one action, configure verification, run preview, and verify preview performs no writes.
- **Manual QA Role/Flow 2**: As an authenticated student on the student surface, receive an AI answer, request an allowed action, cancel once, confirm once, then request a human and verify AI stops.
- **Manual QA Role/Flow 3**: As a guest on the public surface, receive general help, create a new account, and separately verify an existing account through configured questions.
- **Manual QA Role/Flow 4**: As support staff, accept a handed-off conversation and verify the transcript, AI summary, verification/link state, actions, and handoff reason.
- **Manual QA Negative Check**: Attempt disabled data access, disabled action execution, stale or duplicate confirmation, excessive verification attempts, candidate disclosure, prompt injection, and AI response after handoff; every attempt must fail closed and remain auditable.
- **Docker Acceptance**: Apply migrations, rebuild backend/worker/public/student/admin/assistant services, verify PostgreSQL/Redis/health endpoints, confirm AI worker health with valid configuration, and run a real queue-to-handoff smoke test.
- **External Dependencies**: A configured supported AI provider is required for full runtime validation; tests must provide deterministic provider doubles. No OTP or WhatsApp provider is required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST extend the existing durable live-support conversation rather than create a separate participant chat history.
- **FR-002**: When enabled, AI MUST be the initial responder for supported student and guest conversations and MUST be visibly identified as automated.
- **FR-003**: AI MUST use only the active published instructions, published knowledge, and admin-allowed platform or participant data categories.
- **FR-004**: The system MUST treat user messages, knowledge content, and retrieved account data as untrusted input that cannot override permissions, privacy rules, or system instructions.
- **FR-005**: AI messages MUST have durable identity, sender type, canonical time, ordering, retry safety, and the same reconnect guarantees as existing live-support messages.
- **FR-006**: Admin MUST have a dedicated AI support tab for enablement, instructions, knowledge, readable-data permissions, action permissions, guest verification policy, preview, monitoring, and audit.
- **FR-007**: Admin-configurable data and action permissions MUST use stable catalog keys and fail closed when a key is unknown, removed, inactive, or no longer compatible.
- **FR-008**: AI MUST NOT expose or infer data from a category that is not active in the published readable-data policy.
- **FR-009**: AI MUST NOT propose or execute an action that is not active in the published action policy.
- **FR-010**: Every action that can change business state MUST require explicit participant confirmation, even when the action is allowlisted.
- **FR-011**: An action confirmation MUST identify the linked account, action, exact intended values, material effect, and expiry, and MUST be bound to the policy version and relevant current state.
- **FR-012**: No business change may occur before confirmation; valid confirmation MUST produce at most one logical business change despite retries or concurrency.
- **FR-013**: Cancellation, expiry, payload change, account-link change, policy revocation, relevant state change, or human handoff MUST invalidate a pending confirmation.
- **FR-014**: AI actions MUST reuse existing business validation, authorization, idempotency, and audit rules rather than bypass them.
- **FR-015**: The AI-selectable action catalog MUST cover supported account and student operations plus validated account creation; global platform settings, role management, destructive audit removal, and operations without a participant-owned target are out of scope for AI execution.
- **FR-016**: A participant MUST be able to request human support at any time using natural language or an explicit control.
- **FR-017**: Missing action permission, verification failure or exhaustion, unsafe request, unrecoverable AI/provider failure, or explicit human request MUST hand off to the existing human routing system.
- **FR-018**: Human handoff MUST atomically make AI permanently inactive for that conversation, preserve full history, record a safe reason, and queue or assign exactly once.
- **FR-019**: No AI response generated or received after authoritative handoff may be delivered or persisted as a conversation message.
- **FR-020**: If no human is available, the handed-off conversation MUST remain queued durably and show the next configured human-support availability.
- **FR-021**: Staff MUST receive the full transcript, safe AI summary, policy version, verification/account-link state, pending or attempted actions, failures, and handoff reason.
- **FR-022**: Authenticated students MAY use allowed context for their authenticated account without guest verification, subject to existing account status and authorization rules.
- **FR-023**: Unverified guests MAY receive general help and start account creation but MUST NOT receive private existing-account data or execute existing-account actions.
- **FR-024**: Account creation MUST reuse the current validated workflow, display the complete proposed participant data, require explicit confirmation, and create at most one account on retry.
- **FR-025**: Existing-account lookup for a guest MUST disclose no candidate identity or private account data before verification succeeds.
- **FR-026**: Admin MUST configure guest verification from an allowlist of non-secret stored fields, safe question text, comparison rules, required correct-answer count, and a maximum-attempt count defaulting to three.
- **FR-027**: Passwords, hashes, tokens, authentication secrets, protected payment data, and fields that are absent, prohibited, or unsuitable for ownership proof MUST NOT be usable as verification questions.
- **FR-028**: Verification MUST provide no correctness hints, expected values, or alternative candidate information.
- **FR-029**: Verification success MUST identify exactly one eligible account before linking; wrong, incomplete, exhausted, ambiguous, or stale verification MUST link nothing and hand off.
- **FR-030**: Raw verification answers MUST NOT be retained beyond what is necessary to compare the current attempt; audit must store only redacted question keys, outcome, policy version, and timing.
- **FR-031**: Successful guest verification MUST authorize only the current conversation; every new conversation requires fresh verification and verification MUST NOT create a reusable authenticated login session.
- **FR-032**: AI MUST remain available when no human staff is checked in; when a human handoff is needed outside staffed hours, the participant MUST be told that staff are currently unavailable and will contact them, and the conversation MUST enter the next-shift queue while AI remains permanently stopped for that conversation.
- **FR-033**: Admin edits to instructions, knowledge, catalogs, and verification policy MUST be draftable, previewable without production writes, explicitly publishable, versioned, and auditable.
- **FR-034**: Publishing MUST validate instructions, knowledge state, catalog references, verification safety, and policy completeness before the version becomes active.
- **FR-035**: Each AI decision MUST reference the published policy and knowledge versions it used so historical behavior remains reconstructable.
- **FR-036**: Disabling AI MUST block new AI work, safely hand off conversations awaiting AI work, preserve all transcripts and settings, and prevent automatic AI resumption.
- **FR-037**: Every AI response, safe context category, permission decision, confirmation, cancellation, action execution, verification attempt/outcome, account creation, provider error, safety rejection, and handoff MUST be chronologically auditable.
- **FR-038**: Audit, logs, model context records, and admin UI MUST redact passwords, raw verification answers, secrets, tokens, protected payment data, and disallowed participant fields.
- **FR-039**: Admin monitoring MUST distinguish AI-active, AI-resolved, human-handoff, failed, verified, account-created, and action-executed conversations and support investigation by conversation, participant, outcome, and time.
- **FR-040**: AI generation and execution retries MUST be idempotent and concurrency-safe across duplicate participant messages, duplicate provider callbacks, multiple browser tabs, and service restarts.
- **FR-041**: The system MUST expose clear loading, AI-thinking, confirmation-required, cancelled, verification, retry, queued, human-assigned, failure, and closed states on supported participant, staff, and admin surfaces.
- **FR-042**: The participant MUST always be able to cancel a pending action or request a human while AI work is pending.
- **FR-043**: Long conversations MUST be safely summarized or progressively contextualized without silently dropping the current request, confirmed identity state, pending action state, or handoff boundary.
- **FR-044**: Viewing, editing, previewing, publishing, enabling, or disabling AI support configuration MUST be restricted to the built-in Admin role and MUST NOT be delegable through custom-role permissions; support staff may view only the safe AI context required for conversations assigned to them.
- **FR-045**: The feature MUST preserve all existing live-support privacy, ownership, capacity, queue, attendance, action, and audit guarantees.
- **FR-046**: AI MUST close a resolved conversation after explicit participant confirmation, recording an AI-resolved outcome and preserving a clear new-conversation action.
- **FR-047**: Admin MUST configure an AI inactivity duration and warning grace period; the participant MUST receive a warning and be able to cancel auto-close before an inactive AI conversation closes.
- **FR-048**: Admin MUST select guest account lookup keys from a safe allowlist; lookup MUST require a complete value and MUST NOT return candidate lists, partial matches, existence signals, or hints to the guest.

### Key Entities

- **AI Support Policy**: Versioned draft or published configuration containing enablement, instructions, readable-data catalog selections, action catalog selections, confirmation rules, and audit ownership.
- **AI Knowledge Entry**: Versioned support knowledge with title, content, source metadata, draft or published state, validity, and author.
- **AI Conversation State**: Per-conversation mode, policy version, AI lifecycle timestamps, inactivity warning and expiry, terminal resolution or handoff reason, and safe summary reference.
- **AI Turn**: One participant-to-AI decision cycle with source message, output message when any, safe context categories, policy/knowledge versions, status, latency, and redacted error.
- **AI Pending Action**: Exact action proposal, linked target, safe payload fingerprint, policy/state fingerprint, confirmation expiry, idempotency identity, and terminal outcome.
- **Guest Verification Policy**: Versioned safe lookup keys, question definitions, allowed source fields, safe comparison rules, correct-answer threshold, maximum attempts, and publication state.
- **Guest Verification Session**: Conversation-scoped candidate and policy reference, attempt count, state, expiry, and link outcome without retained raw answers.
- **AI Decision Audit**: Append-only evidence linking turns, permissions, confirmations, actions, verification, errors, and handoff to the existing conversation and central audit history.
- **Existing Live Support Records**: Reused conversation, message, event, queue, assignment, participant link, action execution, rating, and audit records.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In normal provider conditions, 95% of AI text replies reach the participant within 8 seconds of a valid message.
- **SC-002**: Across permission-negative tests, AI reads zero disabled data categories and executes zero disabled actions.
- **SC-003**: Across confirmation and retry tests, zero state-changing actions occur before valid confirmation and each valid proposal produces at most one business effect.
- **SC-004**: Across explicit, failure, verification, and concurrency handoff tests, exactly one human handoff occurs and zero AI messages are delivered after the authoritative transition.
- **SC-005**: Across guest verification tests, zero private candidate-account fields or expected verification answers are disclosed before successful verification.
- **SC-006**: Staff can open a handed-off conversation and see all required safe context without visiting another page.
- **SC-007**: Admin can reconstruct 100% of sampled AI answers, permission decisions, confirmations, actions, verification outcomes, provider failures, and handoffs from chronological evidence.
- **SC-008**: Preview testing produces zero participant messages, business writes, account creations, human assignments, or queue-capacity consumption.
- **SC-009**: Disabling AI prevents new AI turns within 5 seconds and preserves 100% of existing conversation and audit history.
- **SC-010**: Supported student, guest, staff, and admin flows remain usable at their existing target screen sizes and meet existing critical accessibility requirements.

## Assumptions

- The existing live-support conversation, queue, staff attendance, SignalR, student action catalog, account creation validation, and central audit capabilities remain authoritative.
- AI provider configuration follows the platform's existing provider strategy, but the product behavior is provider-independent.
- Knowledge changes use draft, preview, and explicit publish semantics so an accidental edit cannot immediately affect production answers.
- Verification uses admin-selected stored non-secret fields, normalized comparison, a configurable correct-answer threshold, and a default maximum of three attempts.
- A verification result is bound to the current conversation only and never transfers to another conversation, browser, or participant.
- AI may create or change only participant-owned student/account state represented by the approved catalogs; global operational administration remains human-only.
- The participant-facing experience remains Arabic-first while stored knowledge and answers may support additional languages later.
