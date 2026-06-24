# Research: AI Live Support Production Completion

## Evidence Baseline

- Feature 142 human support is broadly implemented and has application, PostgreSQL concurrency, and Playwright coverage.
- Feature 143 contains the intended production architecture, but most tasks from application ports through recovery, security, UI modules, and final acceptance remain unchecked.
- Features 144 and 145 added admin statistics, enable/disable, action cards, handoff, verification, and registration directly into existing services. Their task checkmarks do not prove the missing Feature 143 orchestration or worker contracts.
- `LiveSupportService.cs` is over 2,000 lines and currently owns routing, messages, AI context, callback completion, actions, handoff, verification, and account creation.
- Participant message creation saves before directly calling `IJobEnqueuer`, leaving a database/Redis dual-write gap.
- `RedisJobEnqueuer` maps unknown queue names to `notification`, which is fail-open behavior.
- `processLiveSupportTurn.ts` retries inference and callback as one BullMQ operation and may report raw error messages as safe failure detail.
- AI-specific tests currently cover models, catalogs, authorization, and basic admin service behavior only. No `live-support-ai.spec.ts`, worker live-support tests, or AI PostgreSQL race suite exists.
- Baseline on 2026-06-24: backend solution build succeeded with five warnings; worker build plus 31 tests passed; frontend typecheck passed; frontend lint produced four warnings, including two AI card unused catch variables. A parallel backend test collided on shared build artifacts and must be rerun sequentially.

## Decision 1: Feature 146 is the completion authority

**Decision**: Treat Specs 142–145 as evidence and compatibility inputs. Feature 146 owns gap closure and resolves contradictions, while retaining all previously valid contracts and URLs.

**Rationale**: Checkboxes in later specs claim completion despite missing foundational tasks. A single verified authority is needed for implementation and acceptance.

**Alternatives considered**:
- Resume only Feature 143: rejected because Features 144 and 145 changed code and behavior after it.
- Patch each old spec independently: rejected because it would preserve contradictory completion evidence.

## Decision 2: Extract AI modules without rewriting human support

**Decision**: Keep `LiveSupportService` as the compatibility facade for existing participant/staff routes, but delegate all AI behavior to focused Application ports and Infrastructure services.

**Rationale**: The human routing implementation is already exercised. A full rewrite creates regression risk, while continuing to add methods to the facade prevents transaction and authorization reasoning.

**Alternatives considered**:
- Leave the monolith and add regions/helpers: rejected because dependencies and transaction boundaries remain implicit.
- Create a separate deployable AI service: rejected because it adds distributed business authority and violates current architecture.

## Decision 3: PostgreSQL plus outbox is authoritative

**Decision**: Commit participant message, event, conversation update, AI turn, and queue-intent outbox event atomically. Dispatch Redis only from the outbox processor.

**Rationale**: Current direct enqueue can lose work after commit or create retries that cannot be reconstructed. PostgreSQL already stores `OutboxEvent` and has retry/dead-letter handling.

**Alternatives considered**:
- Redis as turn authority: rejected because audit, restart, and data-preservation requirements are durable.
- Direct enqueue with compensating scan only: rejected because normal delivery would still be a dual write.

## Decision 4: Queue mapping fails closed

**Decision**: `RedisJobEnqueuer` uses an explicit supported mapping and throws for unknown queue/job combinations. Live-support job ID is `turn:{turnId}`.

**Rationale**: The current default-to-notification branch can misroute sensitive work and hide configuration errors.

**Alternatives considered**:
- Log and ignore unknown jobs: rejected because accepted work would disappear.

## Decision 5: Inference retry and callback retry are separate

**Decision**: Run at most one classified transient inference retry within the provider deadline. Persist the validated decision result for callback delivery retries so callback failure cannot invoke the provider again.

**Rationale**: Repeating inference increases cost and can produce a different decision. Exactly-once logical behavior requires deterministic callback replay.

**Alternatives considered**:
- Let BullMQ retry the whole processor: rejected because claim, inference, and callback have different idempotency and cost profiles.

## Decision 6: Closed decision schema at worker and backend

