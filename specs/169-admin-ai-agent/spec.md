# Feature Specification: Admin AI Agent

**Feature Branch**: `planning-only; no branch created`
**Created**: 2026-08-11
**Status**: Planned for owner review — implementation not authorized
**Input**: Add a private, Arabic-first AI chat inside the Admin Shell for every Admin. It can answer from all platform business data and execute every current Admin operation only after the required confirmation, while never exposing technical secrets or writing raw database commands. Complete specification, clarification, planning, and tasks only; do not implement until the owner reviews and approves the artifacts.

## Confirmed Business Decisions

- The agent is a standalone Admin Shell surface and has no participant, student-support, or live-support conversation relationship.
- Every authenticated user with the built-in `Admin` role can use the agent; every other role is denied.
- The readable business scope includes student, teacher, employee, content, academic, sales, recharge, wallet, financial, HR, community, support, audit, operational, and reporting data at aggregate and record level.
- Passwords and password hashes, access or refresh tokens, encryption keys, service credentials, session material, verification codes, protected verification answers, and equivalent secrets are never readable by the agent.
- The first release covers every Admin operation that exists when this specification baseline is approved.
- The agent never writes arbitrary database commands and never bypasses the authoritative validation, authorization, transaction, concurrency, or audit behavior of the original Admin operation.
- Every state-changing operation requires an exact proposal and explicit confirmation before execution.
- Destructive, financial, permission, security, account-disable, credential, and bulk operations require a typed strong-confirmation phrase in addition to reviewing the proposal.
- Confirmations expire, can be cancelled, become invalid when relevant state changes, and produce at most one logical effect.
- Admin chat history is private to the initiating Admin by default. Redacted action evidence remains available through the administrative audit trail.
- Product implementation is explicitly paused. This run ends after `tasks.md` until the owner gives a new implementation approval.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask questions across the whole platform (Priority: P1)

As an Admin, I need one private conversational workspace where I can ask natural-language questions about any platform business domain and receive a concise, traceable answer from current authoritative data, so I do not need to manually navigate and combine many screens and reports.

**Why this priority**: Reliable read-only answers deliver immediate value and establish the privacy, access, grounding, and evidence foundation required before any action can be proposed.

**Independent Test**: Sign in as an Admin and ask representative record-level, aggregate, and cross-domain questions covering students, teachers, content, sales, platform finance, HR, support, and audit history. Compare every fact and calculation with the authoritative screens or reports, then ask for a prohibited secret and confirm it is unavailable everywhere.

**Acceptance Scenarios**:

1. **Given** an authenticated Admin, **When** the Admin asks a well-scoped question, **Then** the answer uses current committed business data and identifies the applied scope, filters, result count, and data time.
2. **Given** a question that joins several business domains, **When** all required data is available, **Then** the answer reconciles the domains without double counting and exposes a safe drill-down path to supporting records.
3. **Given** an ambiguous name, date range, entity, or metric, **When** materially different answers are possible, **Then** the agent asks for a clarification and does not guess or mutate data.
4. **Given** no matching records, **When** the question is valid, **Then** the agent returns a clear empty result and the filters used rather than inventing an answer.
5. **Given** a request for a prohibited secret, **When** any component processes the request, **Then** the secret is neither retrieved nor sent to the AI provider nor displayed nor persisted, and the Admin receives a safe refusal.
6. **Given** a non-Admin user, **When** the user attempts to open or call the agent, **Then** the navigation entry is absent and every request is denied without revealing data or feature state.

---

### User Story 2 - Review and confirm ordinary Admin operations (Priority: P1)

As an Admin, I need to request an existing administrative change in the conversation, review exactly what will change, and confirm or cancel it, so the agent can save navigation time without silently changing platform state.

**Why this priority**: Confirmed execution is the core differentiator from a reporting assistant and must preserve every business rule of the original Admin workflow.

**Independent Test**: Request representative non-high-risk operations from each current Admin operation family. Verify the proposal, cancel one, expire one, confirm one repeatedly, change the target before confirming another, and compare successful results with executing the same operation in its original Admin screen.

