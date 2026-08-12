# Research: Admin AI Agent

**Feature**: 169-admin-ai-agent
**Date**: 2026-08-11
**Status**: Technical decisions complete for planning. No implementation is authorized.

## Repository findings

- The platform has no Owner/SuperAdmin distinction. RoleType.Admin is the relevant built-in role, and current permission middleware/frontend policy treat it as full Admin.
- /admin/chat is human internal chat. /admin/live-support/ai configures and monitors the student support agent. Neither owns the semantics, privacy, or lifecycle required here.
- Existing live-support AI provides useful durable patterns—Outbox queueing, worker-only provider access, closed decisions, encrypted pending payloads, state fingerprints, confirmation, and recovery—but its participants, policies, actions, data, tables, and UI are student/support specific and must not be shared.
- The tracked endpoint inventory reports 349 backend endpoints and 321 frontend calls, but it is stale in the current worktree; its check mode fails. Its regex also misses or normalizes some modern C# controller forms incorrectly, so neither tracked nor regenerated regex counts can prove Admin coverage.
- The frontend mutation contract spans many services and hundreds of call sites, but it is a cache-refresh contract rather than an authorization/effect inventory. HTTP verb is also unreliable: a GET can mutate and a POST can be preview/export.
- Several current Admin/HR/finance controller actions contain direct DbContext writes. They must be extracted into authoritative application commands/services before becoming agent capabilities.
- AppDbContext.SaveChangesAsync is not a universal Admin audit writer, and idempotency support is selective. AdminAI needs its own durable execution/evidence guarantees while still reusing original business services.
- Current design sources are PRODUCT.md, DESIGN.md, frontend/src/app/globals.css, and the live Admin components: Tajawal/Montserrat, deep navy, teal, sparse gold, off-white/gray surfaces, Arabic-first RTL, and WCAG AA.

## Decision 1 — Separate AdminAI bounded context

**Decision**: Create a standalone AdminAI namespace, route, tables, contracts, queue processor, client feature, and audit chain. There are no foreign keys or shared state with live-support conversations/participants/messages/policies/actions or internal chat rooms.

**Rationale**: Actors, ownership, data scope, action authority, retention, and confirmation risks are different. Physical and semantic separation prevents student transcript leakage and avoids support-specific routing/handoff behavior.

**Alternatives rejected**:

- Extend /admin/live-support/ai: it administers a student-facing agent and would mix control plane with a private Admin assistant.
- Reuse /admin/chat: it is multi-participant human messaging and exposes typing/read-receipt/room semantics that are wrong here.
- Add a mode column to current chat tables: accidental joins, policies, and authorization bugs remain possible.

## Decision 2 — Every current built-in Admin may use it

**Decision**: Access requires a currently authenticated, active, non-deleted user with a live PostgreSQL UserRole to Role.Type Admin relation. Each conversation is owned by one Admin. Authorize(Roles = Admin) is the first filter, followed by IAdminAIAccessGate at every trust boundary.

**Rationale**: The approved product choice is any account carrying Admin, not one bootstrap owner. JWT or short-lived security cache state can become stale during a high-risk proposal.

**Revalidation points**: REST admission, list/snapshot, reconnect handling, turn creation/claim, every read, proposal creation, confirmation/cancel, secure continuation, and immediately before execution.

**Alternatives rejected**:

- Owner ID/config flag: contradicts confirmed role scope and no owner identity exists in the domain.
- Frontend-only adminOnly: useful UX, not security.
- Trust the claim until expiry: role removal or disablement could leave work executable.

## Decision 3 — Worker-only Gemini, backend-only tools/effects

**Decision**: The Node worker remains the sole @google/genai caller. The backend owns capability authorization, database reads, redaction, proposal construction, confirmation, and execution. The worker receives no database connection and cannot invoke controllers or business actions.

**Rationale**: This follows existing architecture and keeps credentials, EF rules, actor authorization, transactions, and audit inside .NET. A malformed model decision cannot expand authority.

**Alternatives rejected**:

- Let the worker query PostgreSQL: duplicates domain logic and makes row/field authorization and secret exclusion harder to prove.
- Let the model call Admin HTTP routes directly: route selection becomes probabilistic and bypasses proposal semantics.
- Add provider calls to .NET: creates two AI runtimes and violates the worker-only boundary.

## Decision 4 — Manual, durable Gemini function-calling loop

**Decision**: Use installed @google/genai 1.47.0 function declarations and manually execute each function request through the internal backend gateway. Do not use automatic function execution or experimental MCP support. Persist each model step and read invocation.

