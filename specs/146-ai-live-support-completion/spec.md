# Feature Specification: AI Live Support Production Completion

**Feature Branch**: `146-ai-live-support-completion`  
**Created**: 2026-06-24  
**Status**: Draft  
**Input**: User-approved unified review and implementation brief covering the complete student, guest, AI, support-staff, and administration experience while preserving all existing production data and proving the configured AI provider through a real end-to-end test.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Receive safe, continuous AI assistance (Priority: P1)

An authenticated student or outside visitor can open live support, continue an existing eligible conversation, exchange messages with the AI assistant in real time, and always understand whether the assistant is thinking, waiting for input, unavailable, recovering, or transferring the conversation. A visitor can receive general assistance without exposing any student record.

**Why this priority**: The participant conversation is the product's primary entry point. If identity boundaries, delivery, recovery, or basic AI responses fail, no later workflow is usable or safe.

**Independent Test**: Start separate student and guest conversations, send messages through the configured AI provider, disconnect and reconnect both sessions, and verify durable ordered history, explicit states, privacy boundaries, and a real provider response.

**Acceptance Scenarios**:

1. **Given** an authenticated student with no open conversation, **When** the student starts live support and sends a supported question, **Then** one conversation is created and a real AI response appears with its correct sender and durable ordering.
2. **Given** an outside visitor, **When** the visitor provides the required intake details and asks a general question, **Then** the AI responds without automatically linking or disclosing any matching student account.
3. **Given** an acknowledged message followed by a connection interruption, **When** the participant reconnects, **Then** the same conversation and message appear exactly once and the current AI or human mode is restored.
4. **Given** the AI provider or processing path is temporarily unavailable, **When** a participant sends a message, **Then** the interface shows a safe recoverable state, retains the participant's message, and never fabricates a successful response.
5. **Given** a closed conversation, **When** the participant opens its history, **Then** it is read-only and offers an explicit path to start a distinct follow-up conversation.

---

### User Story 2 - Complete verified actions and guest identity workflows (Priority: P1)

The AI can use only the data and capabilities allowed by the active published policy. It can propose a supported student action, account verification, account creation, resolution, or human handoff. No state-changing action runs until the intended participant explicitly confirms it, except a forced safety handoff. Sensitive form values never enter the transcript or AI context.

**Why this priority**: AI-driven state changes and identity linking create the highest privacy and data-integrity risk in the system.

**Independent Test**: Publish a policy allowing representative read and write capabilities, execute confirmed and rejected actions, complete and fail guest verification, create a guest account, and inspect the transcript, audit evidence, and resulting student state.

**Acceptance Scenarios**:

1. **Given** an enabled action and an eligible authenticated student, **When** the AI proposes the action, **Then** the participant sees an accessible confirmation card identifying the target, effect, and expiration while no change has yet occurred.
2. **Given** a valid pending action, **When** the participant confirms it repeatedly, **Then** exactly one authorized business change occurs and one traceable outcome is presented.
3. **Given** an action that expires or is disabled after proposal, **When** the participant confirms it, **Then** execution is rejected without partial change and the card displays a specific terminal state.
4. **Given** a guest claiming an existing account, **When** lookup and challenges run, **Then** the flow reveals no account-existence hint and links only after all configured verification rules pass.
5. **Given** a guest exceeding the permitted verification attempts, **When** the final failure occurs, **Then** verification is locked and the conversation is safely handed to human support.
6. **Given** a guest submits the secure registration form, **When** valid data is accepted, **Then** the account and profile are created once, the conversation is linked, and secrets are absent from messages, AI requests, logs, and audit metadata.
7. **Given** the participant cancels a proposed action, registration, or normal handoff, **When** cancellation completes, **Then** no prohibited change occurs and the AI conversation can continue from a clear state.

---

### User Story 3 - Reach and work with human support reliably (Priority: P1)

When requested or required, a conversation transfers from AI assistance to the human-support workflow without losing its transcript or participant identity boundaries. Eligible staff receive conversations fairly within their configured capacity, can communicate and resolve the issue, and cannot take or mutate conversations they do not own.

**Why this priority**: A trustworthy human fallback is required whenever the AI cannot safely complete the participant's request.

