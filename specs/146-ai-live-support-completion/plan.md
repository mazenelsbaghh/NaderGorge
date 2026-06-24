# Implementation Plan: AI Live Support Production Completion

**Branch**: `146-ai-live-support-completion` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)  
**Input**: Clarified specification from `specs/146-ai-live-support-completion/spec.md`

## Summary

Complete the existing live-support and AI-support implementation as one production workflow without deleting or reinitializing current data. Preserve PostgreSQL as the authority for conversations, policy, knowledge, turns, confirmations, verification, assignment, actions, and audit. Replace direct database-to-queue dual writes and the 2,000-line `LiveSupportService` AI block with bounded Application ports and Infrastructure services. The Node worker remains the only Google GenAI caller, returns a closed validated decision union, and never executes business actions. Participant, staff, and admin interfaces are decomposed into task-focused modules using the existing Massar navy/teal design system, Arabic-first RTL behavior, accessible state feedback, and responsive role-specific layouts.

## Technical Context

**Language/Version**: C# 13 on .NET 9; TypeScript 5.9 strict on Node.js 20; Next.js 16.2.7 and React 19.2.4  
**Primary Dependencies**: ASP.NET Core, MediatR, FluentValidation, EF Core 9.0.6, Npgsql 9.0.4, SignalR 9.0.6 with Redis backplane, StackExchange.Redis 2.12.4, BullMQ 5.71.1, `@google/genai` 1.47.0, Axios, Zustand, Tailwind CSS, Lucide React  
**Storage**: PostgreSQL authoritative state and append-only evidence; Redis queue delivery, recovery hints, routing locks, and SignalR backplane; no vector database  
**Testing**: xUnit unit/application tests, real-PostgreSQL integration and concurrency tests, Node built-in worker tests, TypeScript/ESLint, Playwright Chromium and WebKit, Docker runtime smoke, real configured-provider E2E  
**Target Platform**: Existing Linux Docker deployment; Arabic-first mobile participant UI and tablet/desktop operations UI  
**Project Type**: Multi-service web application with ASP.NET API, Node worker, Next.js frontend, PostgreSQL, and Redis  
**Performance Goals**: 95% of normal connected messages reach a reply or pending-decision state within 10 seconds; queue admission under 2 seconds at expected load; SignalR state visible within 2 seconds; emergency disable admission gate under 5 seconds; participant interactions remain responsive at 320px  
**Constraints**: No destructive migration; no direct Gemini call outside worker; no model-owned execution; no protected values in transcript/provider/log/audit; no AI output after irreversible handoff; exactly-once logical effects under retry; policy preview has zero business writes; current public routes remain compatible  
**Scale/Scope**: Existing platform population and all live-support records; seven user stories; four roles; bounded transcript/context/knowledge payloads; cursor-based large histories; worker concurrency configured independently for live support

## Constitution Check

| Gate | Plan evidence | Status |
|---|---|---|
| Modular Clean Architecture | Domain state remains framework-free; Application owns ports/contracts; Infrastructure owns EF/Redis/provider adapters; API owns transport; frontend uses service/hook/component boundaries | PASS |
| Provider abstraction first | Worker extends existing `AIProviderGateway` operation set; .NET and browser never import or call Google SDK | PASS |
| Security and access by default | Admin-only policy, participant ownership, staff ownership, allowlisted context, internal-token callbacks, redaction, rate limits, confirmation, idempotency, audit | PASS |
| Academic and business integrity | AI and staff adapters invoke existing authoritative MediatR/business services and revalidate current student state | PASS |
| UX simplicity and brand consistency | Existing Massar tokens, Tajawal, RTL, 44px participant targets, explicit next state, standard controls, no decorative redesign | PASS |
| Observability and readiness | Safe metrics, correlation IDs, turn/queue/recovery status, dead-letter visibility, health/readiness, Docker and provider gates | PASS |
| Phase verification | Each implementation slice closes with targeted tests; failed gates become tracked tasks and block the next slice | PASS |
| Data preservation | Additive migration, restrictive history relationships, compatibility reads, seeded upgrade test, documented rollback order | PASS |

Post-design check: all gates remain satisfied. No constitution exception is required.

## Project Structure

### Documentation

```text
specs/146-ai-live-support-completion/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── live-support-completion-api.yaml
│   ├── ai-worker-contract.md
│   ├── state-machine.md
│   └── ui-contract.md
└── tasks.md
```

### Source Code

