# Implementation Plan: Admin AI Agent

**Logical Feature**: `169-admin-ai-agent`
**Workspace Branch**: planning-only; no branch was created or switched because implementation is not authorized and the worktree already contains owner changes
**Date**: 2026-08-11
**Spec**: [spec.md](./spec.md)
**Input**: A standalone, private Admin Shell AI workspace for every built-in `Admin`, grounded in all platform business data and able to propose every current Admin operation. Every mutation requires confirmation; high-risk work requires a typed challenge. This run ends after `tasks.md`.

> **Owner gate:** This document is a design, not authorization to change product code, create a migration, install dependencies, deploy, or execute Phase 5. Implementation remains stopped until the owner reviews the artifacts and gives a new explicit approval.

## Summary

Build a completely separate `AdminAI` feature across the existing .NET API, Node worker, Next.js Admin surface, PostgreSQL, Redis/BullMQ, Outbox, and SignalR infrastructure. The Node worker remains the only Gemini client. It can ask for a closed set of typed read tools, but the backend alone authorizes the current Admin, executes bounded/redacted read projections, constructs action proposals, and invokes authoritative MediatR/application services after confirmation. The worker receives no database credentials and never executes an Admin action.

The first implementation task freezes a runtime-derived and manually reviewed capability baseline. Release coverage is bidirectional: every reachable Admin read/mutation must have exactly one safe capability or an explicit non-business exclusion, and every capability must point back to a live Admin workflow. A stale, missing, duplicated, generic, or untested mapping fails the release. The current regex endpoint inventory is known to be stale and cannot be used as proof of complete coverage.

## Technical Context

**Language/Version**: C# 13 on .NET 9; TypeScript 5.9 strict on Node.js `>=22.13`; Next.js 16.2.7 and React 19.2.4
**Primary Dependencies**: ASP.NET Core Web API, MediatR 12.4.1, FluentValidation 11.11, EF Core 9.0.6/Npgsql 9.0.4, SignalR 9.0.6 with Redis backplane, StackExchange.Redis 2.12.4; Next.js App Router, Axios, Zustand, Tailwind CSS 4, Lucide React, SignalR client 10; BullMQ 5.71.1, ioredis 5.10.1, `@google/genai` 1.47.0, undici 7.24.6
**AI Provider**: Gemini Developer API through the existing worker-only `GEMINI_API_KEY`; current default text model comes from `worker/src/services/aiConfig.ts` and is `gemini-3.6-flash` unless `AI_TEXT_MODEL` overrides it
**Storage**: PostgreSQL 16 is authoritative; Redis is delivery/coordination only; existing private attachment storage is used only through secure continuation flows; no vector database
**Testing**: xUnit application/integration tests with real PostgreSQL for concurrency/query plans; Node test runner and TypeScript build for the worker; source/contract checks; Playwright on Chromium and WebKit; Docker health and real-provider acceptance
**Target Platform**: Existing Linux Docker Compose deployment and three-node production topology; Admin UI is desktop-first but supports 375px through 1440px
**Project Type**: Multi-surface web application with API, background AI worker, relational database, queue, realtime events, and private files
**Performance Goals**: A visible progress state within 2 seconds for 95% of ordinary questions; a complete answer or explicit next state within 10 seconds for ordinary questions; deterministic financial/numerical results; no unbounded reads; no duplicate logical effects
**Constraints**: Built-in `Admin` role only; authoritative PostgreSQL role check at every trust boundary; transcript ownership; no raw SQL or generic CRUD; no model-selected route/type; no prohibited secret in provider/transcript/audit/log/realtime/export; every mutation waits for a durable confirmation; strong phrase for destructive/financial/permission/security/credential/account-disable/bulk actions; safe restart/retry behavior
**Scale/Scope**: All business domains reachable from the current Admin Shell and all current Admin state-changing workflows. Current source includes hundreds of endpoints and mutations, but exact acceptance counts are intentionally derived from the runtime endpoint graph and reachable frontend graph at implementation baseline rather than copied from the stale regex inventory.