**Rationale**: The installed SDK documents a manual declare/generate/execute/function-response flow. Manual mediation allows schema validation, access checks, budgets, redaction, cancellation, audit, and replay.

**Closed decision union**: answer, clarify, request_reads, propose_actions, or refuse. Worker and backend reject unknown keys, extra branches, excessive nesting/length, an unknown schema version, or unlisted capabilities.

**Alternatives rejected**:

- One huge database context: violates minimization and cannot safely cover the platform.
- Free-form JSON or reasoning parsing: fragile and may expose hidden reasoning.
- Automatic/MCP tools: moves execution control outside the reviewed gateway.

## Decision 5 — Versioned bidirectional capability baseline

**Decision**: Freeze the baseline at implementation start from:

1. Runtime ASP.NET EndpointDataSource plus controller/action/auth/permission metadata.
2. A reachable frontend AST/import graph starting at Admin routes and navigation.
3. MediatR/application service, upload, export, queue, finance, HR approval, and external-side-effect review.
4. Manual semantic classification.

The immutable manifest records source route/handler, kind, domain, schemas, safe projection, limits, risk, confirmation, state fingerprint, idempotency, transaction/concurrency, secure input, bulk semantics, audit, and refresh scopes.

**Rationale**: “All current Admin actions” must be testable. Runtime/reachable inventories catch both backend-only and UI-reachable drift; manual semantics catch GET side effects, POST previews, and direct-controller logic.

**Disposition values**: Supported, ReadOnly, SecureContinuation, Excluded. Current Admin business mutations cannot remain Excluded at release. Legitimate exclusions are internal callbacks, E2E routes, infrastructure/deployment, generated code, and role-specific self-service that is not an Admin business workflow.

**Alternatives rejected**:

- Regex endpoint inventory alone: currently stale and parser-limited.
- Frontend service scan alone: includes unreachable/non-Admin calls and misses backend-only work.
- Generic CRUD/route capability: loses validation, transaction, approval, accounting, file, and audit semantics.

## Decision 6 — Typed bounded read projections

**Decision**: Each read capability is a named query with a closed input schema and projection DTO. It defines allowed filters/sorts, default/max page size, time/query budget, deterministic calculations, dataAsOf, result count, completeness/truncation, and safe drill-down mappings.

**Global ceilings**: 3 model steps, 6 total read invocations, 4 reads in one step, 64 KiB total redacted tool results, 200 records per invocation, 5 seconds per query, and a 30-second ordinary provider deadline. Capabilities normally use stricter limits.

**Rationale**: This prevents unbounded scans and context growth, preserves query-plan review, and makes evidence/completeness visible. Financial/count calculations happen in .NET/SQL projections and are only explained by the model.

**Alternatives rejected**:

- Raw SQL or natural-language-to-SQL: cannot safely enforce semantic authorization and stable budgets.
- Return EF entities then redact: a new property can leak by default.
- Give the model raw exports: defeats minimization and context limits.

## Decision 7 — Field allowlists with permanent secret exclusion

**Decision**: Projection allowlists are primary. A versioned sensitive-data policy forbids passwords/hashes, access/refresh/session tokens, encryption/service/connection secrets, fingerprints/session material, verification codes/answers, and equivalent secret types everywhere. Denylist/reflection and sentinel tests provide defense in depth.

**Rationale**: Name-based regex alone can miss aliases or nested values. Never retrieve is stronger than retrieve then mask.

**Legitimate sensitive data**: PII, HR, payroll, payment, and financial information can be returned only when relevant to the explicit Admin question and existing access, with the smallest required fields/rows.

**Alternatives rejected**:

- Let Admin request any column: the approved scope permanently excludes technical secrets.
- Regex masking only: too easy to bypass through renamed/nested fields.
- Store full tool results for audit: expands breach impact and persists unnecessary PII.

## Decision 8 — Server-built proposal, never model-built authority

**Decision**: The model may suggest a catalog key and safe arguments, but the backend reloads the target, validates the action contract, computes current/requested state and effects, classifies risk, derives the fingerprint, and creates the proposal. A proposal has no business effect.

**Rationale**: The model cannot be trusted to determine current state, affected count, money, confirmation mode, or consequences. Preview must match the authoritative operation.

**Multiple actions**: independent changes create independent cards and confirmations. One proposal may contain multiple items only if the original workflow is an atomic or explicitly partial bulk operation.

**Alternatives rejected**:

- Execute after conversational “yes”: ambiguous and not payload-bound.
- Trust a model-generated count/summary: it can be stale or hallucinated.
- One blanket confirmation for all changes: hides independent risk and failure.

## Decision 9 — Ordinary and strong confirmation

**Decision**:

- Ordinary mutations require a proposal-specific explicit button with a concrete verb.
- Destructive, financial, permission, security, account-disable, credential, and bulk capabilities require an exact server-generated phrase with an 8-character challenge.
- Phrase normalization is NFC plus leading/trailing trim plus collapsing whitespace. No case folding, punctuation/digit substitution, fuzzy comparison, or reuse.
- Persist only an HMAC digest. Default TTL is 5 minutes; configurable range is 60–900 seconds and a capability may be shorter.

**Rationale**: A deliberate second input makes high-impact work hard to trigger accidentally while remaining usable in Arabic.

**Alternatives rejected**:

- Checkbox/button only for high risk: insufficient friction for broad conversational control.
- Password/OTP in chat: introduces prohibited secrets and does not prove payload review.
- Very long target-dependent phrase: poor accessibility without meaningful extra assurance.

## Decision 10 — Durable exactly-once execution ledger

**Decision**: One AdminAIActionExecution is uniquely bound to each proposal. Actor/idempotency identity is bound to a normalized payload hash. Compatible retries replay the terminal result; conflicting payload reuse is rejected. Final execution reauthorizes and recomputes state inside the authoritative transaction/lock boundary.

**Rationale**: UI disabling and BullMQ IDs do not prove exactly once under two tabs, callbacks, restarts, or external timeouts. Existing Admin operations have uneven idempotency, so unsupported ones must be refactored before catalog inclusion.

**External operations**: The original command must accept a deterministic idempotency identity and recover an authoritative result after ambiguous timeout. Otherwise the capability cannot pass release.

**Alternatives rejected**:

- In-memory/Redis lock only: locks expire and Redis is not authoritative.
- Mark success before original operation commits: can show false success.
- Retry every failure automatically: can duplicate external/financial effects.

## Decision 11 — Secure continuation outside the transcript

**Decision**: Passwords, token replacements, protected answers, private files, and similar inputs use a short-lived secure overlay or original-screen deep link. The backend issues an opaque actor/proposal-bound grant. Raw values never return to the agent, transcript, model, cache, realtime, or audit and are cleared client-side immediately.

**Rationale**: Complete Admin coverage includes protected workflows, but these values are prohibited from conversation context.

**Alternatives rejected**:

- Ask Admin to paste secrets/files into chat: directly violates policy.
- Permanently exclude these workflows: contradicts complete v1 coverage.
- Put encrypted secrets in message content: still expands access and retention risk.

## Decision 12 — PostgreSQL truth, Outbox/BullMQ delivery

**Decision**: PostgreSQL stores conversations, messages, turns/steps, read evidence, baseline/policy versions, proposals, challenges, executions, and audit events. The API transaction writes AdminAITurnQueued to Outbox; dispatcher enqueues ai-admin-agent-turns with a stable job ID. A bounded recovery service expires proposals and recovers stale turns/executions without inventing success.

**Rationale**: The repository already uses this durable delivery shape. It survives restart and duplicate delivery while keeping Redis disposable.

**Alternatives rejected**:

- Redis chat state: loses durable audit and recovery authority.
- Synchronous Gemini request: fragile under latency/restarts.
- No durable step rows: ambiguous recovery after a failure.

## Decision 13 — Reuse PlatformHub as notification transport

**Decision**: Reuse /hubs/platform and existing user-specific groups. Outbox sends only owner-safe AdminAI notification envelopes. REST list/snapshot is authoritative; sequence gaps or unknown versions trigger refetch.

**Rationale**: An authenticated user-specific connection already exists and avoids another WebSocket, proxy path, and lifecycle. Minimal payloads prevent transcript leakage.

**Alternatives rejected**:

- Dedicated AdminAI hub: adds runtime/proxy/reconnect complexity without improving owner routing.
- Chat/live-support hubs: couple wrong groups and semantics.
- Full snapshot in realtime: duplicates sensitive state and is unsafe for replay.

## Decision 14 — Append-only AdminAI evidence plus AuditLog summary

**Decision**: Persist correlated append-only AdminAIAuditEvent rows for reads, proposals, confirmation attempts, cancellation, execution, rejection, expiry, invalidation, and failures. Existing AuditLog receives a redacted linked summary for ordinary audit discovery. Neither API nor agent exposes evidence update/delete.