**Decision**: Use a versioned discriminated union with exactly six decision types. Apply length, count, enum, required-branch, extra-field, and catalog-key validation in the worker, then repeat trust-boundary validation in the backend.

**Rationale**: Provider structured output improves shape but is not authorization. Backend must reject stale or policy-disallowed decisions.

**Alternatives considered**:
- Free-form JSON/Markdown: rejected as ambiguous and unsafe.
- Provider function calling into platform tools: rejected because it grants the model execution authority.

## Decision 7: Context is backend-built, allowlisted, and bounded

**Decision**: Build structured context packets from active policy keys, an immutable policy version, a bounded transcript window, a safe summary, safe student projections, and ranked published knowledge revisions. Mark all participant and knowledge content as untrusted data.

**Rationale**: The current method appends all selected knowledge and builds human-readable context inside the service without complete bounds or dedicated redaction tests.

**Alternatives considered**:
- Worker database access: rejected because it bypasses application authorization.
- Full transcript and all knowledge: rejected for latency, cost, injection surface, and retention risk.
- Vector database now: rejected because existing PostgreSQL text ranking is sufficient until measured quality shows otherwise.

## Decision 8: Pending decisions need explicit type and nullable target

**Decision**: Add `DecisionKind` to pending decisions and make `StudentUserId` nullable. Action proposals require a target; handoff, account creation, and resolution do not. Backfill existing rows from stable action keys.

**Rationale**: Current guest handoff can construct `StudentUserId = Guid.Empty` against a user foreign key. Overloading action keys without a kind makes validation and UI routing brittle.

**Alternatives considered**:
- Keep sentinel GUIDs: rejected because they violate referential integrity and conceal missing identity.
- Create a separate table for every proposal type: rejected as unnecessary duplication of expiry/status/idempotency lifecycle.

## Decision 9: Sensitive payloads are encrypted and non-display data is hashed

**Decision**: Store safe display JSON separately. Encrypt execution payloads using the existing application protection boundary or a new focused encryption port. Store keyed hashes for payload identity, lookup values, state fingerprints, and confirmation nonces.

**Rationale**: Current entities define encrypted bytes and hashes, but current flows leave important values empty or use plain SHA-style logic. Secrets must remain outside transcript/provider/logs and still support idempotency.

**Alternatives considered**:
- Plain JSON arguments: rejected because action and registration payloads can contain private data.
- Irreversible hash for execution payload: rejected because confirmed actions need validated arguments.

## Decision 10: AI execution uses a dedicated system actor

**Decision**: Add an AI action executor that invokes existing authoritative MediatR/business commands with an auditable system actor, participant confirmation, linked-student scope, and policy evidence. It must not impersonate staff presence or ownership.

**Rationale**: Reusing staff action methods with `Guid.Empty` or admin flags bypasses ownership semantics and weakens audit attribution.

**Alternatives considered**:
- Direct EF updates: rejected because they duplicate business validation and audit.
- Fake admin identity: rejected because audit evidence becomes false.

## Decision 11: Registration reuses the authoritative workflow atomically

**Decision**: Orchestrate the established registration/profile validation, account creation, conversation linking, session outcome, and audit in one recoverable workflow. Never create only the minimal `AdminCreateUserCommand` profile and patch a subset of fields afterward.

**Rationale**: The current code risks incomplete profiles, account-created/link-failed partial results, and different validation from the public registration journey.

**Alternatives considered**:
- Maintain a support-specific registration clone: rejected because registration rules will drift.

## Decision 12: Verification is non-disclosing and answerless at rest

**Decision**: Exact-value lookup always presents one generic public result. The server selects questions; expected and submitted answers are compared in memory. Persist only keyed lookup digest, question keys, outcome codes, attempts, and timestamps.

**Rationale**: Account existence and raw personal answers are security boundaries. Dummy sessions must not accidentally reveal different timing, prompt, or state shapes.

**Alternatives considered**:
- Return no-match immediately: rejected due enumeration signal.
- Send answers to the model: rejected because the model does not need identity secrets.

## Decision 13: One irreversible handoff coordinator owns races

