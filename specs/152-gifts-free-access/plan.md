# Implementation Plan: Gifts and Free Access

**Branch**: `152-gifts-free-access` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/152-gifts-free-access/spec.md`

## Summary

Add an auditable Admin gift workflow for direct package, lesson, video, and exam access plus general or teacher-restricted promotional balances. The implementation introduces a dedicated gift aggregate, recipient-level outcomes, immutable promotional-balance allocations/usages, target-aware access consumption, and a `gifts.manage` permission. Purchases consume eligible promotional value by earliest expiry before paid balance inside one serializable database transaction. Gifts never create teacher or platform sales revenue.

## Technical Context

**Language/Version**: C# 13 on .NET 9; TypeScript 5.9 strict on Next.js 16.2.7 and React 19.2.4
**Primary Dependencies**: ASP.NET Core, MediatR, FluentValidation, EF Core 9.0.6, Npgsql 9.0.4, Next.js App Router, Axios, Zustand, Tailwind CSS, Lucide React
**Storage**: PostgreSQL 16 through EF Core migrations
**Testing**: xUnit application tests, `dotnet test`, ESLint, Next.js production build, Playwright E2E, Docker health/surface checks, SQL invariant checks
**Target Platform**: Linux Docker deployment; modern desktop/mobile browsers for Admin and Student surfaces
**Project Type**: Layered web application with backend API and separate Next.js surfaces
**Performance Goals**: list and lookup requests complete within 2 seconds under normal platform load; a 100-recipient issuance completes within 5 seconds; no overspend under concurrent purchase attempts
**Constraints**: Arabic-first RTL UI; 1-100 deduplicated recipients per issuance; no negative monetary values; no promotional value stored in paid balance; Admin bypass remains authoritative; all mutation paths audited and idempotent
**Scale/Scope**: three Admin routes, one Student balance update, six Admin gift endpoints, purchase/access integration, four new tables plus access-grant linkage, one EF migration

## Constitution Check

### Pre-Design Gate

| Principle | Result | Evidence |
|---|---|---|
| Modular Clean Architecture | PASS | Domain entities/enums, Application commands/queries/services, Infrastructure EF configuration, API controller, frontend service/components remain separated. |
| Provider Abstraction | PASS | No external provider is introduced; gift funding is isolated behind application interfaces instead of added to payment-code logic. |
| Security & Access Control | PASS | `gifts.manage`, Admin bypass, server-side route/API enforcement, FluentValidation, audit events, and idempotency are mandatory. |
| Phased Delivery | PASS | Feature is independently usable and excludes coupons, print templates, messaging, and teacher accounting. |
| Academic Content Integrity | PASS | Existing content hierarchy and teacher ownership remain authoritative for eligibility. |
| Data Integrity | PASS | Conservation constraints, unique request/recipient keys, serializable purchase transaction, atomic conditional updates, and SQL checks are specified. |
| Verification & Operations | PASS | Automated, Docker, SQL, E2E, and manual owner gates are listed below. |

### Layer Impact

| Layer | Impact |
|---|---|
| Backend Domain | New gift, recipient, promotional allocation/usage entities and enums; optional gift linkage/use counters on `StudentAccessGrant`. |
| Backend Application | Gift issue/list/detail/revoke flows, gift-specific lookups, promotional funding service, video-specific access check, video/exam consumption hooks, balance projection changes. |
| Backend Infrastructure | DbSets, mappings, indexes/check constraints, migration, atomic allocation operations. |
| Backend API | `AdminGiftsController`; existing student balance and purchase contracts extended compatibly. |
| Frontend Admin | Gift ledger, issue, and detail/revoke routes; shell entry; role permission definition; direct Admin-only video-types shell entry. |
| Frontend Student | Paid/promotional balance distinction and purchase funding preview/result. |
| Worker | No impact. |
| Mobile | No impact. |
| Docker | No topology change; backend/frontend rebuild and migration required. |

### Post-Design Re-check

PASS. The selected design does not add a new project or external dependency, does not overload access codes or paid balance, and preserves existing clean-architecture boundaries. No constitution exception is required.

## Phase 0: Research Decisions

The resolved technical decisions are recorded in [research.md](./research.md). The decisive outcomes are: use a dedicated gift aggregate instead of access codes, keep promotional value outside paid balance, make PostgreSQL the transaction authority, resolve teacher ownership from content at purchase time, count only target-aware successful uses, and require no worker or scheduled job for correctness.

## Phase 1: Design Outputs

- [data-model.md](./data-model.md) defines entities, constraints, relationships, and state transitions.
- [contracts/gifts-api.yaml](./contracts/gifts-api.yaml) defines Admin issuance, ledger, revocation, and lookup contracts.
- [quickstart.md](./quickstart.md) defines build, migration, automated, SQL, Docker, and owner QA evidence.
- The post-design constitution gate remains PASS after reviewing these outputs.

## Project Structure

### Documentation

```text
specs/152-gifts-free-access/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── gifts-api.yaml
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

