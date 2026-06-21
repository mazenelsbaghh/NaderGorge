# Implementation Plan: Live Support Command Center

**Branch**: `142-live-support-command-center` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/142-live-support-command-center/spec.md`

## Summary

Add a dedicated live-support domain beside the existing internal staff chat. Authenticated students and guest visitors use a participant widget; checked-in support staff use a three-pane command center; admins use an operational dashboard and capacity/schedule configuration. PostgreSQL is the durable source for conversations, queue position, assignments, messages, links, ratings, actions, and events. Redis stores short-lived connection presence and continues as the SignalR backplane. Assignment is serialized in a PostgreSQL transaction so simultaneous arrivals, checkout, disconnect recovery, and closure cannot exceed capacity or create two owners. Sensitive student operations run through an explicit typed action catalog that validates current conversation ownership, linked student, idempotency key, confirmation input, existing business rules, and audit context.

## Technical Context

**Language/Version**: C# 13 on .NET 9 backend; TypeScript 5.x strict mode on Next.js 16.2.7 and React 19.2.4 frontend  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, FluentValidation, EF Core 9.0.6, Npgsql 9.0.4, ASP.NET Core SignalR 9.0.6 with Redis backplane, StackExchange.Redis 2.12.4, Next.js App Router, `@microsoft/signalr` 10.x, Axios, Zustand, Tailwind CSS, Lucide React  
**Storage**: PostgreSQL for durable support state and audit relations; Redis for ephemeral presence, distributed idempotency, and SignalR backplane; existing asset storage for approved chat attachments  
**Testing**: xUnit application tests, PostgreSQL-backed concurrency/integration tests in Docker, Playwright API/UI/E2E tests including Chromium and WebKit mobile profiles, TypeScript/ESLint/build checks  
**Target Platform**: Dockerized Linux VPS behind Nginx; modern desktop browsers for staff/admin; mobile Safari/Chrome and desktop browsers for students/guests  
**Project Type**: Multi-surface web application with API and real-time hub  
**Performance Goals**: p95 participant message visibility ≤2s; p95 assignment or queue result ≤3s; p95 queue admission after capacity release ≤3s; paginated histories remain interactive at 10,000 messages per conversation  
**Constraints**: One durable owner per conversation; no capacity overrun; no automatic phone-to-student disclosure; no WhatsApp/OTP; 2-minute staff disconnect grace; closed conversations immutable and read-only; one-to-five-star rating shared equally across all assignment owners  
**Scale/Scope**: Initial target 50 support-enabled employees, 500 concurrent participant sessions, 100 concurrent active conversations, 10,000 messages/day, and 1,000 queued or historical conversations without redesign

## Constitution Check

*GATE: Passed before research and passed again after design.*

| Constitution gate | Plan evidence | Result |
|---|---|---|
| Modular Clean Architecture | New domain entities and enums stay in Domain; commands/queries/ports stay in Application; persistence, Redis presence, guest session issuance, and assignment coordination stay in Infrastructure; HTTP/SignalR adapters stay in API; frontend uses service/hook/component boundaries. | PASS |
| Provider abstraction | Guest session, presence, action catalog, assignment coordinator, and attachment storage are behind interfaces; no controller writes database state directly. | PASS |
| Security and access control | Separate participant authorization policy, hashed/revocable guest session, owner-and-student-bound action execution, rate limits, idempotency, confirmation, redaction, and AuditLog correlation are mandatory. | PASS |
| Phased delivery | Tasks must deliver participant entry, routing, command center, actions, and admin oversight in independently testable groups; no AI/voice/third-party channels leak into scope. | PASS |
| Academic/content integrity | Existing student/admin MediatR rules remain authoritative; live support does not bypass academic or purchase invariants. | PASS |
| UX simplicity | Student/guest widget is mobile-first; staff workspace prioritizes queue, conversation, and student context; closed/read-only, waiting, failure, and unavailable states are explicit. | PASS |
| Observability | Every lifecycle transition emits a support event; state-changing student actions also create AuditLog entries with correlation and conversation IDs; queue, latency, transfer, failure, and rating metrics are queryable. | PASS |
| Existing design system | Reuse current admin/student tokens, Tajawal/Montserrat context, Lucide icons, 44px touch targets, restrained product styling, RTL, WCAG AA, and reduced motion. UI Pro Max's unrelated dark/green palette and Noto fonts are rejected to preserve Massar identity. | PASS |
| Database migrations only | All schema work is an EF Core migration; no direct production DDL. | PASS |
| Phase verification | Every implementation group must pass targeted automated tests, Docker health/migration gates, manual role QA, and an end-of-phase evidence update before the next group. | PASS |

### Layer Impact

- **Domain**: New live-support entities and enums; additions to `IAppDbContext`.
- **Application**: Live-support commands, queries, DTOs, validators, routing/action interfaces, catalog metadata, and audit orchestration; HR clock-in/out notifications.
- **Infrastructure**: EF mappings/migration, PostgreSQL assignment coordinator, Redis presence/session support, action adapters, and indexes.
- **API**: Participant/staff/admin controllers, guest session endpoints, `LiveSupportHub`, policies, rate limits, outbox dispatch integration, disconnect recovery hosted service.
- **Frontend**: Participant widget, staff command center, admin operations/configuration views, services, hub hook, state store, shell navigation, RTL/mobile/accessibility behavior.
- **Worker**: No change.
- **Docker/Nginx**: No new service or port; existing backend WebSocket route and Redis/PostgreSQL services are reused. Only migration and health verification are required.

## Phase 0: Research Decisions

All dependency, integration, concurrency, security, authorization, UI, testing, migration, and rollout unknowns are resolved in [research.md](./research.md). The key approved decisions are: a separate live-support aggregate, limited guest cookie authentication, PostgreSQL-serialized routing, Redis-only ephemeral presence, attendance-gated eligibility, transactional outbox delivery, conversation-bound typed student actions, immutable lifecycle/audit evidence, and gated rollout. No unresolved technical clarification remains.

## Phase 1: Design and Contracts

The detailed schema and transitions are defined in [data-model.md](./data-model.md). HTTP, SignalR, student-action, and responsive UI contracts are defined under [contracts/](./contracts/). Setup, migration, automated tests, Docker gates, manual QA, and rollout verification are defined in [quickstart.md](./quickstart.md). These artifacts are authoritative inputs to task generation.

## Architecture and Implementation Boundaries

### Durable state and real-time delivery

1. HTTP commands validate and persist each business transition with a `LiveSupportEvent` and, where applicable, an `OutboxEvent` in the same database save.
2. `LiveSupportHub` manages groups and ephemeral presence but does not contain business rules or direct EF writes.
3. The existing outbox processor is extended through a typed dispatcher so live-support events reach participant, conversation, staff, queue, and admin groups after commit.
4. Clients deduplicate by durable message/event IDs and reconcile from paginated HTTP snapshots after reconnect.

### Assignment atomicity

1. `ILiveSupportAssignmentCoordinator` is defined in Application and implemented in Infrastructure against `AppDbContext`.
2. Every assignment-changing path enters one PostgreSQL transaction and obtains a transaction-scoped advisory lock for the single support pool.
3. The coordinator locks the oldest waiting row, computes eligible checked-in and Redis-online staff below capacity, orders by active count then `LastAssignedAt`, writes one assignment, updates conversation/queue state, and commits.
4. Capacity edits below current load never evict active conversations. Checkout and confirmed disconnect route owned conversations through the same coordinator.
5. PostgreSQL-backed concurrency tests, not only EF InMemory tests, prove no over-capacity or double ownership.

### Participant identity

- Authenticated students continue to use the existing JWT and are linked by user ID.
- Guests receive an encrypted, HttpOnly, Secure support-session cookie representing a persisted `LiveSupportGuestSession`; no student lookup result is returned from guest name/phone.
- A dedicated authorization policy accepts either a Student JWT or valid guest support session only for participant endpoints and the live-support hub.
- Staff/admin routes require authenticated employee identity plus support configuration; write actions additionally require current conversation ownership, except explicit admin intervention endpoints.

### Student action catalog

- `ILiveSupportStudentActionHandler` is a justified registry abstraction with multiple concrete typed handlers from day one.
- Initial action keys and payloads are fixed in [contracts/student-action-catalog.md](./contracts/student-action-catalog.md).
- Each execution requires conversation ID, action key, idempotency key, confirmation token/version, and typed payload.
- The executor validates active assignment, linked student, action target ownership, existing business validators, and audit redaction before dispatch.
- Existing MediatR commands/business services are reused or extracted into shared Application services where direct reuse would create command nesting. Existing AdminController routes remain unchanged.
- Catalog contract tests fail if a server action lacks metadata/UI registration or if a UI action lacks a server handler.

### UI architecture

- **Participant**: shared `LiveSupportLauncher` and `LiveSupportWidget` on landing/student surfaces; unavailable schedule, guest intake, queue status, chat, reconnecting, closed/read-only, new-conversation, and rating states.
- **Staff**: `/assistant/live-support` uses queue/owned list, conversation, and lazy student-context/action drawer. On tablet the student panel becomes a full-height sheet; on narrow screens the three areas become drill-in views.
- **Admin**: `/admin/live-support` provides operations, history/metrics, staff capacity/schedule, and conversation investigation tabs; admin may intervene without silently becoming the normal owner.
- Reuse Massar tokens and shared primitives. Do not copy the existing oversized `AdminStudentProfileClient`; extract small student-context sections and typed action panels into `components/live-support`.
- Lists/messages use pagination or virtualization; server state remains in services/hooks while ephemeral selected tab/draft state uses a dedicated Zustand store.

## Project Structure

### Documentation (this feature)

```text
specs/142-live-support-command-center/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── live-support-api.yaml
│   ├── live-support-hub.md
│   ├── student-action-catalog.md
│   └── ui-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/src/NaderGorge.Domain/
├── Entities/LiveSupport/
├── Enums/LiveSupport*.cs
└── Interfaces/IAppDbContext.cs

