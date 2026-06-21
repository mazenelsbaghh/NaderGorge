# Research: AI Live Support Agent

## Decision 1: Backend is the only business authority

**Decision**: The model returns a strict decision proposal. Backend services own context construction, policy checks, identity, confirmations, actions, account linking, resolution, and handoff.

**Rationale**: The existing live-support action catalog delegates to validated MediatR commands and records audit/idempotency. Letting the worker call tools would bypass ownership and make prompt injection a business-write path.

**Alternatives considered**:
- Automatic model function calling into admin APIs: rejected because provider output would hold execution authority.
- Read-only chatbot: rejected because the approved feature explicitly requires admin-selected actions.

## Decision 2: Dedicated low-latency BullMQ queue

**Decision**: Add `ai-live-support-turns` with deterministic turn job IDs, configurable concurrency, 6-second provider timeout, maximum queue age, one bounded transient retry, and separate callback-delivery retry.

**Rationale**: The current shared Redis stream can block for two seconds, uses serial bridge handling, worker concurrency one, and five retries starting at five seconds. Repeating inference because callback delivery failed wastes cost and risks duplicates.

**Alternatives considered**:
- Reuse `ai-video-chapters`: rejected due hour-scale work and incompatible latency/retry policy.
- Call Gemini directly from ASP.NET: rejected by the provider-abstraction constitution.
- Synchronous browser-to-worker call: rejected because it loses durable state and trusted authorization.

## Decision 3: Transactional outbox before queue delivery

**Decision**: Participant message, AI turn, event, and `AISupportTurnQueued` outbox record commit together. Outbox dispatch performs Redis stream enqueue and marks processed only after success.

**Rationale**: Direct XADD after database commit can lose work; XADD before commit can process nonexistent state. Existing outbox retry/dead-letter patterns can be extended.

**Alternatives considered**:
- Direct `IJobEnqueuer` call: rejected for dual-write failure.
- Redis as authoritative turn store: rejected because restart and audit guarantees require PostgreSQL.

## Decision 4: Structured output schema with server validation

**Decision**: Use `application/json` structured output and a closed discriminated decision schema, then validate again in worker and backend. The installed `@google/genai` 1.47 interface exposes `responseJsonSchema`/`responseSchema`; official Gemini documentation confirms structured output for agentic workflows and supported JSON-schema subsets.

**Rationale**: A closed union makes reply/action/verification/resolution/handoff explicit and prevents arbitrary tool names or payloads from becoming executable.

**Alternatives considered**:
- Parse free-form Markdown: rejected as nondeterministic and unsafe.
- Automatic function calling: rejected because the model would control tool invocation.

**Primary references**:
- https://ai.google.dev/gemini-api/docs/structured-output
- https://googleapis.github.io/js-genai/release_docs/interfaces/types.GenerateContentConfig.html

## Decision 5: Immutable published policy and knowledge revisions

**Decision**: Drafts are mutable; publication creates immutable policy/knowledge revisions and atomically marks one policy active. Every turn references exact versions.

**Rationale**: Historical decisions must be reconstructable and policy changes must invalidate stale actions without changing old evidence.

**Alternatives considered**:
- Mutable global settings rows: rejected because old AI answers could not be explained.
- Copy entire knowledge text into every turn: rejected for duplication and sensitive retention.

## Decision 6: PostgreSQL text retrieval first

**Decision**: Rank published knowledge with normalized PostgreSQL text search and strict result/character limits. Do not add a vector database in this phase.

**Rationale**: Knowledge is admin-authored and initially bounded. Existing PostgreSQL is operationally available; retrieval quality can be measured before adding another service.

**Alternatives considered**:
- Full knowledge in every prompt: rejected for cost, context overflow, and prompt-injection surface.
- New vector store: rejected as unproven operational complexity.

## Decision 7: AI conversation mode is separate from human status

**Decision**: Add a one-to-one AI state with `AiActive`, `HumanQueued`, `HumanAssigned`, `AiResolved`, and terminal/failed facts while retaining the existing conversation status for participant closure and human routing.

**Rationale**: Existing `Waiting` means human queue and currently consumes routing semantics. AI must operate 24/7 without a queue entry until handoff.

**Alternatives considered**:
- Add AI values to the existing status only: rejected because it tangles participant lifecycle with human capacity.
- Separate conversation table: rejected because it fragments transcript and audit history.