```text
backend/src/NaderGorge.Domain/
├── Entities/GiftEntities.cs
├── Entities/CodeEntities.cs
├── Enums/GiftEnums.cs
└── Interfaces/{IAppDbContext.cs,IAccessCheckService.cs,IPromotionalBalanceService.cs}

backend/src/NaderGorge.Application/
├── Features/Admin/Gifts/{Commands,Queries,Models}/
├── Features/Student/Balance/
├── Features/Student/Purchases/
├── Features/Tracking/Commands/CreateVideoSessionCommand.cs
├── Features/Exams/Commands/StartExamAttemptCommand.cs
├── Features/Content/Queries/GetLessonDetailQuery.cs
└── Services/{AccessCheckService.cs,PromotionalBalanceService.cs,GiftUsageService.cs}

backend/src/NaderGorge.Infrastructure/
├── Data/AppDbContext.cs
└── Migrations/*AddGiftsAndPromotionalBalance*.cs

backend/src/NaderGorge.API/Controllers/AdminGiftsController.cs
backend/tests/NaderGorge.Application.Tests/GiftsAndPromotionalBalanceTests.cs

frontend/src/
├── app/admin/gifts/{page.tsx,new/page.tsx,[id]/page.tsx}
├── app/student/balance/StudentBalancePageClient.tsx
├── components/admin/gifts/
├── components/admin/AdminShellChrome.tsx
├── components/admin/settings/AdminSettingsPageClient.tsx
├── components/balance/{BalanceDisplay.tsx,PurchaseContentModal.tsx}
├── services/{admin-gifts-service.ts,balance-service.ts}
└── app/admin/layout.tsx

frontend/tests/e2e/admin-gifts.spec.ts
```

**Structure Decision**: Extend the existing Domain/Application/Infrastructure/API layers and existing Next.js route/component/service conventions. The feature gets its own Admin application module and frontend service so gift-specific authorization does not depend on broader `users.manage` or `content.manage` permissions.

## Implementation Design

### 1. Gift Aggregate and Recipient Outcomes

- `GiftIssuance` is the immutable request header and carries a unique client-generated `RequestId`, target, terms, reason, issuer, and aggregate status.
- `GiftRecipient` records exactly one outcome per deduplicated student. Unique `(GiftIssuanceId, StudentId)` prevents duplicates independently from request idempotency.
- Header validation fails the request before persistence. Recipient validation is converted to outcome rows so one invalid/inactive/already-entitled student does not roll back valid recipients.
- Replaying `RequestId` returns the original issuance and outcomes without granting again.

### 2. Direct Content Access

- Package, lesson, video, and exam gifts create `StudentAccessGrant` rows linked to their `GiftRecipient`.
- Video access gets a dedicated `HasAccessToVideoAsync` path. Lesson detail returns only directly granted videos when full lesson access is absent and excludes sibling videos, resources, homework, and lesson-level exam access.
- Video use increments only after a new playable session is successfully created. Exam use increments only when a fresh attempt is created, never when resuming an in-progress attempt.
- Existing non-gift entitlement takes precedence and is never weakened. A grant limit is consumed only when the gift is the access source that enabled the action.

### 3. Promotional Balance Conservation

- Promotional allocations are not added to `StudentBalance.CurrentBalance`.
- Every allocation obeys `OriginalAmount = AvailableAmount + ConsumedAmount + ExpiredAmount + RevokedAmount` and all parts are non-negative.
- Eligible allocations are active, unexpired, under their funded-purchase cap, and either general or restricted to the content's authoritative teacher.
- Purchases consume allocations by `ExpiresAt NULLS LAST, CreatedAt, Id`, then paid balance for any remainder.
- `PurchaseContentCommand` owns a serializable transaction covering promotional allocation updates, paid deduction, access grant, usage rows, and the purchase event. Conditional updates prevent concurrent overspend; a conflict rolls back cleanly and returns a retryable business error.
- Purchase/audit projections expose promotional and paid portions. Only the paid portion may flow to existing sales accounting; gifted value produces no teacher commission, payout, or platform sales revenue.