**Independent Test**: Confirm a normal handoff, trigger a forced failure handoff, queue multiple conversations, assign them across eligible staff, transfer and close one conversation, and verify ownership, transcript continuity, capacity, and participant status.

**Acceptance Scenarios**:

1. **Given** a normal AI handoff proposal, **When** the participant confirms it, **Then** AI replies stop and the conversation becomes queued or assigned according to current staff availability.
2. **Given** a critical provider failure or verification lockout, **When** forced handoff starts, **Then** the conversation enters human support without requiring a confirmation that could leave the participant stuck.
3. **Given** multiple eligible staff members, **When** conversations arrive concurrently, **Then** each conversation has at most one owner, no staff capacity is exceeded, and waiting conversations retain fair order.
4. **Given** a staff connection interruption within the recovery period, **When** the staff member reconnects, **Then** ownership and drafts remain coherent; after the period expires, redistribution preserves all messages and events.
5. **Given** a staff member who is neither the owner nor an authorized administrator, **When** that user attempts to send, transfer, close, or execute a student action, **Then** the operation is denied without leaking private conversation data.
6. **Given** a human conversation is closed, **When** capacity becomes available, **Then** the oldest eligible waiting conversation is admitted and the completed conversation becomes immutable apart from its allowed rating.

---

### User Story 4 - Resolve student needs from the staff workspace (Priority: P2)

An authorized support employee can inspect the linked student's permitted context and run the current supported administration actions without leaving the conversation. Unlinked guests expose no student data. Every sensitive operation is validated, confirmed, idempotent, refreshed, and auditable.

**Why this priority**: The staff experience determines whether escalated conversations can be resolved rather than merely discussed.

**Independent Test**: Open linked and unlinked conversations, load each student-context section, run representative actions from all supported categories, correct a guest link, and verify authorization, confirmation, refresh, error handling, and audit evidence.

**Acceptance Scenarios**:

1. **Given** an owned conversation linked to a student, **When** staff opens the workspace, **Then** all permitted context sections load progressively with explicit loading, empty, error, and retry states.
2. **Given** an unlinked guest conversation, **When** staff opens student tools, **Then** private student context and actions remain unavailable until an explicit verified link is created.
3. **Given** a sensitive staff action, **When** staff confirms a valid request, **Then** it executes once, refreshes affected context, and records safe before-and-after evidence.
4. **Given** a validation, authorization, concurrency, or downstream failure, **When** an action ends, **Then** no partial success is shown and the staff member receives a specific recoverable or terminal result.
5. **Given** an incorrect guest-to-student link, **When** authorized staff replaces or removes it with confirmation, **Then** the prior student's active data disappears immediately and immutable link history remains available to administrators.

---

### User Story 5 - Configure and supervise the complete service (Priority: P2)

An administrator can safely enable or disable AI assistance, publish versioned policies and knowledge, configure permitted data, actions, verification, and staff routing, preview AI behavior without business writes, monitor current operations, inspect historical evidence, and intervene in human conversations.

**Why this priority**: Production operation requires control, observability, rapid disablement, and complete investigation without direct database access.

**Independent Test**: Configure staff and AI policy, publish a new version, run a zero-write preview, inspect live and historical conversations and AI decisions, disable AI during activity, and verify permissions, redaction, version conflicts, and reconciliation.

**Acceptance Scenarios**:

1. **Given** an administrator editing a draft policy, **When** publication succeeds, **Then** one immutable active version becomes authoritative and ongoing operations observe a consistent version.
2. **Given** conflicting policy edits, **When** a stale draft is submitted, **Then** the administrator receives a conflict state and no valid newer version is overwritten.
3. **Given** an AI preview request, **When** the preview completes, **Then** the administrator sees the proposed decision and evidence while no participant message, action, verification, account, or conversation state is changed.
4. **Given** an emergency disable request, **When** it is confirmed, **Then** new AI work stops promptly, in-flight work is reconciled safely, and participants receive a defined human-support or unavailable state.
5. **Given** an administrator investigating a conversation, **When** its timeline is opened, **Then** participant, AI, queue, assignment, message, action, verification, handoff, failure, intervention, closure, and rating events can be reconstructed chronologically with protected values redacted.
6. **Given** a non-administrator, **When** that user attempts to access AI policy, knowledge, preview, global statistics, or intervention controls, **Then** access is denied consistently in navigation, pages, and server operations.