**Acceptance Scenarios**:

1. **Given** a valid state-changing request, **When** the agent understands the target and inputs, **Then** it creates a proposal showing the action, target, current value or state, requested value or state, expected effect, risk, validation summary, and expiry, while producing no business effect.
2. **Given** a pending ordinary proposal, **When** the Admin explicitly confirms it before expiry, **Then** the platform revalidates the actor, target, inputs, and current state and executes the authoritative operation once.
3. **Given** a proposal that is cancelled, expired, invalidated, or never confirmed, **When** it reaches a terminal state, **Then** it creates zero business effects and clearly explains that outcome.
4. **Given** a duplicate confirmation or network retry, **When** the same logical confirmation is processed repeatedly, **Then** the original compatible result is returned and no duplicate effect occurs.
5. **Given** relevant data changed after the proposal, **When** the Admin tries to confirm, **Then** execution is rejected as stale, current data is shown, and a new review is required.
6. **Given** an operation that requires a password, protected answer, token replacement, or equivalent secret input, **When** the Admin proceeds, **Then** the value is collected in the operation's secure input flow outside the conversation and never enters the AI context, transcript, or audit payload.

---

### User Story 3 - Strongly confirm high-risk and bulk operations (Priority: P1)

As an Admin, I need stronger, unmistakable confirmation for financial, destructive, security, permission, account-disable, credential, and bulk operations, so a conversational misunderstanding cannot cause a high-impact change.

**Why this priority**: Broad action coverage is unacceptable unless irreversible or far-reaching operations have a deliberately higher human-control barrier.

**Independent Test**: Propose representative financial, deletion, role/permission, account-disable, credential, and bulk operations. Attempt empty, wrong, expired, stale, repeated, and correct confirmation phrases; verify scope, business effects, and audit evidence.

**Acceptance Scenarios**:

1. **Given** a high-risk request, **When** the agent prepares it, **Then** the proposal identifies the risk category, affected entities and count, money and currency when applicable, irreversible or downstream consequences, and the exact strong-confirmation phrase.
2. **Given** a high-risk proposal, **When** the phrase is absent, approximate, copied from an old proposal, or does not match the current proposal, **Then** execution is denied with zero effects.
3. **Given** a valid high-risk proposal and exact phrase, **When** the Admin confirms, **Then** the platform performs a final state and rule check and either executes once or fails closed with a specific reason.
4. **Given** a bulk operation, **When** it is proposed, **Then** the Admin can inspect the selection rule, total count, representative preview, excluded records, per-item or atomic semantics, and expected partial-failure behavior before confirmation.
5. **Given** a bulk operation whose original Admin workflow supports partial outcomes, **When** execution finishes, **Then** the result separates succeeded, skipped, validation-failed, and system-failed items without silently retrying incompatible failures.

---

### User Story 4 - Cover every current Admin capability safely (Priority: P1)

As the platform owner, I need an auditable capability inventory proving that every current Admin read surface and state-changing operation is represented safely, so "all Admin actions" is measurable rather than an open-ended promise.

**Why this priority**: The requested first-release scope is complete coverage. A release cannot claim completion while actions are missing, mapped to unsafe generic execution, or behaviorally different from the original workflow.

**Independent Test**: Generate the approved baseline inventory of Admin navigation, readable domains, and operations; compare it with the agent capability catalog and contracts; verify every active item has exactly one supported, intentionally excluded, or non-mutating classification and that no state-changing Admin operation remains unmapped.

**Acceptance Scenarios**:

1. **Given** the approved baseline of current Admin capabilities, **When** coverage is evaluated, **Then** every readable domain has a bounded safe projection and every state-changing operation has a named proposal and execution contract.
2. **Given** an Admin operation with secure fields, attachments, concurrency rules, approvals, financial posting, or other special behavior, **When** it is represented by the agent, **Then** that behavior remains enforced rather than flattened into a generic update.
3. **Given** an operation that is removed, renamed, or contractually changed during delivery, **When** the coverage gate runs, **Then** stale capability definitions fail the release until reconciled.
4. **Given** a request for an unknown, future, internal-only, infrastructure, deployment, or otherwise uncatalogued operation, **When** the agent evaluates it, **Then** it refuses and performs no operation.
5. **Given** a new Admin operation added after the approved baseline, **When** it has not yet received a safe capability definition and tests, **Then** the agent cannot execute it even though the original Admin screen remains usable.

---

### User Story 5 - Resume conversations and audit every decision (Priority: P2)

As an Admin or auditor, I need private conversation continuity and a redacted, immutable action trail, so I can resume analysis while investigators can reconstruct what the agent read, proposed, confirmed, executed, rejected, or failed without exposing secrets.

**Why this priority**: Conversation history improves daily usefulness, while durable evidence is required for accountability and incident investigation.

**Independent Test**: Create and resume several Admin conversations, run successful, cancelled, expired, invalid, stale, duplicate, and failed actions, inspect the initiating Admin's history and the shared action audit, and verify retention, ownership, redaction, ordering, and trace correlation.

**Acceptance Scenarios**:

1. **Given** an Admin conversation, **When** its owner returns later, **Then** the owner can list, rename, reopen, and continue it with clear history boundaries and without inheriting another Admin's private transcript.
2. **Given** another Admin, **When** that Admin attempts to open a transcript they do not own, **Then** access is denied even though redacted action evidence remains available through the existing administrative audit permissions.
3. **Given** any action proposal or execution attempt, **When** an auditor follows its trace, **Then** the actor, timestamps, request summary, capability, safe target, confirmation state, result, and redacted before/after evidence can be reconstructed.
4. **Given** an AI-provider, data-source, queue, or execution dependency failure, **When** a turn or action cannot complete, **Then** the conversation shows a safe retry or terminal failure state, no unconfirmed change occurs, and the failure is auditable.
5. **Given** a user cancels a generating answer or pending proposal, **When** cancellation wins the applicable race, **Then** further work stops where safe, no new action executes, and the resulting state remains consistent after refresh.

### Edge Cases

- The Admin role is removed, the account is disabled, the session expires, or the security version changes while an answer or proposal is in progress.
- Two tabs or two Admins act on the same entity while a proposal is pending.
- One prompt contains multiple questions, several independent changes, or a mix of reads and writes.
- A request names an entity ambiguously, uses a duplicate name, supplies a partial phone number, or crosses tenant/teacher/academic ownership boundaries represented by existing rules.
- A result set is empty, too large, changes during pagination, contains deleted records, or includes historical and current rows with different semantics.
- Retrieved business text contains prompt-injection instructions, markup, code, links, secrets, or malicious attachment content.
- The model invents a capability key, target identifier, filter, monetary amount, confirmation phrase, or execution result.
- A proposal expires during confirmation, is disabled administratively, or loses its underlying capability before execution.
- A financial or bulk action partially fails according to its original workflow; the result must not claim full success.
- The AI provider times out, returns malformed output, repeats a callback, becomes unavailable, or completes after the user cancels.
- The platform restarts while a turn, proposal, or execution is pending; durable state must recover to one consistent outcome.
- The same idempotency identity is reused with a different payload; the request must be rejected.
- An operation requires secure file, password, token, or verification input that is forbidden in conversational context.
- An Admin asks the agent to expose, export, delete, or alter its own audit evidence or the protected secret policy.
- An RTL conversation includes English identifiers, numbers, dates, currency, code, tables, long words, or large structured results on a narrow viewport.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: As an Admin, open the standalone Admin AI Agent surface, ask Arabic record, aggregate, and cross-domain questions, inspect scope/evidence, continue a saved conversation, and verify no student/live-support conversation appears.
- **Manual QA Role/Flow 2**: Propose, cancel, expire, stale, and confirm representative ordinary operations from every capability family; compare successful effects with the original Admin workflows.
- **Manual QA Role/Flow 3**: Execute representative financial, destructive, permission, security, credential, account-disable, and bulk operations using strong confirmation; verify wrong phrases and changed state create zero effects.
- **Manual QA Negative Check**: Verify every non-Admin role is denied, secrets never appear, prompt injection cannot change policy, unknown operations fail closed, duplicate confirmation creates one outcome, and another Admin cannot open a private transcript.
- **Manual QA Accessibility/Responsive Check**: Complete read, proposal, strong-confirmation, cancellation, history, and recovery flows by keyboard at 375px, 768px, 1024px, and 1440px, in light and dark themes and reduced-motion mode, with no hidden focus, overflow, or color-only state.
- **Docker Acceptance**: Start the complete application, data, queue, real-time, and configured AI stack; apply additive schema changes to representative existing data; run Admin/non-Admin, read, confirmation, strong-confirmation, concurrency, restart, redaction, audit, and capability-coverage smoke paths; verify health and durable recovery without deleting or reinitializing existing data.
- **External Dependencies**: Full validation requires the configured production-equivalent AI provider, PostgreSQL, Redis/queue delivery, current private attachment storage, and representative seeded records. Missing credentials or services must be reported explicitly and may not be replaced by a claimed production pass.