## Constitution Check

### Pre-research gate

| Gate | Decision and evidence required |
|---|---|
| Backend layering | New contracts, commands, queries, validators, and interfaces live under `Application/Features/AdminAI`; entities/enums live in `Domain`; EF configurations, projections, protection, orchestration, and adapters live in `Infrastructure`; controllers/hub/outbox wiring remain in `API`. Controllers are not called from capabilities. |
| Worker-only AI | The Node worker is the only `@google/genai` caller. .NET owns data and effects; the worker has no PostgreSQL access for AdminAI. Manual SDK function calling is used; automatic/MCP execution is forbidden. |
| Frontend boundaries | A new `/admin/ai-agent` route and `frontend/src/features/admin-ai-agent/` module use the persistent Admin Shell. Human internal chat and student live-support modules, stores, tables, routes, and hubs are not reused. |
| Database safety | One additive migration introduces only AdminAI tables, constraints, and indexes. No existing data is deleted or reinitialized; action/audit rows use restricted foreign keys and no cascade delete. |
| Redis/queue | A dedicated `ai-admin-agent-turns` BullMQ queue is fed through the durable Outbox using stable job IDs. Redis never becomes the source of transcript, proposal, confirmation, or execution truth. |
| Realtime | Reuse `/hubs/platform` and its `User_{adminId}` group. Events are safe notifications with event IDs and sequences; REST snapshots remain authoritative and reconcile gaps. No new chat hub is needed. |
| Security | `[Authorize(Roles = \"Admin\")]` is a first filter, followed by a PostgreSQL-backed `IAdminAIAccessGate` at admission, every read, proposal, confirmation/cancel, secure continuation, and final execution. JWT/cache state alone is insufficient. |
| Tests first | Capability inventory, redaction sentinel, no-side-effect proposal, closed decision schema, ownership, reauthorization, idempotency, stale-state, finance, bulk, queue recovery, and route-permission tests are written before their production slice. |
| Manual QA | The owner must test Admin/non-Admin access, reads across all capability families, ordinary and strong confirmation, cancellation/staleness/retry, audit/privacy, and responsive/accessibility states. |
| Docker gate | `docker compose config -q`, additive migration on existing data, full stack health, queue/realtime recovery, representative real-provider reads/actions, and no-volume-reset validation are mandatory. |
| Phase transition | A failed automated, Docker, capability-coverage, secret, finance, or manual gate blocks the next implementation wave. An owner-approved risk must name the exact failed gate and containment; it cannot be silently treated as pass. |

### Post-design re-check

- The data model is additive and isolates AdminAI from all live-support entities.
- Every model output is a closed, versioned union and is validated on both worker and backend.
- Reads are typed projections with field allowlists; business text is untrusted data and cannot alter policy.
- Proposals are server-built and bind actor, capability/baseline/policy versions, target, normalized payload hash, authoritative state fingerprint, expiry, and confirmation mode.
- Executions use the initiating Admin identity and a durable unique execution ledger. No “system Admin” or first-Admin fallback is permitted.
- Financial totals and all other deterministic calculations are produced by backend capabilities, not inferred by the model.
- Original Admin screens remain the manual authority and fallback.
- Design follows the current `PRODUCT.md`, `DESIGN.md`, and `--admin-*` tokens. The older constitution palette/typography text conflicts with those active sources; the documented exception is recorded under Complexity Tracking rather than introducing a third aesthetic.

## Phase 0: Research Decisions

The completed dependency, security, provider, capability-inventory, confirmation, idempotency, realtime, UI, rollout, and alternative analysis is recorded in [research.md](./research.md). It resolves every technical unknown needed for design without expanding the approved product scope.

## Phase 1: Design Outputs