---

### User Story 6 - Operate accessibly across supported devices (Priority: P2)

Participants can use the complete chat on supported mobile screens, while staff and administrators can operate their workspaces on supported tablet and desktop screens. All critical interactions are keyboard-accessible, readable, stable, and explicit about async state.

**Why this priority**: A technically complete workflow is still unusable if controls are hidden, overlapping, inaccessible, or ambiguous during long-running operations.

**Independent Test**: Complete participant, staff, and admin smoke journeys at their minimum supported widths using keyboard-only navigation and assistive status announcements, while simulating long content, empty data, slow requests, failures, and reconnects.

**Acceptance Scenarios**:

1. **Given** a 320-pixel-wide participant viewport, **When** the widget opens and interactive cards appear, **Then** every control remains reachable without horizontal scrolling or covering persistent navigation.
2. **Given** a supported tablet or desktop staff viewport, **When** queue, conversation, and student context are used, **Then** the current task remains visually dominant and panels do not overlap or lose essential actions.
3. **Given** keyboard-only operation, **When** dialogs, cards, tabs, composer controls, and error recovery are used, **Then** focus order, visible focus, labels, and focus restoration are correct.
4. **Given** loading, empty, reconnecting, pending, failed, expired, cancelled, closed, or disabled states, **When** the state changes, **Then** users receive clear text and semantic announcements without relying on color alone.
5. **Given** long Arabic or mixed-direction content, **When** it is rendered in messages, cards, timelines, or policy editors, **Then** content wraps predictably without layout shift or clipped actions.

---

### User Story 7 - Preserve production data and recover safely (Priority: P1)

Existing conversations, messages, policies, actions, verification state, links, assignments, ratings, and audit evidence remain intact through deployment. Retries, restarts, stale work, and concurrent commands converge on one durable, explainable state.

**Why this priority**: Completion work cannot be released if it risks deleting historical support evidence or duplicating sensitive effects.

**Independent Test**: Seed representative existing records, apply the upgrade, restart each service during active scenarios, replay callbacks and confirmations, and verify unchanged history, safe recovery, single effects, and consistent terminal states.

**Acceptance Scenarios**:

1. **Given** existing production-shaped support records, **When** the upgrade is applied, **Then** all prior records remain readable and semantically unchanged.
2. **Given** duplicate participant messages, callbacks, action confirmations, or registration submissions, **When** they are replayed, **Then** each logical operation produces at most one durable effect and compatible retries return the original result.
3. **Given** the same retry key with a different request body, **When** it is submitted, **Then** the request is rejected as a conflict and neither payload is silently substituted.
4. **Given** stale queued or processing AI work after restart, **When** recovery runs, **Then** each item is retried, failed, cancelled, or handed off according to one observable rule and no conversation remains indefinitely ambiguous.
5. **Given** simultaneous close, transfer, disable, action, and callback events, **When** they race, **Then** terminal-state and ownership invariants hold and every rejected loser has a traceable reason.

### Edge Cases