## Requirements *(mandatory)*

### Functional Requirements

#### Access and surface isolation

- **FR-001**: The platform MUST provide a standalone Admin AI Agent conversation surface inside the Admin Shell.
- **FR-002**: The Admin AI Agent MUST share no conversation, participant, message, routing, assignment, rating, verification, handoff, or policy state with student or live-support chat.
- **FR-003**: Only a currently authenticated user with the built-in `Admin` role MUST be able to discover, open, read, or mutate Admin AI Agent resources.
- **FR-004**: Every conversation, read request, proposal, confirmation, cancellation, execution, history, and streaming/realtime operation MUST revalidate the caller's current Admin access.
- **FR-005**: Role removal, account disablement, session revocation, or security-version change MUST terminate access and invalidate pending proposals for that actor.
- **FR-006**: Every Admin conversation MUST have exactly one owning Admin; other Admins MUST NOT read its transcript by default.
- **FR-007**: The product MUST keep the existing human internal chat and student/live-support AI interfaces visually and behaviorally distinct from the Admin AI Agent.

#### Grounded reads and answers

- **FR-008**: The agent MUST support bounded, read-only access to every current Admin-readable business domain in the approved capability baseline.
- **FR-009**: Read capabilities MUST cover both aggregate analysis and authorized record-level detail.
- **FR-010**: Every answer MUST be grounded only in retrieved authoritative platform data, approved static platform knowledge, and the current conversation; open-web facts MUST NOT be introduced as platform facts.
- **FR-011**: Every data-backed answer MUST identify the applied filters, scope, result count or completeness limit, and the time or version of the data used.
- **FR-012**: Numerical totals, counts, balances, financial amounts, ratios, and reconciliations MUST be computed from deterministic platform results rather than model estimation.
- **FR-013**: The agent MUST request clarification when identity, scope, time range, metric definition, target, or requested effect is materially ambiguous.
- **FR-014**: The agent MUST return an explicit empty, partial, unavailable, or stale state rather than inventing missing information.
- **FR-015**: Large results MUST be bounded, summarized, pageable or exportable through an existing authorized path, and MUST disclose truncation or incomplete coverage.
- **FR-016**: Safe drill-down references MUST allow the Admin to reach the relevant original Admin records or reports without exposing raw internal or protected identifiers unnecessarily.
- **FR-017**: Business data and retrieved content MUST be treated as untrusted input and MUST NOT override system policy, capability definitions, confirmation rules, or output restrictions.
- **FR-018**: The agent MUST distinguish facts, calculations, inferences, limitations, and suggested next actions in its response when that distinction affects Admin decisions.

#### Complete capability inventory