The additive relational design and race invariants are in [data-model.md](./data-model.md). Public/internal API, worker tools, capability baseline, action lifecycle, realtime, sensitive-data, and UI contracts are under [contracts/](./contracts/). Verification, Docker, provider, manual QA, disable, and rollback instructions are in [quickstart.md](./quickstart.md).

## Architecture

### Trust boundaries

```text
Admin browser
  -> AdminAI REST API (Admin role + authoritative access/ownership gate)
      -> PostgreSQL (conversation, turn, evidence, proposal, execution truth)
      -> bounded read capability adapters -> authoritative queries/services
      -> action capability adapters -> authoritative MediatR/services
      -> Outbox -> BullMQ ai-admin-agent-turns
      -> PlatformHub User_{adminId} safe notifications

BullMQ worker
  -> internal-token claim/tool/complete APIs
  -> Gemini function-calling loop (closed schemas, redacted data only)
  -> never database, controller, or business-action execution
```

### Turn and tool loop

1. The API creates the owner message, `AdminAITurn`, first `AdminAITurnStep`, and `AdminAITurnQueued` Outbox event in one transaction.
2. A stable BullMQ job claims the turn using `AI_CALLBACK_SECRET`; the backend verifies the lease, deadline, actor access, capability baseline, sensitive-data policy, and cancellation state.
3. The worker sends bounded conversation context and only the tool declarations valid for the turn to Gemini. Retrieved business text is explicitly marked untrusted.
4. The model must return one closed decision: `answer`, `clarify`, `request_reads`, `propose_actions`, or `refuse`.
5. For `request_reads`, the worker submits a bounded list of named calls to the internal tool gateway. The backend reauthorizes the owner, validates schemas and budgets, executes projections, redacts before returning, and persists safe evidence/digests. The worker sends function responses back to Gemini.
6. The loop is durable and bounded: maximum 3 model steps, 6 read invocations per turn, 4 read calls in one step, 64 KiB total redacted tool payload, 200 safe records per invocation unless a stricter capability limit applies, 5 seconds per read, and a 30-second ordinary provider deadline. Config may lower these bounds; raising them requires reviewed tests.
7. `propose_actions` is advisory only. The backend independently validates the named catalog items and constructs one server-owned proposal per independent action. Unknown or mismatched actions fail closed.
8. A terminal answer is persisted before a safe realtime notification is emitted. The UI obtains authoritative state from its snapshot.

### Capability baseline and drift gate

- A runtime ASP.NET integration inventory reads `EndpointDataSource`, controller action descriptors, auth/role/permission metadata, and resolved route templates; regex is not the authority.
- A TypeScript AST/import-graph inventory starts at reachable Admin routes/navigation and follows components/services/API calls. Dynamic calls require an explicit contract.
- A manually reviewed semantic manifest classifies `Read`, `Preview`, `Export`, `Mutation`, `ExternalSideEffect`, `SecureContinuation`, or `Excluded` and records domain, source workflow, authoritative handler/service, schema, redaction, limits, risk, confirmation, idempotency, concurrency, transaction, bulk semantics, audit, and refresh scopes.
- The baseline is the union of the runtime backend inventory and reachable Admin frontend graph. Internal/e2e/deployment/infrastructure/self-service-only routes can be excluded only with a durable reason.
- Bidirectional tests require exactly one manifest disposition for every baseline item and a live source for every capability. An endpoint, nav, service, or handler drift fails CI and release.
- HTTP verbs do not determine effects. GETs with side effects are mutations; POST previews/exports may be reads.
- Direct-controller `DbContext` mutations must first be extracted into authoritative commands/services. They cannot be wrapped by a generic route invoker.

### Read model and secret policy