```text
backend/src/NaderGorge.Domain/
├── Entities/LiveSupport/
└── Enums/LiveSupportAIEnums.cs

backend/src/NaderGorge.Application/Features/LiveSupportAI/
├── Commands/
├── Queries/
├── Dtos/
├── Interfaces/
├── Services/
└── Validation/

backend/src/NaderGorge.Infrastructure/
├── Data/AppDbContext.cs
├── Migrations/
├── Background/RedisJobEnqueuer.cs
└── Services/LiveSupportAI/
    ├── LiveSupportAITurnOrchestrator.cs
    ├── LiveSupportAIContextBuilder.cs
    ├── LiveSupportAIKnowledgeService.cs
    ├── LiveSupportAIActionExecutor.cs
    ├── LiveSupportAIVerificationService.cs
    ├── LiveSupportAIRegistrationService.cs
    ├── LiveSupportAIHandoffService.cs
    └── LiveSupportAIRecoveryService.cs

backend/src/NaderGorge.API/
├── Controllers/LiveSupportParticipantController.cs
├── Controllers/LiveSupportAIAdminController.cs
├── Controllers/InternalController.cs
├── BackgroundServices/OutboxProcessorBackgroundService.cs
├── BackgroundServices/LiveSupportAIRecoveryBackgroundService.cs
├── Configuration/RateLimitingConfiguration.cs
└── Hubs/LiveSupportHub.cs

worker/src/
├── jobs/processLiveSupportTurn.ts
├── services/liveSupportAgent.ts
├── services/liveSupportDecisionSchema.ts
├── services/liveSupportCallbackClient.ts
├── services/liveSupportTelemetry.ts
├── services/aiProvider.ts
├── services/geminiService.ts
└── index.ts

frontend/src/
├── app/admin/live-support/ai/
├── app/admin/live-support/
├── app/assistant/live-support/
├── components/live-support/ai-admin/
├── components/live-support/participant/
├── components/live-support/staff/
├── components/live-support/admin/
├── hooks/useLiveSupportHub.ts
├── stores/live-support-store.ts
├── services/live-support-ai-service.ts
└── services/live-support-service.ts

backend/tests/NaderGorge.Application.Tests/LiveSupportAI/
backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/
worker/src/**/*.test.ts
frontend/tests/e2e/live-support-ai.spec.ts
frontend/tests/e2e/live-support.spec.ts
```

**Structure Decision**: Keep the existing three-service architecture and current URLs. Extract AI behavior from `LiveSupportService.cs` behind Application interfaces rather than introduce a fourth service or duplicate student business rules. Existing non-AI routing methods remain in `LiveSupportService`; new AI modules collaborate through explicit ports and transactions.

## Phase 0: Research Decisions

All decisions and rejected alternatives are recorded in [research.md](./research.md). No planning ambiguity remains. The central rules are: PostgreSQL owns state, outbox owns queue delivery, backend owns authorization and execution, worker owns provider inference and schema validation, and irreversible handoff defeats late callbacks.

## Phase 1: Design and Contracts

- [data-model.md](./data-model.md) specifies compatibility-preserving entity changes, indexes, constraints, lifecycle transitions, and migration checks.
- [contracts/live-support-completion-api.yaml](./contracts/live-support-completion-api.yaml) specifies participant, admin, and internal completion endpoints and safe errors.
- [contracts/ai-worker-contract.md](./contracts/ai-worker-contract.md) specifies deterministic jobs, claim/decision/callback boundaries, deadlines, and redaction.
- [contracts/state-machine.md](./contracts/state-machine.md) specifies conversation, turn, pending decision, verification, and handoff race precedence.
- [contracts/ui-contract.md](./contracts/ui-contract.md) specifies participant, staff, and admin responsive/accessibility states.
- [quickstart.md](./quickstart.md) specifies baseline, migration, automated, Docker, browser, and real-provider verification.

## Implementation Design

### 1. Establish a truthful baseline and contract parity

- Add contract-parity tests for C# enums/DTOs, TypeScript unions, worker decision schema, catalog keys, endpoint routes, and SignalR event names.
- Preserve the completed human-support behavior from Feature 142 while treating unchecked Feature 143 tasks as unimplemented until verified by code and tests.
- Fix the integration-test EF Core package mismatch and run backend build/test commands sequentially to avoid shared `obj` file locking.

### 2. Separate AI orchestration from the live-support facade