- **FR-019**: Delivery MUST establish a versioned baseline inventory of every current Admin navigation surface, readable business domain, and state-changing operation.
- **FR-020**: Every baseline read domain MUST map to a named, bounded, redacted read capability with defined inputs, outputs, limits, and evidence behavior.
- **FR-021**: Every baseline state-changing Admin operation MUST map to a named action capability with defined targets, inputs, validation, risk, preview, confirmation, execution, result, refresh behavior, and audit evidence.
- **FR-022**: Each action capability MUST preserve the authoritative operation's authorization, validation, transaction, concurrency, approvals, accounting, notification, file, idempotency, and audit semantics.
- **FR-023**: The agent MUST NOT execute an unknown, disabled, incompatible, internal-only, infrastructure, deployment, raw database, generated-code, or otherwise uncatalogued operation.
- **FR-024**: Release acceptance MUST fail if any baseline state-changing Admin operation is missing, duplicated ambiguously, stale, or lacks its required security and behavior tests.
- **FR-025**: Capability changes during delivery MUST be reconciled against the baseline; new post-baseline operations remain unavailable to the agent until explicitly catalogued and tested.
- **FR-026**: Original Admin screens and workflows MUST remain available as the authoritative manual fallback.

#### Proposal and confirmation lifecycle

- **FR-027**: A state-changing request MUST create a durable proposal and MUST NOT directly execute a business effect.
- **FR-028**: A proposal MUST identify the capability, actor, target, current state, requested state, expected effect, risk category, validation summary, required confirmation type, and expiration.
- **FR-029**: A proposal MUST bind confirmation to the exact actor, capability, target, normalized inputs, relevant current state, and policy/capability version.
- **FR-030**: Every proposal MUST support explicit confirmation and cancellation and MUST expose pending, confirming, executing, succeeded, partially succeeded, cancelled, expired, invalidated, rejected, and failed states where applicable.
- **FR-031**: No state-changing operation MUST execute before a valid confirmation is durably accepted.
- **FR-032**: Ordinary state-changing operations MUST require an unambiguous confirmation action from the initiating Admin.
- **FR-033**: Destructive, financial, permission, security, account-disable, credential, and bulk operations MUST require an exact proposal-specific typed confirmation phrase.
- **FR-034**: Strong-confirmation comparison MUST be exact after only safe whitespace normalization; approximate, stale, partial, or case-altered content MUST NOT be silently accepted when the phrase is case-sensitive.
- **FR-035**: Confirmation MUST expire after a bounded period and MUST become invalid immediately if the actor, capability, policy, target, protected inputs, or relevant business state changes.
- **FR-036**: Cancelled, expired, invalidated, rejected, or unconfirmed proposals MUST create zero business effects.
- **FR-037**: A prompt containing multiple state changes MUST create reviewable proposals with separate confirmation unless an existing authoritative Admin operation already defines the request as one atomic bulk action.
- **FR-038**: Bulk proposals MUST show selection rules, total candidates, exclusions, representative preview, operation semantics, and expected partial-failure behavior before strong confirmation.

#### Safe execution and results

- **FR-039**: Immediately before execution, the platform MUST revalidate the Admin role, account/session status, capability version, target existence, relevant state, inputs, permissions, and all authoritative business rules.
- **FR-040**: Execution MUST invoke only the same authoritative business operation used by the original Admin workflow; arbitrary database mutation is forbidden.
- **FR-041**: Each confirmed proposal MUST produce at most one logical execution and one compatible business outcome under retries, duplicate clicks, callbacks, reconnects, restarts, and concurrent delivery.
- **FR-042**: Reuse of an execution identity with a different actor, proposal, or payload MUST be rejected.
- **FR-043**: The result MUST distinguish full success, partial success, validation rejection, stale rejection, authorization rejection, cancellation, provider failure, dependency failure, and unknown safe failure.
- **FR-044**: Successful execution MUST expose the affected safe references and refresh categories required to observe the authoritative resulting state.
- **FR-045**: Failed or partial execution MUST never be summarized as full success and MUST expose safe per-item or per-stage evidence when the original workflow supports partial outcomes.
- **FR-046**: Financial operations MUST preserve currency, precision, source documents, posting rules, period controls, and immutable accounting/audit behavior of the original operation.
- **FR-047**: Secret-bearing inputs required by an action MUST be captured through the original or equivalent secure input surface outside the AI context and transcript.
- **FR-048**: The model, client, or queue MUST NOT be treated as proof that a business action succeeded; only the authoritative recorded result may be shown as success.