- Read capabilities return explicit DTOs, never EF entities or arbitrary serialized rows.
- Each capability defines allowed filters/sorts, row/field limits, `dataAsOf`, completeness/truncation, deterministic calculations, and safe drill-down references.
- Field-level allowlists are primary. A prohibited-field/type denylist and seeded sentinel tests provide defense in depth.
- Permanently prohibited categories include password/hash, access/refresh/session tokens, encryption/service/connection secrets, device/session fingerprints, verification codes/answers, protected key material, and equivalent future fields.
- Sensitive legitimate PII/HR/payroll/payment data is returned only when relevant to the explicit question and is minimized before provider context.
- Raw provider prompts, hidden instructions, reasoning traces, raw tool results, secure inputs, and unrestricted audit `OldValues/NewValues` never enter visible transcripts or ordinary logs.

### Proposal, confirmation, and execution

- Ordinary actions require an explicit proposal-specific button.
- High-risk actions require a server-generated phrase such as `أؤكد تنفيذ <safe action> — <8-char challenge>`. The server applies NFC normalization, trims leading/trailing whitespace, and collapses whitespace runs only; it does not lowercase, alter punctuation, substitute digits, or accept fuzzy matches. Only an HMAC digest is persisted.
- Default proposal TTL is 5 minutes. Configuration may reduce it or raise it only within 60–900 seconds; a capability may choose a shorter TTL. Role/security/capability/policy/target/state changes invalidate immediately.
- Secure values and files are collected by a short-lived secure continuation outside the composer, transcript, model context, client cache, realtime payload, and audit values. The proposal holds only an opaque, actor-bound grant ID.
- Confirmation acquires the durable proposal/execution lock, rechecks PostgreSQL Admin status and conversation ownership, recalculates the authoritative state fingerprint and bulk membership, verifies the exact payload/challenge digest, then invokes the authoritative adapter.
- `AdminAIActionExecution` has one row per proposal and a unique actor/idempotency identity plus payload hash. Compatible retries return the recorded result; the same key with a different payload is rejected.
- External and financial operations are catalogued only after their underlying command supports durable idempotency and authoritative result recovery. Success is shown only from the terminal execution record.

### Realtime and client state

- REST snapshot/list endpoints are authoritative. PlatformHub notifications contain `schemaVersion`, `eventId`, owner-safe `conversationId`, monotonic `sequence`, event type, and time; no transcript, proposal payload, secret, or detailed PII is broadcast.
- The frontend deduplicates event IDs and applies only the next sequence. A gap, reconnect, tab resume, or unknown event triggers a snapshot refetch.
- Query cache owns conversation lists/snapshots. A small feature store holds selected conversation, responsive view, in-memory drafts, connection state, last sequence, and in-flight intent IDs. Transcripts, proposals, and secrets are never persisted in browser local storage.
- Execution results return allowlisted refresh scopes. Existing cache invalidation maps update affected Admin screens without a global reload.

## Project Structure

### Documentation for this feature

```text
specs/169-admin-ai-agent/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/requirements.md
├── contracts/
│   ├── admin-ai-api.yaml
│   ├── worker-tool-protocol.md
│   ├── capability-baseline.md
│   ├── action-state-machine.md
│   ├── realtime-events.md
│   ├── sensitive-data-policy.md
│   └── ui-contract.md
└── tasks.md
```

### Planned source structure

