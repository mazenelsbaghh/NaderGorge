# Implementation Plan: AI Live Support Agent

**Branch**: `143-ai-live-support-agent` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)
**Input**: Clarified feature specification from `specs/143-ai-live-support-agent/spec.md`

## Summary

Extend the durable live-support command center with an AI-first conversation mode. PostgreSQL remains authoritative for published policy, knowledge, AI turns, verification, confirmations, audit, and irreversible handoff. The backend materializes only allowlisted context, creates durable turn work through an outbox, and alone validates or executes actions. A dedicated Node/BullMQ queue calls the existing Google GenAI provider abstraction and returns a strict decision union; it never reads the database freely or invokes business tools. Human handoff is a serializable, one-way transition that wins races and prevents later AI messages. Admin receives a modular AI tab for draft/publish policy, knowledge, catalogs, verification, dry-run preview, monitoring, and evidence.

## Technical Context

**Language/Version**: C# 13 on .NET 9; TypeScript 5.9 strict on Node.js 20; Next.js 16.2.7 and React 19.2.4  
**Primary Dependencies**: ASP.NET Core, MediatR, EF Core 9.0.6, Npgsql 9.0.4, SignalR 9.0.6, StackExchange.Redis 2.12.4, BullMQ 5.71.1, `@google/genai` 1.47.0, Axios, Zustand, Tailwind CSS, Lucide React  
**Storage**: PostgreSQL for authoritative AI policy/knowledge/turn/action/verification state; Redis for queue delivery, distributed locks, cancellation hints, and SignalR backplane; no vector database in this phase  
**Testing**: xUnit application/integration tests, Node built-in test runner for worker, ESLint/TypeScript, Playwright Chromium/WebKit, real PostgreSQL migration/concurrency gates, Docker health and manual four-role QA  
**Target Platform**: Existing Linux Docker deployment serving Arabic-first web surfaces and background worker  
**Project Type**: Multi-service web application with API, worker, public/student/admin/assistant frontend surfaces  
**Performance Goals**: 95% of normal AI text replies visible within 8 seconds; disable gate effective within 5 seconds; dedicated queue age under 2 seconds at target load  
**Constraints**: AI cannot execute tools directly; no post-handoff AI message; no raw verification answers/passwords/tokens in provider context, Redis, logs, or audit; preview has zero production writes; all state changes require participant confirmation and existing business validation  
**Scale/Scope**: Existing platform population and live-support traffic; initial worker concurrency configurable with bounded context and up to 50 published knowledge entries selected per policy, while retrieval sends only a small ranked subset per turn

## Constitution Check

| Gate | Plan evidence | Status |
|---|---|---|
| Modular Clean Architecture | Domain entities/enums, Application orchestration/ports, Infrastructure EF/queue/action adapters, API controllers/callbacks, worker model execution, frontend feature modules | PASS |
| Provider abstraction first | Reuse `AIProviderGateway`; add `live-support` operation and adapter behind worker service, never call Gemini from .NET/controllers | PASS |
| Security/access by default | Built-in Admin-only config, allowlisted context, structured decision validation, explicit confirmation, redaction, rate limits, scoped internal callbacks, immutable audit | PASS |
| Phased MVP discipline | Foundation/policy, reply, handoff, actions, guest verification/account creation, admin monitoring delivered as independently verified slices | PASS |
| Academic/business integrity | Existing MediatR commands and `LiveSupportActionService` business rules remain authoritative | PASS |
| Observability/readiness | Turn latency/outcome/provider/handoff metrics, safe errors, dead-letter visibility, Docker health and readiness gates | PASS |
| Phase verification | Every implementation phase closes with targeted automated tests, Docker gate, manual QA evidence, and no next phase before failures are fixed | PASS |

Post-design re-check: all gates remain satisfied. No justified constitution violation is required.

## Project Structure

### Documentation

```text
specs/143-ai-live-support-agent/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── ai-support-api.yaml
│   ├── ai-worker-contract.md
│   ├── ai-decision-schema.md
│   ├── ai-action-and-verification-catalog.md
│   ├── ai-support-hub.md
│   └── ui-contract.md
└── tasks.md
```

### Source Code