#### Privacy, redaction, and model safety

- **FR-049**: Passwords, password hashes, access/refresh tokens, encryption keys, service credentials, connection secrets, session material, verification codes, protected verification answers, and equivalent secret fields MUST be permanently excluded from all agent read capabilities.
- **FR-050**: Prohibited secrets MUST NOT enter AI-provider requests, conversation text, action proposals, execution payload summaries, logs, metrics, traces, realtime events, exports, or audit before/after evidence.
- **FR-051**: Each read capability MUST retrieve only the fields and rows required for the question and MUST apply field-level redaction before data enters model context.
- **FR-052**: Sensitive but legitimate business details, including personal, HR, payroll, payment, and financial data, MUST appear only when relevant to the Admin's explicit question and existing business access.
- **FR-053**: The system MUST prevent prompt injection in user prompts, stored business content, attachments, logs, or retrieved text from changing policy, selecting uncatalogued capabilities, bypassing confirmation, or exfiltrating other data.
- **FR-054**: The system MUST validate and bound every model-produced decision before it can select a read capability, create a proposal, or influence a result.
- **FR-055**: Raw provider prompts, hidden instructions, reasoning traces, and unredacted retrieved payloads MUST NOT be exposed through the product or ordinary logs.
- **FR-056**: Attachments may be inspected only through an existing Admin-authorized safe content path, with type, size, malware, redaction, and prompt-injection controls; unsupported attachments MUST be refused.
- **FR-057**: The agent MUST refuse requests to weaken its access, redaction, confirmation, audit, or capability policies.

#### Conversation, audit, and lifecycle

- **FR-058**: Admins MUST be able to create, list, rename, reopen, continue, and intentionally archive their own conversations.
- **FR-059**: Conversation history MUST preserve message order, visible evidence references, proposal cards, action terminal states, and failure/cancellation states across refresh and restart.
- **FR-060**: Conversation retention and archive behavior MUST follow the existing platform retention policy; records linked to action evidence MUST NOT be hard-deleted through the chat surface.
- **FR-061**: Every read invocation MUST record the actor, conversation/turn, capability, safe scope, timing, completion status, and trace identifier without storing prohibited payloads.
- **FR-062**: Every proposal, confirmation attempt, cancellation, execution, rejection, expiry, invalidation, and failure MUST produce correlated, redacted administrative audit evidence.
- **FR-063**: Audit evidence MUST identify the actor, capability, safe target, risk, confirmation type and status, timestamps, result, and redacted before/after references sufficient to reconstruct the operation.
- **FR-064**: Another Admin MAY inspect redacted action evidence through existing audit authorization but MUST NOT gain access to the initiating Admin's private transcript by default.
- **FR-065**: Audit evidence and protected policy records MUST NOT be deletable or mutable by the agent.

#### UX, accessibility, and resilience