```text
backend/src/
├── NaderGorge.Domain/
│   ├── Entities/AdminAI/
│   └── Enums/AdminAIEnums.cs
├── NaderGorge.Application/Features/AdminAI/
│   ├── Catalog/
│   ├── Commands/
│   ├── Queries/
│   ├── Dtos/
│   ├── Interfaces/
│   ├── Security/
│   └── Validation/
├── NaderGorge.Infrastructure/
│   ├── Data/Configurations/AdminAI/
│   ├── Services/AdminAI/
│   │   ├── Reads/
│   │   └── Actions/
│   └── Migrations/<timestamp>_AddAdminAIAgent.cs
└── NaderGorge.API/
    ├── Controllers/AdminAIAgentController.cs
    ├── Controllers/AdminAIInternalController.cs
    ├── BackgroundServices/AdminAIRecoveryBackgroundService.cs
    ├── BackgroundServices/OutboxProcessorBackgroundService.cs
    ├── Configuration/RateLimitingConfig.cs
    └── Program.cs

backend/tests/
├── NaderGorge.Application.Tests/AdminAI/
└── NaderGorge.Integration.Tests/AdminAI/

worker/src/
├── jobs/processAdminAITurn.ts
├── services/adminAIAgent.ts
├── services/adminAIDecisionSchema.ts
├── services/adminAICallbackClient.ts
└── services/adminAITelemetry.ts

frontend/src/
├── app/admin/ai-agent/
├── features/admin-ai-agent/
├── services/admin-ai-agent-contract.ts
├── services/admin-ai-agent-service.ts
├── hooks/useAdminAiAgentEvents.ts
└── lib/admin-ai-agent-client-contract.ts

frontend/tests/e2e/admin-ai-agent.spec.ts
scripts/generate-admin-ai-capability-baseline.mjs
tests/admin_ai_capability_baseline.json
tests/admin_ai_capability_baseline.md
tests/test_admin_ai_capability_inventory.py
```

**Structure Decision**: Extend the repository's existing backend/frontend/worker boundaries. AdminAI receives its own domain namespace, tables, contracts, queue, worker processor, and frontend feature module, while reusing shared authentication, Outbox, PlatformHub, cache invalidation, admin tokens, and authoritative application services. No fourth runtime, vector store, generic tool server, or live-support coupling is introduced.

## Planned Delivery Waves

1. **Baseline and safety scaffolding**: fix runtime/reachable inventory, freeze manifest/policy versions, create drift and secret-sentinel gates, and identify direct-controller logic/idempotency gaps.
2. **Durable foundation**: additive schema, access gate, encryption/HMAC, Outbox queue, recovery, internal protocol, closed worker decisions, realtime envelopes, and observability.
3. **Grounded read-only agent**: capability projections for every baseline domain, deterministic calculations, tool budgets, Arabic answers/evidence, conversation history, and Admin-only UI.
4. **Ordinary actions**: extract authoritative application services where needed, build preview/proposal adapters, ordinary confirmation, execution ledger, refresh scopes, and parity tests.
5. **High-risk, bulk, secure, external, and financial actions**: strong challenge, secure continuation, membership fingerprints, finance precision/period controls, external idempotency/result recovery, and partial-outcome rendering.
6. **Complete coverage closure**: reconcile every baseline item, remove all temporary unsupported dispositions for current Admin business operations, run generated per-capability matrices, and prove 100% bidirectional coverage.
7. **Hardening and acceptance**: resilience, role revocation, concurrency/restart, performance/query plans, accessibility/responsive, real provider, Docker, manual owner review, and go/no-go evidence.

The implementation may land internally in these waves, but the requested first release cannot claim completion until Wave 6 and every final gate pass.

## Phase Closure & Verification Plan