**Rationale**: Existing audit coverage is not universal, and raw OldValues/NewValues may contain PII. A dedicated safe schema proves lifecycle without leaking transcript/provider payload.

**Alternatives rejected**:

- Ordinary logs only: mutable/retention-limited and weakly correlated.
- Raw prompts/results in audit: exposes hidden instructions and unnecessary data.
- Original operation audit only: misses reads and rejected proposals.

## Decision 15 — Standalone Admin workspace

**Decision**: Add /admin/ai-agent as a prominent standalone Admin-only item after Admin home. Reuse persistent shell, AdminPage, current tokens, and cache infrastructure, but create feature/service contracts independent from chat/live support.

**Layout**:

- At least 1024px: 280–320px history plus transcript/composer; evidence inline/collapsible.
- 768–1023px: history drawer or list-to-conversation drill-in.
- 375px: one pane, safe-area composer, one transcript scroll, no document horizontal scroll.

**Rationale**: The user asked for a private Admin chat on its own. Current brand sources require navy authority, teal progress, sparse gold, Tajawal, RTL, and dense but clear Admin layout.

**Alternatives rejected**:

- Put under communications/live support: implies wrong relationships.
- Reuse support pending-action card: lacks full before/after/risk/impact semantics.
- Three permanent desktop panes: squeezes Arabic evidence and adds load.

## Decision 16 — Accessible explicit state model

**Decision**: Expose every FR-067 state with text and icon, inline durable errors, predictable focus, a role=log transcript, separate polite status announcements, mixed-direction bdi for IDs/currency/dates, 44px touch targets, reduced motion, and no token-by-token announcements.

**Rationale**: Long AI work and high-risk confirmation must never look frozen or rely on color. RTL/mixed identifiers and mobile shell geometry are known failure points.

**Alternatives rejected**:

- Toast-only state: disappears and is not reconstructable after refresh.
- Unconditional autoscroll: steals the reader's position.
- Client-only phrase validation: may aid UI but cannot authorize.

## Decision 17 — Rate/work budgets and cancellation

**Decision**: Add distributed per-Admin policies: 10 turn admissions/minute, 20 confirmations/minute, 120 internal callbacks/minute/IP, and at most 2 active turns per Admin. Tool/model ceilings are defined in Decision 6. Cancellation is durable; a late callback cannot create an answer/proposal after cancellation wins.

**Rationale**: Provider/database work is expensive and operates across nodes. Two turns preserve useful parallel work without flooding.

**Alternatives rejected**:

- Unlimited Admin traffic: compromise or loops can overload the platform.
- One global Admin lock: blocks independent work unnecessarily.
- Browser-only cancellation: worker/provider could still create late state.

## Decision 18 — Feature disable and rollout

**Decision**: AdminAI:Enabled defaults false outside reviewed environments. Disabled state hides nav and rejects new turns/proposals while preserving owner read-only history and terminal evidence; pending proposals are invalidated. Only one Active capability baseline is accepted.

**Rationale**: Complete action coverage requires an operational kill switch that does not destroy evidence.

**Alternatives rejected**:

- Delete data or rollback migration: destructive and slow.
- Disable only worker: requests would queue indefinitely.
- Continue pending proposals after disable: violates operator intent.

## Decision 19 — No open web or vector database

**Decision**: Answer platform questions only from bounded capabilities and approved static platform knowledge. Do not browse the web and do not add a vector store in this release.

**Rationale**: The request is authoritative platform data/actions. Web facts and vectorized relational rows weaken freshness, authorization, and financial correctness.

## Decision 20 — Verification defines completion

**Decision**: Release requires zero unmapped current Admin business mutations, zero duplicate/stale mappings, zero prohibited sentinel leakage, generated and representative parity/security tests, real PostgreSQL concurrency/restart evidence, real-provider Docker acceptance, and owner manual/accessibility review.

**Rationale**: The requested breadth makes evidence more important than implementation claims.

**Alternatives rejected**:

- Sample operations and infer the rest: cannot prove “all.”
- Treat compilation as completion: misses data/auth/concurrency/external/UX failures.
- Waive difficult direct-controller work: silently narrows confirmed v1.

## Resolved unknowns and deferred evidence values

No product-level clarification remains. Exact baseline counts, manifest hash, migration timestamp, production capacity thresholds, and capability-specific limits stricter than the global ceilings are derived from sealed implementation source and representative data. They are evidence values, not unresolved feature decisions.