- **FR-066**: The conversation experience MUST be Arabic-first and RTL, while correctly rendering English names, identifiers, numbers, dates, currency, tables, code fragments, and mixed-direction text.
- **FR-067**: The surface MUST provide distinct and accessible loading, retrieving, reasoning, waiting-for-clarification, proposing, waiting-for-confirmation, executing, partial, success, empty, cancelled, expired, invalidated, rejected, provider-failed, dependency-failed, and retry states.
- **FR-068**: The interface MUST never communicate risk, confirmation, execution, or success by color alone.
- **FR-069**: Keyboard and screen-reader users MUST be able to navigate messages, evidence, proposals, strong-confirmation input, cancellation, results, history, and retry controls with predictable focus restoration.
- **FR-070**: The surface MUST remain usable without horizontal page scrolling at supported mobile, tablet, laptop, and desktop widths, including long Arabic and English content and large structured results.
- **FR-071**: The Admin MUST be able to stop a generating answer and cancel a pending proposal; cancellation outcomes MUST remain explicit after refresh.
- **FR-072**: Provider or dependency failures MUST fail closed, preserve the conversation and proposal state, avoid unconfirmed business effects, and provide safe retry guidance where compatible.
- **FR-073**: Rate and workload limits MUST protect the platform from unbounded scans, huge context, repeated expensive questions, and action flooding while returning explicit retry or narrowing guidance.
- **FR-074**: The product MUST show that answers are based on platform data and MUST avoid presenting the agent as an infallible authority.

### Non-Functional Requirements

- **NFR-001**: For production-like data, 95% of ordinary questions MUST show an acknowledged progress state within 2 seconds and a complete answer or explicit next state within 10 seconds; approved complex reports MUST continuously expose progress and finish or fail explicitly within their documented limit.
- **NFR-002**: 100% of tested numerical and financial answers MUST match authoritative deterministic results; at least 95% of a representative factual evaluation set MUST be fully correct, scoped, and evidence-backed, with no unsupported claims in the remainder.
- **NFR-003**: All tested confirmed operations MUST create at most one logical effect, and cancelled, expired, invalidated, rejected, unauthorized, or unconfirmed proposals MUST create zero effects.
- **NFR-004**: The capability inventory and contracts MUST remain deterministic and reviewable; probabilistic model output alone cannot expand capability access.
- **NFR-005**: The surface MUST meet WCAG 2.1 AA contrast, keyboard, focus, semantic announcement, and reduced-motion expectations in supported themes and widths.
- **NFR-006**: Conversation and action state MUST recover consistently after process restart, reconnect, duplicate delivery, and dependency interruption.
- **NFR-007**: Read and action workload MUST be bounded per Admin, conversation, turn, capability, result size, and time window, with no unbounded database scan exposed to the model.
- **NFR-008**: All persisted timestamps MUST retain an unambiguous instant and render in the Admin's configured Cairo-facing experience consistently with existing platform conventions.
- **NFR-009**: Existing Admin screens, non-Admin authorization, student/live-support flows, business transactions, and audit behavior MUST not regress.
- **NFR-010**: No existing production data may be deleted or reinitialized to introduce the feature; any schema evolution and capability baseline must preserve prior records.

### Key Entities

- **Admin AI Conversation**: One Admin-owned private conversation, including title, lifecycle, created/updated times, last activity, and retention/archive state.
- **Admin AI Turn**: One user request and its grounded response lifecycle, including status, safe citations/evidence references, cancellation, failure, and timing.
- **Read Capability Definition**: A versioned allowlisted business projection with input schema, bounded output, redaction rules, evidence behavior, and availability.
- **Action Capability Definition**: A versioned mapping from a current Admin operation to its target/input contract, risk, preview, confirmation type, authoritative execution, refresh behavior, and audit rules.
- **Capability Baseline**: The approved inventory snapshot against which complete first-release coverage and later drift are measured.
- **Action Proposal**: A durable no-effect intent bound to actor, action, target, normalized inputs, relevant state, risk, confirmation requirement, and expiry.
- **Confirmation Challenge**: The proposal-specific ordinary or strong confirmation record, attempts, outcome, and expiry without storing prohibited secrets.
- **Action Execution**: The exactly-once claim and authoritative terminal or partial result associated with a confirmed proposal.
- **Read/Tool Invocation Evidence**: Redacted trace of a capability call, safe scope, timing, status, and result reference.
- **Admin AI Audit Event**: Immutable correlated evidence for access, reads, proposals, confirmations, cancellations, executions, policy denials, and failures.
- **Sensitive Data Policy**: Versioned classification of permanently prohibited secrets, conditionally visible business data, safe evidence, and redaction tests.