**Focused automated gates**:

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter 'FullyQualifiedName~AdminAI'
ConnectionStrings__DefaultConnection='<test-postgres>' dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter 'FullyQualifiedName~AdminAI'
npm --prefix worker test
npm --prefix worker run build
npm --prefix frontend run check:route-permissions
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend run build
node scripts/generate-endpoint-inventory.mjs --check
node scripts/generate-admin-ai-capability-baseline.mjs --check
python3 -m pytest -q tests/test_endpoint_inventory.py tests/test_admin_ai_capability_inventory.py tests/test_admin_ai_agent.py
cd frontend && npx playwright test tests/e2e/admin-ai-agent.spec.ts tests/e2e/route-permission-parity.spec.ts --project=chromium --project=webkit
```

**Full repository gate**:

```bash
make verify
git diff --check
```

**Docker gate**:

```bash
docker compose config -q
make up
make migrate
make ps
curl -f http://localhost:5245/health
curl -f http://localhost:3001/health
curl -f http://localhost:3001/ready
curl -f http://localhost:8740
```

The migration gate must run both on a clean test database and a representative copy with existing records. No command may delete named volumes or reinitialize existing data. Full AI acceptance uses the configured production-equivalent provider; mock-only success is reported as incomplete.

**Required critical-path evidence**:

- Admin allowed; every non-Admin denied in nav, direct route, REST, internal misuse, and reconnect.
- Role/account/security-version revocation during turn and confirmation closes access and invalidates proposals.
- Owner transcript private from other Admins; redacted action evidence remains audit-visible under existing audit authorization.
- Every baseline read/action has exactly one manifest disposition; no orphan, duplicate, stale, unknown, or generic mapping.
- Prohibited seeded secrets are absent from provider request, response, transcript, proposal, DB evidence, audit, log, metric, trace, realtime, and export.
- Prompt injection in user and stored data cannot select unknown tools, reveal hidden context, alter confirmation, or claim an action succeeded.
- Proposal preview creates no database/external/queue/file effect other than AdminAI workflow evidence.
- Cancel, expiry, wrong phrase, stale state, access loss, duplicate request, payload conflict, two tabs, two Admins, callback retry, process restart, and partial failure produce the specified zero-or-one outcome.
- Original and agent paths have validation, authorization, accounting, notification, transaction, concurrency, and audit parity.
- 375/768/1024/1440 widths, 200% zoom, keyboard, screen reader announcements, mixed RTL/LTR, light/dark, reduced motion, long content, and no document horizontal scroll.

**Manual QA required from owner**:

1. Ask representative Arabic record, aggregate, cross-domain, empty, ambiguous, and large-result questions from every business family and compare evidence to original screens.
2. Propose/cancel/expire/stale/confirm ordinary actions from every family and compare with the original workflow.
3. Execute representative financial, destructive, permissions, security, credential, account-disable, external, attachment, and bulk flows using strong confirmation and secure continuation.
4. Test another Admin's transcript, every non-Admin role, injected text, prohibited secrets, duplicate confirmation, reconnect, and role removal.
5. Complete the experience by keyboard at all required widths/themes and verify focus, announcements, no color-only state, and readable mixed-direction data.

**End-of-phase report format**: scope completed; baseline version/hash and coverage counts; migrations; commands and exact results; Docker/service health; provider/model tested; manual checklist; capability exceptions (must be zero for current Admin business operations at release); known risks; rollback/feature-disable evidence; explicit go/no-go.

## Implementation Safety in the Current Workspace

The worktree already contains owner changes in Admin, HR, live-support, reporting, services, tests, and navigation files. A future implementation must re-read and integrate those changes; it must not replace them with planned versions. In particular, `AdminController.cs`, HR controllers/services, `LiveSupportAdminController.cs`, `AdminShellChrome.tsx`, `navigation.tsx`, and related untracked HR/live-support files are active owner work. This planning run modifies only Spec Kit metadata and documentation.

## Complexity Tracking

| Constitution tension | Why needed | Simpler alternative rejected because |
|---|---|---|
| The confirmed first release covers every current Admin business operation rather than a small MVP subset. | Complete v1 coverage was explicitly selected by the owner and is made measurable by a frozen baseline and release gate. | A read-only or limited action catalog would be safer and faster but would contradict the approved scope. Delivery is therefore internally waved, with fail-closed capabilities and no completion claim before 100% coverage. |
| The current `PRODUCT.md`, `DESIGN.md`, live `--admin-*` tokens, Tajawal/Montserrat, navy/teal palette, and recent application direction supersede the legacy gold/cream/glass design paragraph in Constitution Principle VIII. | Matching the shipped Admin Shell prevents a feature-specific visual regression and follows the repository's current source-of-truth design files. | Reintroducing the legacy palette/glass treatment solely for this feature would create two Admin design systems and violate current brand guidance. Accessibility, RTL, shared-component, performance, and no-clutter parts of the constitution remain enforced. |