```text
backend/src/NaderGorge.Domain/
├── Entities/LiveSupportAI/
└── Enums/LiveSupportAI*.cs

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
├── Services/LiveSupportAI*.cs
└── Background/RedisJobEnqueuer.cs

backend/src/NaderGorge.API/
├── Controllers/LiveSupportAIAdminController.cs
├── Controllers/LiveSupportParticipantController.cs
├── Controllers/LiveSupportStaffController.cs
├── Controllers/InternalController.cs
├── BackgroundServices/LiveSupportAI*.cs
└── Hubs/LiveSupportHub.cs

worker/src/
├── jobs/processLiveSupportTurn.ts
├── services/liveSupportAgent.ts
├── services/liveSupportDecisionSchema.ts
├── services/aiProvider.ts
├── services/geminiService.ts
└── index.ts

frontend/src/
├── app/admin/live-support/ai/page.tsx
├── components/live-support/ai-admin/
├── components/live-support/participant/
├── components/live-support/staff/
├── services/live-support-ai-service.ts
└── services/live-support-service.ts

backend/tests/NaderGorge.Application.Tests/LiveSupportAI/
backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/
worker/src/**/*.test.ts
frontend/tests/e2e/live-support-ai.spec.ts
```

**Structure Decision**: Keep feature 143 inside the existing three-service architecture. Backend owns policy, context, state transitions, and business execution; worker owns provider invocation and schema parsing only; frontend is split into admin AI modules and participant/staff state components rather than enlarging existing page clients.

## Phase 0: Research Decisions

All dependency, integration, concurrency, security, retrieval, verification, provider, and UI decisions are resolved in [research.md](./research.md). No planning clarification remains. The key gate is that the model produces a structured proposal only; backend policy and business services remain authoritative.

## Phase 1: Design Artifacts

- [data-model.md](./data-model.md) defines policy/knowledge versions, AI conversation mode, turns, actions, verification, constraints, redaction, and transitions.
- [contracts/](./contracts/) defines participant/admin/internal APIs, worker queue/callback, strict decision schema, catalogs, hub events, and UI states.
- [quickstart.md](./quickstart.md) defines static, test, migration, Docker, manual, security, and rollback gates.

## Implementation Design

### 1. Durable AI admission and turn lifecycle

- Separate AI admission from human availability. A published enabled AI policy permits a conversation 24/7 even with no checked-in employee.
- Initial AI conversation creates `LiveSupportAIConversationState` in `AiActive`; it does not create a human queue entry or consume staff capacity.
- A participant text message transaction creates the durable message/event, one `LiveSupportAITurn` keyed uniquely by source message, and an `AISupportTurnQueued` outbox event.
- Outbox dispatch enqueues deterministic `turn:{turnId}` work to a dedicated `ai-live-support-turns` BullMQ queue. Unknown queue names fail closed instead of falling through to notifications.
- Worker fetches a one-time backend-built context packet by turn ID. Redis contains IDs and safe metadata only.

### 2. Provider and decision boundary

- Extend the installed provider gateway operation union with `live-support`; preserve Vertex-primary/Developer-fallback configuration.
- Dedicated worker concurrency, maximum queue age, 6-second provider deadline, one bounded transient retry, and separate retryable callback delivery target the 8-second product SLA.
- Use `responseMimeType: application/json` plus supported JSON schema. The decision union is `reply`, `propose_action`, `request_verification`, `propose_account_creation`, `request_resolution`, or `handoff`.
- Worker validates size and schema, records safe provider/model/latency/usage metadata, and never calls platform actions.
- Callback is internally authenticated and idempotent. Backend revalidates turn status, policy version, conversation mode/version, catalogs, and account link before persisting any participant-visible result.

### 3. Atomic handoff and resolution

- One serializable command with row/advisory locking performs `AiActive → HumanQueued/HumanAssigned`, invalidates pending turns/actions/verification, creates at most one active queue entry, records reason/event/audit, and publishes participant/staff/admin updates.
- Callback after handoff marks the turn discarded and cannot create an AI message. Handoff is irreversible for that conversation.
- When no staff is present, copy states that employees are unavailable and will contact the participant; the queue persists for the next shift.
- AI resolution closes only after participant confirmation, or after configured inactivity warning and grace expiration. Participant activity cancels the pending auto-close.

### 4. Context, knowledge, and policy publication

- Backend-owned stable catalogs enumerate readable sections, action keys, safe lookup fields, and verification fields. Unknown or removed keys invalidate publication.
- Draft policy is editable; publishing creates an immutable version and atomically selects one active version. Turns retain exact policy and knowledge revision IDs.
- Initial knowledge retrieval uses PostgreSQL normalized text search/ranking over published revisions with strict result and character limits. Vector storage is excluded until measured retrieval quality requires it.
- Context builder emits structured allowlisted fields only, labels user/knowledge/account text untrusted, bounds transcript/summary, and excludes secrets and raw verification answers.
- Dry-run preview calls the same context/decision validation in `DryRun` mode with synthetic inputs and blocks all message, account, action, queue, assignment, and participant audit writes.