## Out of Scope

- Student, guest, teacher, parent, assistant, staff, or live-support participant access to the Admin AI Agent.
- Reusing or merging student/live-support conversations, participant verification, handoff, routing, or rating state.
- Open-web browsing or treating external internet content as platform data.
- Raw SQL, arbitrary database writes, generated executable code, shell access, infrastructure administration, deployment, server control, or secret/configuration retrieval.
- Creating new business operations that do not already exist in an authoritative Admin workflow at the approved baseline.
- Letting the model define, publish, enable, or relax its own read, action, confirmation, redaction, audit, or retention policies.
- Replacing original Admin screens; they remain the authoritative manual workflow and fallback.
- Implementing any production code during the current Spec Kit run; implementation begins only after a separate owner review and approval.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of non-Admin access attempts across navigation, history, read, proposal, confirmation, execution, realtime, and audit paths are denied without disclosing business data.
- **SC-002**: 100% of representative numerical and financial questions match authoritative platform totals, and at least 95% of the complete factual evaluation set is correct, scoped, and evidence-backed.
- **SC-003**: Zero prohibited secrets appear in provider requests, replies, transcripts, proposals, logs, traces, metrics, exports, realtime events, or audit evidence across automated adversarial tests.
- **SC-004**: The approved capability baseline accounts for 100% of current Admin readable domains and state-changing operations; no active mutation is missing or mapped to generic raw execution.
- **SC-005**: 100% of tested state changes show a no-effect proposal before execution and execute only after a valid current confirmation.
- **SC-006**: 100% of tested destructive, financial, permission, security, account-disable, credential, and bulk operations reject missing or incorrect strong-confirmation phrases with zero effects.
- **SC-007**: Duplicate, concurrent, replayed, restarted, stale, expired, cancelled, and mismatched confirmation tests produce at most one compatible business effect and never a partial untracked effect.
- **SC-008**: Auditors can reconstruct actor, capability, safe target, proposal, confirmation state, authoritative result, timestamps, and redacted evidence for 100% of sampled action attempts.
- **SC-009**: At least 95% of ordinary questions show progress within 2 seconds and return an answer or explicit next state within 10 seconds on the agreed production-like dataset.
- **SC-010**: Admins complete representative read, ordinary action, strong-confirmation, history, cancellation, and recovery journeys by keyboard at 375px, 768px, 1024px, and 1440px with no critical accessibility, focus, overflow, theme, or reduced-motion failure.
- **SC-011**: Restart and dependency-failure tests retain consistent conversation/proposal/execution state and never claim success without an authoritative recorded result.
- **SC-012**: Every implementation phase, once separately authorized, passes its specified automated tests, Docker gate, manual QA, capability-coverage report, redaction evidence, and phase report before the next phase begins.

## Assumptions

- The existing built-in `Admin` role remains the only eligibility rule for the first release; there is no separate owner or Super Admin distinction.
- Admins already have legitimate business access to the included personal, financial, HR, support, and audit data through existing platform rules.
- Only the minimum relevant redacted data is sent to the configured AI provider; the provider and deployment must use the project's approved data-handling configuration.
- Conversation history follows the platform's existing retention policy. Each Admin sees their own transcripts, while redacted action evidence follows existing administrative audit access and retention.
- Secure fields required by an existing operation are entered through a protected form outside the conversation; supporting the operation does not make the secret readable by the agent.
- Multi-operation requests become separate proposals unless the original Admin workflow already defines a single atomic bulk operation; original partial/atomic semantics remain authoritative.
- "Every current Admin operation" means the versioned baseline captured when this specification is approved. New operations added afterward require a separate safe capability before the agent can execute them.
- Arabic is the primary UI and response language, with English names and technical terms supported when present in platform data.
- Current authentication, private file storage, audit, notification, financial, and business workflows remain authoritative dependencies rather than being reimplemented by the agent.