backend/src/NaderGorge.Application/Features/LiveSupport/
├── Commands/
├── Queries/
├── Actions/
├── Dtos/
└── Interfaces/

backend/src/NaderGorge.Infrastructure/
├── Data/AppDbContext.cs
├── Migrations/<timestamp>_AddLiveSupportCommandCenter.cs
├── Services/LiveSupportAssignmentCoordinator.cs
├── Services/LiveSupportPresenceStore.cs
└── Services/LiveSupportGuestSessionService.cs

backend/src/NaderGorge.API/
├── Controllers/LiveSupportParticipantController.cs
├── Controllers/LiveSupportStaffController.cs
├── Controllers/LiveSupportAdminController.cs
├── Hubs/LiveSupportHub.cs
├── Authorization/LiveSupportAuthorization.cs
├── BackgroundServices/LiveSupportRecoveryBackgroundService.cs
└── Program.cs

backend/tests/NaderGorge.Application.Tests/LiveSupport/
├── ParticipantSessionTests.cs
├── RoutingPolicyTests.cs
├── ConversationLifecycleTests.cs
├── StudentLinkAndActionTests.cs
├── RatingAndMetricsTests.cs
└── LiveSupportSecurityTests.cs

backend/tests/NaderGorge.Integration.Tests/LiveSupport/
└── AssignmentConcurrencyTests.cs