### 4. Expiration and Revocation

- Access checks enforce expiration without deleting evidence.
- Promotional allocations are lazily expired under an atomic update before balance projection, purchase funding, or revocation. No scheduler is required for correctness.
- Revocation requires a reason and is idempotent. It disables future content access or moves only currently available promotional value to `RevokedAmount`; sessions, attempts, usages, and paid balance remain intact.
- Every issuance, recipient outcome, consumption, expiration, revocation, replay, and denied destructive action is written through the existing audit facility.

### 5. Authorization and Shell

- All `/api/admin/gifts/**` operations and gift-specific lookup endpoints require `gifts.manage`; built-in Admin passes through the existing permission filter bypass.
- `/admin/gifts`, `/admin/gifts/new`, and `/admin/gifts/[id]` use route metadata plus server API checks. The Admin Shell shows the entry only for Admin or `gifts.manage`.
- `gifts.manage` is added to permission definitions and nav mapping so eligible staff roles can receive it.
- `/admin/content/video-types` receives a direct Admin-only shell entry and a specific Admin-only route rule before generic `/admin/content`. Existing type-list access for authorized content forms remains unchanged; mutations remain Admin-only.

### 6. Frontend Workflow

- The ledger is the default page with search, target/status filters, paging, and a clear create command.
- The issue flow uses compact stages: target type/target, recipients, terms, then review. Search uses gift-scoped endpoints so delegated gift managers do not need unrelated permissions.
- Details show issuance metadata and per-recipient outcome/value/use history; revoke is a reason-required confirmation dialog.
- Student balance and purchase UI show paid balance, eligible promotional balance, restrictions/expiry, and the projected promotional/paid split without exposing admin reasons.
- All pages include explicit loading, empty, validation, partial-success, permission-denied, and retry states; RTL and keyboard/focus behavior follow existing shared components.

## Delivery Sequence

1. Add domain model, interfaces, EF mappings, and migration with conservation/uniqueness constraints.
2. Implement promotional-balance and gift-usage services with focused unit/integration tests.
3. Implement Admin gift commands/queries/lookups/controller and permission/audit enforcement.
4. Integrate direct video/exam access consumption and partial lesson projection.
5. Integrate promotional funding into purchases and extend student balance contracts.
6. Build Admin routes/components/service and shell/permission entries, including Admin-only video-types entry.
7. Update Student balance/purchase UI and add Playwright coverage.
8. Run quality guards, complete automated/Docker/SQL gates, and leave owner manual QA as pending until performed.

## Phase Closure & Verification Plan

**Automated Tests Required**:

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj
dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj
cd frontend && npm run lint
cd frontend && npm run build
cd frontend && npx playwright test tests/e2e/admin-gifts.spec.ts --project=chromium
```

Critical paths: permission denial/no writes, idempotent bulk partial success, exact video-only isolation, video/exam use counting, expiration/revocation, teacher restriction, earliest-expiry funding, mixed funding atomicity, concurrent conservation, Admin bypass, staff assignment, and student disclosure.

**Docker Gate Required**:

```bash
docker compose config -q
make up
make migrate
make ps
make verify-surfaces
curl -fsS http://localhost:8740/admin/gifts
curl -fsS http://localhost:8739/health
```

Run SQL invariants after migration: no negative promotional components; exact conservation; no duplicate request ids or issuance/student rows; no dangling gift grants/usages; no gifted amount in paid balance transactions or teacher revenue records.

**Manual QA Required**: Product owner logs in as Admin and delegated staff with/without `gifts.manage`; issues each direct target and both balance scopes; verifies video-only isolation; verifies expiration/use limits; performs general, restricted, and mixed purchases; opens/resumes exam attempts; revokes unused remainder; checks ledger/audit; verifies Student balance wording; verifies direct video-types shell visibility. Record each as `passed`, `failed`, or `pending`. Until the owner performs these checks, manual QA remains `pending` and the feature is not described as manually verified.

**End-of-Phase Report Format**: implemented scope; changed artifacts; automated command/result table; Docker/migration/health evidence; SQL invariant evidence; manual QA checklist and status; residual risks; explicit go/no-go. Failed gates block completion unless the owner explicitly accepts a documented risk.

## Complexity Tracking

No constitution violations require justification.