### 5. Action confirmation and AI actor

- Add a backend `ILiveSupportAIActionExecutor` that reuses action metadata/business MediatR commands without impersonating a checked-in staff owner.
- `LiveSupportAIPendingAction` stores safe proposal, payload hash, linked account, policy version, state fingerprint, expiry, and deterministic idempotency key.
- Participant confirm/cancel endpoints own the transition. Confirm rechecks conversation mode, account, policy allowlist, payload/state fingerprint, expiry, and existing business rules before exactly-once execution.
- Password/account-secret fields use secure participant-owned inputs excluded from AI messages and provider context. AI may guide the validated account-creation proposal but does not generate or retain a password.

### 6. Guest lookup and verification

- Admin publishes safe complete-value lookup keys and non-secret verification questions. Lookup response is always generic and never returns candidates or existence signals.
- Verification session is bound to one conversation and one unexposed candidate. Multiple matches fail closed.
- Backend selects challenge questions; expected answers never reach the model. Submitted answers are normalized and compared in memory, then discarded. Persist only question keys, safe outcomes, policy version, and timestamps.
- Default maximum attempts is three; configurable correct threshold and maximum are validated at publication. Failure/exhaustion hands off permanently.

### 7. Admin and participant UI

- Dedicated `/admin/live-support/ai` route restricted to built-in Admin with tabs for overview, instructions/knowledge, data/actions, verification, preview, and activity/evidence.
- Use Massar navy/teal tokens, restrained density, 12–16px surface radii, clear draft/published state, standard controls, no decorative glass or card grids, keyboard-visible focus, 44px mobile actions, and reduced-motion-safe transitions.
- Participant UI shows an AI identity badge, thinking/retry state, structured confirmation cards, verification without hints, human request action, inactivity warning/cancel, and final queue state. It disables AI affordances permanently after handoff.
- Staff handoff panel shows safe summary, policy version, verification/link state, attempted actions/failures, and reason without raw answers or system instructions.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter 'FullyQualifiedName~LiveSupportAI|FullyQualifiedName~LiveSupport'`
- `dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter 'FullyQualifiedName~LiveSupportAI|FullyQualifiedName~LiveSupport'`
- `npm --prefix worker test`
- `npm --prefix frontend run lint`
- `(cd frontend && npx tsc --noEmit)`
- `npm --prefix frontend run build`
- `(cd frontend && npx playwright test tests/e2e/live-support-ai.spec.ts --project=chromium --project=webkit)`

Critical coverage: Admin-only publication; allowlist non-access; prompt injection; durable turn and callback idempotency; response/handoff/disable races; confirmation cancel/stale/revoked/exactly-once; guest non-disclosure/current-conversation-only/exhaustion; account-creation retry; inactivity warning/cancel/close; preview zero-write; worker schema/timeout/callback retry; mobile/RTL/a11y UI.

**Docker Gate Required**:

- `docker compose config -q`
- `make migrate && make up && make ps`
- Rebuild backend, worker, landing, student, admin, and assistant images.
- Verify backend `/api/health`, worker `/health` and `/ready`, all four frontend origins, Redis, PostgreSQL migration head, SignalR negotiate, AI queue ingestion, one configured-provider turn, and human handoff.

**Manual QA Required**: Admin draft/preview/publish/disable; authenticated student answer/action cancel-confirm/handoff; guest general support/new account/existing-account verification failure and success; staff handoff context; negative disabled context/action, prompt injection, stale confirmation, candidate disclosure, and post-handoff callback.

**End-of-Phase Report Format**: Implemented scope; exact commands and pass/fail counts; migration/Docker/health evidence; manual QA checklist; security/redaction evidence; unresolved external dependency; risks; explicit go/no-go. No phase advances with failed gates unless the owner accepts a documented external-only blocker.

## Rollout and Recovery

- Migration first, with AI policy disabled by default.
- Deploy backend/worker/frontend, publish a minimal knowledge-only policy, validate preview, then enable for internal/test accounts before public rollout.
- Begin with a narrow action allowlist; expand only after audit review.
- Emergency disable is Admin-only, effective within 5 seconds, hands off active work, and preserves all evidence.
- Rollback application code only after disabling AI and draining/cancelling queue work; do not remove referenced policy/turn/audit tables.

## Complexity Tracking

No constitution violation. New tables and a dedicated queue are necessary because policy history, confirmation, verification, and post-handoff race guarantees cannot be represented safely in transient Redis or existing staff-owned action records.