frontend/src/
├── app/admin/live-support/{page.tsx,AdminLiveSupportPageClient.tsx}
├── app/assistant/live-support/{page.tsx,AssistantLiveSupportPageClient.tsx}
├── components/live-support/participant/
├── components/live-support/staff/
├── components/live-support/admin/
├── components/live-support/student-context/
├── hooks/useLiveSupportHub.ts
├── services/live-support-service.ts
└── stores/live-support-store.ts

frontend/tests/e2e/live-support.spec.ts
```

**Structure Decision**: Preserve the existing four-layer backend and App Router frontend. Live support is a separate bounded feature because the existing internal chat requires authenticated `User` participants and has no guest, queue, capacity, ownership, rating, or student-action semantics. Shared visual primitives and existing student business commands are reused through defined interfaces rather than table/entity inheritance.

## Exact Implementation Scope

### Backend production files to add or modify

- Add entities/enums described in [data-model.md](./data-model.md) under `NaderGorge.Domain/Entities/LiveSupport` and `NaderGorge.Domain/Enums`.
- Add all live-support DbSets to `IAppDbContext` and `AppDbContext`; configure table names, constraints, filtered unique indexes, delete behaviors, and optimistic timestamps.
- Scaffold one EF migration named `AddLiveSupportCommandCenter` and update the model snapshot.
- Add `Features/LiveSupport` commands/queries with one handler per transition and FluentValidation for every external payload.
- Add assignment, presence, participant-session, action-catalog, redaction, and metrics interfaces in Application with Infrastructure implementations.
- Update `ClockInCommand` and `ClockOutCommand` to emit post-commit routing notifications without embedding live-support persistence logic in HR handlers.
- Add the three controllers and one hub defined by the API and hub contracts.
- Add `LiveSupportRecoveryBackgroundService` to process two-minute disconnect expiry and queue reconciliation under distributed/advisory locks.
- Refactor `OutboxProcessorBackgroundService` to dispatch typed live-support targets without exposing guest events to unrelated groups.
- Register policies, services, hub, rate-limit policy, and hosted service in `Program.cs`.
- Extend E2E seed data with two support-enabled checked-in staff records, capacities, schedules, and deterministic student records.

### Frontend production files to add or modify

- Add typed DTOs and API methods in `live-support-service.ts`; never use `any` for action payload/result unions.
- Add a separate `useLiveSupportHub` hook with automatic reconnect, snapshot reconciliation, event deduplication, and participant/staff authentication modes.
- Add participant widgets to the public and student shell composition without duplicating logic across surfaces.
- Add staff/admin pages and shell navigation entries gated by support enablement/admin role.
- Build modular list, conversation, identity, student-context, action, audit, metrics, configuration, and rating components per [ui-contract.md](./contracts/ui-contract.md).
- Reuse/extract student detail presentation logic from `AdminStudentProfileClient` only where the extracted component has a real second caller; do not rewrite the existing page wholesale.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet build backend/NaderGorge.sln`
- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter LiveSupport`
- PostgreSQL integration command documented in `quickstart.md` for simultaneous arrival, capacity edit, closure, checkout, and disconnect races.
- `npm --prefix frontend run lint`
- `npm --prefix frontend exec tsc -- --noEmit`
- `npm --prefix frontend run build`
- `npm --prefix frontend exec playwright test tests/e2e/live-support.spec.ts --project=chromium`
- `npm --prefix frontend exec playwright test tests/e2e/live-support.spec.ts --project=webkit`
- Contract checks for API schemas, SignalR event names, and action catalog server/UI parity.

**Docker Gate Required**:

- `docker compose config -q`
- `make migrate`
- `make up`
- `make ps`
- `curl -f http://localhost:5245/api/health`
- `curl -f http://localhost:8738`
- `curl -f http://localhost:8739`
- `curl -f http://localhost:8740`
- `curl -f http://localhost:8742`
- WebSocket smoke: authenticated student, guest cookie, checked-in assistant, and admin all connect only to permitted live-support groups.