**Decision**: Perform handoff under serializable transaction plus existing routing lock. Invalidate AI turns and pending decisions, set AI mode, create at most one active queue entry, record safe context, and invoke assignment in the same authoritative path.

**Rationale**: Handoff is triggered by participant confirmation, verification exhaustion, provider failure, unsafe decision, missing capability, and emergency disable. Duplicated implementations will race.

**Alternatives considered**:
- Each caller updates status independently: rejected because callback-versus-handoff and duplicate-queue races are unavoidable.

## Decision 14: Recovery is indexed, bounded, and observable

**Decision**: A background service processes bounded batches for stale turns, expired decisions, verification expiry, inactivity warnings/closure, and disable reconciliation. Each transition is idempotent and emits safe telemetry.

**Rationale**: Current live-support recovery focuses on staff presence and does not close AI lifecycle gaps.

**Alternatives considered**:
- Rely only on BullMQ retry: rejected because pending database states and disablement also require reconciliation.

## Decision 15: REST snapshot is authoritative; SignalR advances it

**Decision**: Initial load and reconnect fetch a snapshot with last event sequence, current mode, active pending decision, verification state, ownership, and composer permissions. SignalR events carry monotonically increasing sequence and invalidate/refetch on gaps.

**Rationale**: Client-only event accumulation cannot recover missed or reordered events and can re-enable stale controls.

**Alternatives considered**:
- SignalR replay only: rejected because disconnect duration and event retention are variable.

## Decision 16: Preserve Massar design identity

**Decision**: Use existing Deep Navy `#0A1D3D`, Teal `#0E8F8F`, Warm Gold `#D4A017`, off-white surfaces, Tajawal, Lucide icons, restrained product density, moderate radii, and state-driven 150–250ms motion.

**Rationale**: `PRODUCT.md` and `DESIGN.md` are authoritative. The generic UI search recommendation of dark slate/green and Noto fonts conflicts with the committed brand. Chat is a task surface, not a decorative AI dashboard.

**Alternatives considered**:
- Adopt generic dark AI dashboard styling: rejected for brand conflict, poor student daylight use, and inconsistent app vocabulary.
- Redesign all shared shells: rejected because this feature should normalize live support to the existing system, not replace it.

## Decision 17: Role-specific responsive information architecture

**Decision**: Participant is a one-column stateful flow at 320px. Staff uses queue, transcript, and student context with breakpoint-driven drill-in. Admin separates operations, policy, knowledge, verification, preview, evidence, and performance into standard tabs/modules.

**Rationale**: One layout cannot serve mobile participants and dense operators. Current page clients are large and mix data loading, polling, mutations, and rendering.

**Alternatives considered**:
- Responsive scaling of one three-pane layout: rejected because it hides task priority and creates overflow.

## Decision 18: Test the real provider through the deployed path

**Decision**: Keep deterministic provider doubles for automated edge cases, then require one Docker E2E using the configured provider from participant send through durable callback and UI transcript.

**Rationale**: Mock tests prove logic but not credentials, model availability, schema support, network, timeout, callback routing, or container secrets.

**Alternatives considered**:
- Provider CLI smoke only: rejected because it bypasses queue, backend context, callback, and UI.
- Mock-only acceptance: explicitly rejected by the product owner.

## Decision 19: Additive migration and rollback by application version

**Decision**: Add nullable/backfilled columns and indexes, retain existing tables and rows, validate before adding restrictive checks, and never drop historical fields in this feature. Rollback disables AI and rolls back images, not data.

**Rationale**: The user explicitly requires preservation of current conversations, policy, action, and audit data.

**Alternatives considered**:
- Recreate AI tables: rejected as destructive.
- Down migration that drops new evidence after use: rejected because it can destroy post-deployment history.

## Decision 20: Package-version warning is fixed in scope

**Decision**: Align the integration test project's EF Core relational dependencies with the production EF Core 9.0.6 set.

**Rationale**: The baseline build reports an unresolved 9.0.1 versus 9.0.6 conflict. Real migration/concurrency evidence is not trustworthy while test runtime dependencies differ.

**Alternatives considered**:
- Ignore the warning because build succeeds: rejected because integration behavior can differ at runtime.