## Decision 8: Handoff is irreversible and wins races

**Decision**: A serializable, locked command changes mode, invalidates pending work, inserts one queue entry, and records event/audit/outbox atomically. Callbacks compare mode/version; stale callbacks become discarded evidence and never messages.

**Rationale**: Worker cancellation cannot prevent a response already in flight. Only an authoritative callback guard can guarantee zero post-handoff AI messages.

**Alternatives considered**:
- Best-effort job cancellation: rejected as advisory only.
- Allow AI to continue until staff accepts: rejected by the confirmed product decision.

## Decision 9: Confirmation is a durable proposal, not conversational intent

**Decision**: Store exact safe proposal, payload hash, account, policy and state fingerprints, expiry, and idempotency. Participant confirm/cancel endpoints perform transitions; model text cannot count as confirmation.

**Rationale**: Natural-language confirmation is ambiguous and vulnerable to races. The UI must show exact effect and collect explicit consent.

**Alternatives considered**:
- Treat “yes” as execution: rejected due ambiguity and stale state.
- Reuse staff confirmation version alone: rejected because AI needs policy/account/payload binding and participant consent evidence.

## Decision 10: Dedicated AI actor without staff impersonation

**Decision**: AI execution uses a stable system actor identity/type and a dedicated executor that reuses action metadata and MediatR business commands but does not require staff ownership, attendance, or presence.

**Rationale**: Faking a staff ID corrupts accountability. Reusing the existing business commands preserves validation while audit clearly attributes AI and confirming participant.

**Alternatives considered**:
- Use admin user as actor: rejected because no human performed the action.
- Give worker direct database access: rejected by layering and audit rules.

## Decision 11: Guest verification remains backend-controlled

**Decision**: Admin publishes safe complete-value lookup keys and questions. Backend selects candidate and questions, compares normalized answers in memory, stores no raw answer, and binds success to one conversation. Multiple matches or exhausted attempts hand off.

**Rationale**: Expected answers must never enter model context. Generic lookup responses prevent account enumeration.

**Alternatives considered**:
- AI evaluates answer similarity: rejected because it would receive secrets and make ownership probabilistic.
- Persist verification across sessions: rejected by explicit clarification.
- OTP: excluded by the approved request.

## Decision 12: Secure account creation flow

**Decision**: AI guides a structured account proposal, but secret fields are collected in secure participant-owned controls excluded from transcript/provider context. Confirmation reuses existing account validation and exactly-once creation.

**Rationale**: Existing support creation accepts a password but redacts only after receipt. The AI must never see or generate the secret.

**Alternatives considered**:
- Model-generated password: rejected as insecure.
- Put password in a chat message: rejected because messages are durable and visible to staff.

## Decision 13: Inactivity closes with warning and cancellation

**Decision**: A hosted service uses durable last-participant activity, one warning timestamp, configured timeout/grace, and optimistic version checks. New participant activity cancels the pending close.

**Rationale**: The confirmed product behavior combines resolution confirmation with admin-configured inactivity closure and advance warning.

**Alternatives considered**:
- Browser timer: rejected because it fails on disconnect/restart.
- Immediate timeout closure: rejected because users need warning and cancellation.

## Decision 14: Built-in Admin-only configuration

**Decision**: AI configuration, preview, publication, enable/disable, and evidence endpoints require the built-in Admin role, not `HasPermission` custom-role delegation. Assigned staff receive only safe handoff context.

**Rationale**: The owner explicitly selected non-delegable Admin access. These settings control data exposure and business-write capability.

## Decision 15: UI follows existing product register

**Decision**: Add a modular admin route and focused participant states using existing Massar navy/teal tokens, Tajawal, RTL, standard form/tab/table patterns, 44px actions, visible focus, reduced motion, restrained color, and moderate 12–16px radii.

**Rationale**: `PRODUCT.md`/`DESIGN.md`, `ui-ux-pro-max`, and `impeccable` require a trustworthy dense admin tool, not a generic AI dashboard aesthetic.

**Alternatives considered**:
- Add all controls to `AdminLiveSupportPageClient`: rejected because it is already dense and would violate modularity.
- Decorative “AI” gradients/glass: rejected as inconsistent and lower trust.