- The participant sends text while an action, verification, registration, resolution, or handoff decision awaits confirmation.
- AI is disabled after work is queued, after provider processing starts, or immediately before callback persistence.
- A published policy removes access to data or an action while a proposal or verification is pending.
- Provider output is malformed, unsupported, oversized, delayed beyond its deadline, or contains unsafe content.
- Redis, the AI worker, the application service, or the real-time connection fails independently and later recovers.
- The participant closes or abandons a conversation while AI work or a human action is processing.
- Staff attendance, presence, permission, or capacity changes during assignment or transfer.
- Two administrators publish policy changes or intervene in the same conversation concurrently.
- A guest clears local session state, changes device, supplies another student's phone, or submits inconsistent verification answers.
- Registration collides with an existing unique identity or succeeds while conversation linking fails.
- A transcript, queue, audit timeline, or student history is too large for a single response.
- Arabic, English, emoji, files, long unbroken text, and right-to-left/left-to-right combinations appear in the same conversation.
- The configured AI credentials are missing, invalid, quota-limited, or point to an unavailable model during final acceptance.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: As an authenticated student, open the participant chat on mobile, obtain a real AI reply, confirm one allowed action, reject another, reconnect, request a human, finish the conversation, and submit a rating.
- **Manual QA Role/Flow 2**: As a guest, obtain general help, verify an existing account through challenges, separately create a new account through the secure form, and confirm no secret or identity hint appears in transcript or logs.
- **Manual QA Role/Flow 3**: As support staff, become eligible, receive a queued handoff, use linked-student context and representative actions, transfer and close the conversation, and verify ownership restrictions in another staff session.
- **Manual QA Role/Flow 4**: As administrator, publish AI policy and knowledge, run a zero-write preview, inspect statistics and evidence, disable and re-enable AI, intervene in a conversation, and review the complete timeline.
- **Manual QA Negative Check**: Verify guests cannot discover accounts, AI cannot use disabled data/actions, staff cannot mutate unowned conversations, non-admins cannot access global AI controls, expired confirmations cannot execute, and no password, token, secret, or protected answer enters provider context or observable logs.
- **Docker Acceptance**: Apply the upgrade to a database containing existing support data; start the complete application, worker, data, queue, and real-time stack; verify health/readiness; execute participant-to-AI-to-human and admin journeys; restart services mid-flow; and confirm persistence and recovery.
- **External Dependencies**: Full acceptance requires valid configured AI-provider credentials, network access to that provider, available provider quota, representative student/staff/admin accounts, and at least two browser sessions. Missing or invalid provider access blocks completion rather than being replaced by mock-only evidence.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose one coherent live-support lifecycle across participant AI assistance, pending participant decisions, human queueing, human assignment, closure, follow-up, and rating.
- **FR-002**: The system MUST restore the same eligible open conversation and its current mode when the same authenticated student or guest session reconnects.
- **FR-003**: The system MUST durably order messages and lifecycle events and MUST prevent acknowledged items from appearing more than once after retry or reconnect.
- **FR-004**: The system MUST distinguish authenticated students from unverified visitors and MUST NOT infer, reveal, or link a student identity from visitor-supplied contact data alone.
- **FR-005**: The AI assistant MUST operate only when an active published policy permits operation and MUST use a consistent policy version for each decision.
- **FR-006**: The AI assistant MUST be limited to the readable data, knowledge, decision types, and action keys permitted by the policy used for that decision.
- **FR-007**: Unsupported, malformed, unsafe, oversized, or late AI decisions MUST be rejected without applying participant or student state changes.
- **FR-008**: Each participant message accepted for AI processing MUST reach one terminal outcome: replied, awaiting participant decision, resolved, handed off, cancelled, or failed with an observable recovery path.
- **FR-009**: State-changing AI proposals and normal handoffs MUST require explicit participant confirmation before execution.
- **FR-010**: Critical technical failure and verification lockout MUST be able to force human handoff without participant confirmation when waiting for confirmation would strand the participant.
- **FR-011**: Pending actions, handoffs, verifications, registrations, and resolutions MUST expose clear pending, confirmed, cancelled, expired, succeeded, and failed states where applicable.
- **FR-012**: Confirmation MUST revalidate current policy, participant identity, conversation state, authorization, target, arguments, expiration, and business rules at execution time.
- **FR-013**: Compatible retries of messages, callbacks, confirmations, actions, and registration MUST return one logical result; reuse with a different payload MUST be rejected.
- **FR-014**: Student write actions MUST execute atomically or leave the authoritative student state unchanged and MUST never be presented as successful before durable completion.
- **FR-015**: The participant UI MUST refresh or clearly report the resulting state after a successful, rejected, expired, cancelled, or failed proposal.
- **FR-016**: Guest account lookup and verification MUST avoid account-existence disclosure and MUST enforce the active challenge, attempt, expiry, and lockout rules.
- **FR-017**: Guest account creation MUST use the product's authoritative validation and creation rules and MUST create and link at most one account for one accepted submission.
- **FR-018**: Passwords, authentication secrets, private tokens, raw verification answers, and other protected form values MUST NOT be stored in transcripts, supplied to the AI provider, or emitted in logs, metrics, or audit metadata.
- **FR-019**: Human handoff MUST preserve the participant identity boundary, transcript, AI decision context safe for staff, pending-state outcome, and full chronological evidence.
- **FR-020**: Human assignment MUST consider current eligibility and capacity, maintain at most one owner per conversation, avoid over-capacity assignment, and preserve fair waiting order under concurrency.
- **FR-021**: Only the current owner or an explicitly authorized administrator MUST be able to send staff messages or mutate an active human conversation.
- **FR-022**: A temporary staff disconnect MUST preserve ownership for the configured recovery period; expiry MUST redistribute or requeue without losing transcript or events.
- **FR-023**: Closing a human conversation MUST release capacity, admit the next eligible waiting conversation, make the closed conversation read-only, and allow one immutable rating.
- **FR-024**: Staff student context and actions MUST remain unavailable for an unlinked guest and MUST immediately stop showing a previously linked student after unlink or replacement.
- **FR-025**: Staff actions MUST apply the same authorization, validation, confirmation, transaction, idempotency, and audit rules as their authoritative administration workflows.
- **FR-026**: Successful staff actions MUST refresh affected context without requiring navigation away from the conversation.
- **FR-027**: Administrators MUST be able to configure AI availability, versioned instructions, knowledge, readable data, allowed actions, verification rules, confirmation expiry, and human-routing settings.
- **FR-028**: Policy publication MUST produce one immutable active version, detect stale edits, validate referenced catalog keys, and preserve all prior versions for investigation.
- **FR-029**: AI preview MUST evaluate intended behavior without creating messages, actions, verifications, accounts, assignments, or other business changes.
- **FR-030**: Emergency AI disablement MUST stop admission of new AI work promptly and reconcile queued, in-flight, and pending participant states into defined safe outcomes.
- **FR-031**: Administrators MUST be able to monitor current availability, workload, queue, AI activity, outcomes, errors, handoffs, actions, verification, ratings, and performance over selectable periods.
- **FR-032**: Administrators MUST be able to reconstruct a complete chronological conversation timeline and distinguish participant, AI, worker, staff, administrator, recovery, and system events.
- **FR-033**: Audit and operational evidence MUST identify the actor, conversation, target, policy version, decision or action, canonical time, result, safe reason, and redacted before-and-after state where applicable.
- **FR-034**: Non-administrators MUST be denied access to policy, knowledge, preview, global statistics, global evidence, staff configuration, and intervention capabilities at both navigation and operation boundaries.
- **FR-035**: The system MUST expose explicit loading, empty, pending, retrying, reconnecting, failed, expired, cancelled, disabled, handed-off, closed, and read-only states appropriate to each role.
- **FR-036**: Critical participant flows MUST remain operable at 320 pixels wide; staff and administrator workspaces MUST remain operable at their supported tablet and desktop widths without hidden critical actions or horizontal page overflow.
- **FR-037**: Critical workflows MUST support keyboard operation, visible focus, programmatic labels, semantic state announcements, non-color status cues, focus restoration, and readable contrast.
- **FR-038**: Long histories and evidence lists MUST remain usable through bounded progressive loading with stable ordering.
- **FR-039**: Existing conversations, messages, policies, knowledge, turns, pending actions, verifications, assignments, links, ratings, and audit evidence MUST remain intact and readable after deployment.
- **FR-040**: Data changes MUST be additive or safely transformed, reversible through a documented operational procedure where feasible, and MUST NOT require destructive reinitialization.
- **FR-041**: Recovery MUST resolve stale queued or processing AI work, callbacks, pending decisions, expired verification, disconnects, and disablement without leaving indefinite ambiguous states.
- **FR-042**: Concurrent close, transfer, disable, action, callback, publish, and recovery operations MUST preserve terminal-state, ownership, capacity, version, and at-most-once-effect invariants.
- **FR-043**: Operational errors MUST provide stable safe codes and correlation evidence without exposing prompts, personal data, credentials, secrets, or protected provider output.
- **FR-044**: The complete feature MUST have automated coverage for happy paths, permissions, validation, privacy, idempotency, concurrency, recovery, accessibility-critical behavior, and regression of existing live-support behavior.
- **FR-045**: Final acceptance MUST include a real configured AI-provider response through the deployed application path; mock-only provider evidence MUST NOT qualify the feature as complete.
- **FR-046**: Final acceptance MUST include production-like startup, upgrade, health, restart, and end-to-end role journeys for the complete deployed service stack.