**Manual QA Required**:

- Student: open, queue, assigned chat, reconnect, close/read-only, new conversation, rating.
- Guest: name/phone intake, privacy-negative phone match, manual link, incorrect-link replacement, new-student creation.
- Two staff: attendance-gated eligibility, unequal load, tie rotation, capacity cap, FIFO queue, transfer, checkout, 2-minute disconnect reassignment.
- Staff actions: representative read/write from each action catalog category, validation failure, repeated idempotency key, confirmation and refreshed context.
- Admin: live board, history, metrics, ratings applied to all owners, capacity/schedule changes, intervention, audit before/after redaction.
- Responsive/accessibility: 320/375/768/1024/1440 widths, RTL, keyboard-only staff flow, screen-reader status announcements, reduced motion.

**End-of-Phase Report Format**: Implemented task IDs and files; migration status; exact commands and pass/fail counts; Docker service health; manual QA checklist with evidence; unresolved risks; explicit go/no-go. A failed gate blocks the next implementation group unless the owner accepts a documented risk.

## Rollout and Recovery

1. Deploy migration before exposing navigation or launcher.
2. Seed no support-enabled staff automatically; admin explicitly enables employees, sets capacity, and adds schedule windows.
3. Keep participant launcher disabled by a cached platform setting until staff configuration and smoke tests pass.
4. Enable admin view first, staff command center second, student users third, guest/public entry last.
5. Monitor queue age, assignment failures, hub disconnects, action failures, duplicate prevention, outbox lag, and rating submission errors.
6. Rollback UI exposure by disabling the platform setting; retain durable conversation/audit data. Schema rollback is not attempted after production data exists.

## Complexity Tracking

No constitution violations require exceptions. The separate live-support aggregate and action registry add code, but are required because the internal chat cannot represent guests, queue ownership, capacity, ratings, or conversation-bound elevated actions safely.