- Keep participant/staff/routing facade signatures compatible, but delegate AI admission, turn creation, claim, completion, failure, pending decisions, verification, registration, and handoff to dedicated interfaces.
- Move EF-heavy logic out of the Application layer and prevent controller methods from coordinating multi-step business transitions.
- Split `LiveSupportService.cs` at method boundaries, not with a wholesale rewrite, to reduce migration risk.

### 3. Make message-to-turn delivery atomic

- Participant message, event, conversation version, AI activity, turn, and `LiveSupportAITurnQueued` outbox record commit once.
- Extend `OutboxProcessorBackgroundService` to dispatch queue events separately from SignalR events. Mark processed only after Redis accepts the deterministic `turn:{turnId}` job.
- Change `RedisJobEnqueuer` to reject unknown queue names, preserve deterministic live-support job IDs, and never silently route unknown work as notifications.

### 4. Enforce a closed provider boundary

- Build bounded context in the backend from policy allowlists, ranked published knowledge, safe student projections, and a limited transcript window plus summary.
- Worker accepts only the versioned claim contract and uses existing Vertex-primary/Developer-fallback abstraction for `live-support`.
- Parse a closed union: `reply`, `propose_action`, `request_verification`, `propose_account_creation`, `request_resolution`, `handoff`. Reject extra/unknown fields, excessive strings, missing branch payloads, and invalid action keys.
- Apply a provider deadline, one inference retry only for classified transient failure, and independent callback-delivery retries so a callback network failure never repeats inference.

### 5. Revalidate every decision in the backend

- Internal callback verifies internal token, body size, schema version, callback idempotency key, turn status/version, policy version, conversation mode/version, participant link, catalog membership, and decision branch payload.
- Late callback after disable, close, abandon, transfer, or handoff becomes a recorded discarded turn and cannot create a message or proposal.
- Safe provider/model/latency/token metadata is persisted; prompts and provider raw bodies are not.

### 6. Correct pending actions and account workflows

- Add an explicit pending-decision kind and nullable target student so guest handoff/account-creation proposals never use `Guid.Empty` as a foreign key.
- Encrypt execution payloads through an application encryption port; store only safe display JSON, hashes, expiry, state fingerprint, and deterministic idempotency identity in queryable form.
- Confirmation rechecks participant ownership, conversation mode, target link, active policy, action allowlist, expiry, payload hash, state fingerprint, and authoritative business validation.
- AI actions run through a dedicated system-actor adapter that invokes existing commands without pretending to be a checked-in human owner.
- Secure registration calls the authoritative registration/profile workflow in one orchestration boundary; it does not hand-build a partial profile or place password values in transcript/event payloads.

### 7. Close guest verification privacy gaps

- Exact-value lookup returns the same public response for no match, one match, and ambiguous matches; only the internal session records the safe outcome.
- Hash lookup values with a keyed digest, never a plain unsalted hash. Compare submitted answers in memory and persist question keys and outcome codes only.
- Bind one active verification to conversation, policy, candidate, expiry, and attempt sequence. Multiple matches fail closed. Exhaustion triggers the same irreversible handoff service.

### 8. Centralize handoff, disablement, and recovery

- One serializable/advisory-locked handoff operation changes AI mode, invalidates pending turns/decisions/verification, creates at most one active queue entry, records reason/summary/audit/outbox, and invokes human assignment.
- Emergency disable blocks new AI admission first, then reconciles AI-active conversations and queued/in-flight work with the handoff service.
- Recovery scans bounded indexed batches for stale queued/processing turns, expired pending decisions, expired verification, inactivity warnings, auto-close candidates, and undelivered callbacks/outbox events.
- Precedence: terminal conversation > human handoff > emergency disable > confirmed business effect > pending AI callback. Losing operations record a safe reason.

### 9. Restore real-time and snapshot coherence

- Publish typed outbox events for message, AI typing/turn state, proposal, verification, registration, handoff, queue, assignment, ownership loss, disablement, and closure.
- `useLiveSupportHub` deduplicates by sequence, requests a snapshot after reconnect/gap, and never re-enables AI composer or action cards after a terminal/human mode snapshot.
- REST snapshot remains authoritative for initial load and recovery; SignalR only advances state.

### 10. Rebuild participant UI around explicit states

- Decompose `ParticipantConversation` and `LiveSupportWidget` into message list, AI status, composer, pending-decision region, verification, secure registration, handoff, queue, closed/rating, and recoverable error modules.
- Use Massar deep navy as authority, teal for active/progress state, gold only for achievement, Tajawal, moderate 12–16px radii, consistent Lucide icons, and no decorative dark-dashboard or glass treatment.
- At 320px, cards and forms use one column, 44px controls, safe-area spacing, no horizontal overflow, stable reserved state regions, visible keyboard focus, `aria-live`, and reduced-motion alternatives.