### Key Entities

- **Support Conversation**: The durable participant support case, including participant type, lifecycle mode, human ownership, linked student where verified, closure, follow-up, and rating relationships.
- **Support Message and Event**: Ordered immutable communication and lifecycle evidence with actor, canonical time, delivery or processing state, and safe correlation details.
- **AI Policy Version**: An immutable published definition of assistant availability, instructions, readable data, allowed actions, verification, confirmation, and routing rules.
- **AI Knowledge Item**: Versioned administrator-managed support knowledge available only under the active policy and publication state.
- **AI Turn**: One participant request and its durable processing lifecycle, policy reference, decision outcome, provider evidence, retries, and terminal state.
- **AI Pending Decision**: A proposed action, handoff, verification, registration, or resolution awaiting participant or system outcome with expiry and idempotency identity.
- **AI Verification State**: Privacy-protected visitor lookup and challenge progress, attempts, expiry, lockout, and verified linking outcome.
- **Human Assignment and Queue Entry**: Durable ownership, capacity, fairness, transfer, disconnect recovery, and queue-order evidence.
- **Student Link and Action Execution**: Verified conversation-to-student history and idempotent authorized student changes with redacted audit results.
- **Operational Evidence**: Metrics, errors, policy usage, timelines, ratings, recovery decisions, and administrator interventions suitable for monitoring and investigation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every tested student and guest journey reaches one clear conversation state without duplicate acknowledged messages or lost history across reconnect and restart scenarios.
- **SC-002**: At least 95% of normal connected participant messages display either an AI response or an explicit pending-decision state within 10 seconds, excluding documented external-provider delays.
- **SC-003**: A real configured AI-provider request completes through the production-shaped participant path and produces a valid durable response during final acceptance.
- **SC-004**: All tested confirmed sensitive operations create exactly one business effect, while cancelled, expired, disabled, unauthorized, or invalid proposals create zero effects.
- **SC-005**: All privacy-negative tests show zero account-existence hints and zero protected values in transcripts, provider requests, logs, metrics, and audit metadata.
- **SC-006**: All tested normal and forced handoffs preserve the complete allowed transcript and reach a visible queued or assigned human-support state without participant re-entry.
- **SC-007**: Repeated concurrent assignment and transfer tests produce zero double ownership, zero assignments above capacity, and correct oldest-first admission for all tested queue entries.
- **SC-008**: Staff complete representative context and action workflows from every supported student-administration category without leaving the conversation workspace.
- **SC-009**: Administrators can publish, preview, disable, monitor, investigate, and intervene through the product interface, while every tested non-administrator path is denied.
- **SC-010**: Administrators can reconstruct 100% of tested conversation, AI, action, verification, handoff, human-support, intervention, closure, and rating events from chronological evidence.
- **SC-011**: All critical participant tasks complete at 320-pixel width without horizontal page scrolling, and all critical role workflows complete with keyboard-only operation.
- **SC-012**: Existing representative support records remain readable with unchanged identity and history after upgrade and restart verification.
- **SC-013**: All stale-work, callback-retry, service-restart, and emergency-disable scenarios reach a defined terminal or recoverable state within the configured recovery window.
- **SC-014**: The complete automated feature matrix passes with no unresolved critical or high-severity security, correctness, data-loss, accessibility, or workflow defect.

## Assumptions

- Existing Specs 142–145 describe intended baseline behavior and are evidence inputs; this unified specification is authoritative when it explicitly closes a gap or resolves an inconsistency.
- Existing authentication, student administration rules, staff attendance, role permissions, and audit policies remain authoritative and are reused rather than weakened.
- Existing participant, staff, and administrator URLs remain stable unless a compatibility-preserving redirect or replacement is required by the final plan.
- The currently configured AI provider remains the provider for this feature; provider replacement and new external communication channels are outside scope.
- Production data may already contain records from all existing live-support and AI migrations, so deployment cannot depend on clearing or reseeding those records.
- Valid provider credentials, network access, and quota will be available for final acceptance; their absence is a release blocker for the explicitly required real-provider test.