### 11. Rebuild staff and admin surfaces for operational work

- Split `AssistantLiveSupportPageClient` into staff status, queue, conversation header/transcript/composer, handoff summary, and lazy student-context modules with explicit ownership loss.
- Split `AdminAISupportPageClient` into overview/emergency control, policy editor, knowledge manager, allowlist/action selector, verification rules, zero-write preview, activity evidence, statistics, and active-conversation investigation.
- Use compact product density, standard tabs/forms/tables, sticky controls only where they do not cover content, skeleton loading, useful empty states, inline recoverable errors, and no nested card grid.

### 12. Prove data safety, behavior, and real provider operation

- Apply the additive migration to seeded pre-146 PostgreSQL data and compare counts, IDs, transcripts, policy versions, links, actions, and audit evidence before and after.
- Add unit, integration, concurrency, worker, frontend component/contract, and Playwright coverage for all seven stories and negative paths.
- Run one real-provider conversation through Docker from participant message to durable AI reply; record provider/model, safe correlation, duration, and resulting transcript without recording credentials or raw prompt.

## Rollout and Recovery

1. Back up PostgreSQL and verify restore procedure.
2. Deploy additive migration while AI remains disabled; run compatibility/read probes.
3. Deploy backend, worker, and frontend; verify queue, internal callback, health, readiness, SignalR, and admin preview.
4. Publish a narrow knowledge-only policy to test accounts and run the real-provider smoke.
5. Enable limited actions only after audit and concurrency evidence passes.
6. Emergency rollback order: disable AI, stop new queue admission, drain or mark queued work, retain all database records, roll back application images only. Do not drop Feature 143/146 tables or migrations.

## Phase Closure and Verification Plan

**Automated Tests Required**:

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~LiveSupport"`
- `ConnectionStrings__DefaultConnection="Host=localhost;Port=5435;Database=massar_platform_test;Username=postgres;Password=postgres" dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter "FullyQualifiedName~LiveSupport"`
- `npm --prefix worker test`
- `npm --prefix frontend run lint`
- `(cd frontend && npx tsc --noEmit)`
- `npm --prefix frontend run build`
- `(cd frontend && npx playwright test tests/e2e/live-support.spec.ts tests/e2e/live-support-ai.spec.ts --project=chromium --project=webkit)`
- `dotnet build backend/NaderGorge.sln`

Critical coverage includes policy publication/version conflict, allowlisted context, prompt injection, exact-value privacy, secure registration, message/turn/outbox idempotency, worker schema/deadline/callback retry, action expiry/revocation/concurrency, handoff/disable/callback races, recovery, queue/ownership/capacity, timeline redaction, reconnect snapshots, mobile RTL, keyboard operation, and regression of human-only support.

**Docker Gate Required**:

- `docker compose config -q`
- `make migrate && make up && make ps`
- Backend `/api/health`, worker `/health` and `/ready`, PostgreSQL and Redis health, frontend public/student/assistant/admin surfaces, SignalR negotiate, deterministic queue ingestion, internal callback authorization, restart recovery, and one real-provider participant reply.

**Manual QA Required**:

- Student: real reply, allowed action confirm/cancel, reconnect, confirmed handoff, human conversation, close, rating.
- Guest: general help, privacy-negative lookup, successful and exhausted verification, secure account creation, no secret leakage.
- Staff: eligibility, queue assignment, safe AI handoff context, student context/actions, transfer, ownership denial, close.
- Admin: draft/version conflict, knowledge, preview zero-write, enable/disable, statistics, evidence/timeline, intervention, non-admin denial.
- Responsive/a11y: 320px participant, tablet/desktop operations, keyboard-only, reduced motion, Arabic/English long content.

**End-of-Phase Report Format**: Implemented scope and exact files; migration/data-preservation evidence; commands and pass/fail counts; real-provider evidence; Docker/health/restart evidence; role-by-role manual checklist; security/redaction evidence; performance observations; unresolved external blockers; explicit go/no-go. No implementation phase advances with an unresolved feature-introduced failure.

## Complexity Tracking

No constitution violation. The additive state fields, dedicated queue semantics, and extracted AI services are required to enforce data preservation, exactly-once effects, privacy, and irreversible handoff. A smaller patch inside the existing facade was rejected because the current AI implementation already combines routing, provider context, verification, account creation, and business execution in one 2,000-line class.
