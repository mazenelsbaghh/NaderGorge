# Implementation Plan: Phase 1 Sales and Content Completion

**Branch**: `153-phase1-sales-content` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/153-phase1-sales-content/spec.md`

## Summary

Complete Phase 1 by adding a sales-rule layer, advanced digital coupons, printable QR/Serial sales codes with reusable templates, and standalone public exams. The implementation reuses existing content identity/video types from Spec 151, gift/promotional funding separation from Spec 152, existing checkout/access grants, and question-bank/exam attempt infrastructure. New financial effects are persisted separately from paid balance and teacher payout so Phase 3 accounting can consume reliable evidence later.

## Technical Context

**Language/Version**: C# 13 on .NET 9; TypeScript 5.9 strict on Next.js 16.2.7 and React 19.2.4
**Primary Dependencies**: ASP.NET Core, MediatR, FluentValidation, EF Core 9.0.6, Npgsql 9.0.4, Next.js App Router, Axios, Zustand, Tailwind CSS, Lucide React, existing QR generation utilities
**Storage**: PostgreSQL 16 through EF Core migrations
**Testing**: xUnit application tests, EF migration drift check, optional PostgreSQL integration tests, ESLint, TypeScript, Next.js production build, Playwright E2E
**Target Platform**: Linux Docker deployment; modern desktop/mobile browsers for Admin and Student surfaces
**Project Type**: Layered web application with backend API and separate Next.js Admin/Student surfaces
**Performance Goals**: checkout coupon preview and validation complete within 2 seconds; coupon/code admin list pages respond within 2 seconds for 10k rows with paging; printable batch creation supports up to 10,000 generated codes without duplicates
**Constraints**: Arabic-first RTL UI; no negative payable amount; no coupon/code consumption unless purchase/redemption commits; all financial mutations audited; new public exams must not mutate question bank into a sold product; existing CodeGroup redemption remains backward compatible
**Scale/Scope**: new Sales feature module, public exam product metadata, checkout integration, admin/student APIs, five Admin workspaces, one Student public-exams surface, one EF migration, focused tests and E2E smoke

## Constitution Check

### Pre-Design Gate

| Principle | Result | Evidence |
|---|---|---|
| Modular Clean Architecture | PASS | Domain entities/enums, Application Sales/PublicExams feature folders, Infrastructure mappings/migration, API controllers, and frontend services/components stay separated. |
| Provider Abstraction | PASS | No external provider is introduced; QR/export stays local and existing video-provider abstractions are untouched. |
| Security & Access Control | PASS | New permissions are explicit: `sales.manage`, `sales.templates.manage`, and `public_exams.manage`; student redemption/checkout validates server-side. |
| Phased Delivery | PASS | Scope completes Phase 1 and explicitly excludes Phase 3 teacher daily accounting/payout, external notifications, ads, and live video. |
| Academic Content Integrity | PASS | Public exams reuse question bank as source only; teacher/subject/grade classification is explicit and reportable. |
| Data Integrity | PASS | Unique coupon/code identifiers, serial uniqueness, usage counters, idempotency keys, transaction-scoped checkout, and audit evidence are mandatory. |
| Verification & Operations | PASS | Automated tests, Docker gate, SQL invariants, and manual QA plan are defined. |

### Layer Impact

| Layer | Impact |
|---|---|
| Backend Domain | Add coupon, sales-code batch/code, template, sales-rule, public-exam metadata, discount usage, and financial-effect entities/enums. Extend `Exam` and `StudentAccessGrant` minimally for public-exam product linkage/access. |
| Backend Application | Add `Features/Admin/Sales`, `Features/Admin/PublicExams`, `Features/Student/PublicExams`, coupon validation/discount services, printable-code redemption service, and checkout integration. |
| Backend Infrastructure | Add DbSets, EF mappings, indexes/check constraints, migration, and transaction-safe redemption/checkout constraints. |
| Backend API | Add `AdminSalesController`, `AdminPublicExamsController`, `PublicExamsController`, and extend `BalanceController`/purchase preview for coupon input. |
| Frontend Admin | Add Sales navigation/workspaces for coupons, printable batches, templates, sales rules, and public exams. |
| Frontend Student | Add coupon entry in purchase modal, code redemption feedback, public exam listing/detail/purchase/start flow. |
| Worker | No required impact. |
| Mobile | No required impact. |
| Docker | No topology change; backend/frontend rebuild and migration required. |

### Post-Design Re-check

PASS. The design uses existing project boundaries and adds no new external system. No constitution exception is required.

## Phase 0: Research Decisions

See [research.md](./research.md). Core decisions:

- Create a new Sales module instead of overloading legacy `CodeGroup`.
- Keep existing `CodeGroup` redemption compatible and migrate future advanced behavior through new entities.
- Integrate coupons and printable codes into checkout through one discount engine.
- Persist financial-effect evidence separately from teacher account balances.
- Model public exams as product metadata attached to `Exam`, not as a new question-bank product.

## Phase 1: Design Outputs

- [data-model.md](./data-model.md) defines entities, relationships, constraints, and state transitions.
- [contracts/phase1-sales-api.yaml](./contracts/phase1-sales-api.yaml) defines Admin/Student API contracts.
- [quickstart.md](./quickstart.md) defines automated tests, Docker gates, SQL invariants, and manual owner QA.
- AGENTS.md must reference this plan between the Spec Kit markers.

## Project Structure

### Documentation

```text
specs/153-phase1-sales-content/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── phase1-sales-api.yaml
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

```text
backend/src/NaderGorge.Domain/
├── Entities/SalesEntities.cs
├── Entities/ExamEntities.cs
├── Entities/CodeEntities.cs
├── Enums/SalesEnums.cs
└── Interfaces/
    ├── IAppDbContext.cs
    ├── IDiscountEngine.cs
    └── ISalesRedemptionService.cs

backend/src/NaderGorge.Application/
├── Features/Admin/Sales/
│   ├── Commands/
│   ├── Queries/
│   └── Models/
├── Features/Admin/PublicExams/
│   ├── Commands/
│   ├── Queries/
│   └── Models/
├── Features/Student/PublicExams/
│   ├── Commands/
│   └── Queries/
├── Features/Student/Commands/PurchaseContentCommand.cs
├── Features/Student/Queries/GetPurchaseFundingPreviewQuery.cs
├── Features/Codes/Commands/ActivateCodeCommand.cs
├── Features/Exams/Commands/StartExamAttemptCommand.cs
└── Services/
    ├── DiscountEngine.cs
    ├── SalesRedemptionService.cs
    └── AccessCheckService.cs

backend/src/NaderGorge.Infrastructure/
├── Data/AppDbContext.cs
└── Migrations/*AddPhase1SalesContent*.cs

backend/src/NaderGorge.API/Controllers/
├── AdminSalesController.cs
├── AdminPublicExamsController.cs
├── PublicExamsController.cs
├── BalanceController.cs
└── CodesController.cs

backend/tests/NaderGorge.Application.Tests/
├── Phase1SalesContentTests.cs
└── PublicExamProductTests.cs

frontend/src/
├── app/admin/sales/
│   ├── coupons/
│   ├── printable-codes/
│   ├── templates/
│   └── rules/
├── app/admin/public-exams/
├── app/student/public-exams/
├── components/admin/sales/
├── components/admin/public-exams/
├── components/student/public-exams/
├── components/balance/PurchaseContentModal.tsx
├── services/admin-sales-service.ts
├── services/admin-public-exams-service.ts
├── services/public-exams-service.ts
└── services/balance-service.ts

frontend/tests/e2e/
├── admin-sales.spec.ts
└── public-exams.spec.ts
```

**Structure Decision**: Implement a new Sales domain/application/frontend module because the feature crosses legacy access codes, coupons, printed batches, checkout, and future accounting. Public exams get their own Admin/Student feature folders because their lifecycle and reports must stay independent from lesson/video exams while reusing the exam attempt engine.

## Implementation Design

### 1. Sales Rules and Target Resolution

- Add `SalesRule` to represent whether a target may be sold/unlocked by coupon/code and what target constraints apply.
- Target types include Package, Term, ContentSection, Lesson, SpecificVideo, VideoType, PublicExam, Teacher, and Platform.
- Teacher/subject/grade ownership is resolved server-side from existing hierarchy:
  - Package has `TeacherId` and `SubjectId`.
  - Term/section/lesson/video inherit from package.
  - Public exam uses explicit metadata.
- Rule activation fails if the target requires teacher/subject/grade/video-type data and it cannot be resolved.
- Existing student purchase UI continues to use known product cards; sales rules are authoritative for coupon/code eligibility and future accounting evidence.

### 2. Digital Coupons

- Add `SalesCoupon` with unique normalized code, discount type, amount/percentage, target scope, owner/source, date range, global limit, per-student limit, stacking policy reference, and status.
- Add `SalesCouponUsage` keyed by coupon, student, purchase operation/idempotency key, and target. Usage rows are created only after checkout succeeds.
- Coupon validation is previewable and final-checkout safe. The final checkout runs the same validation inside the purchase transaction.
- Discount values are capped to the purchase price and to the admin-configured stacking policy.
- Every create/update/disable/use/failure-significant event writes audit evidence.

### 3. Discount Stacking Policy

- Add an administrator-managed `DiscountStackingPolicy` with mode:
  - `SingleOnly`
  - `AllowCouponAndPrintedCode`
  - `AllowMultipleWithCap`
- Policies define optional max total discount percentage or fixed cap and priority order.
- Checkout accepts zero or more discount inputs, resolves the applicable policy, computes allowed discount, and rejects combinations not allowed by policy.
- Default policy is `SingleOnly` for safety, but Admin can change policy from Sales settings.

### 4. Printable QR/Serial Sales Codes

- Add `PrintableCodeBatch` for batch metadata and `PrintableSalesCode` for each generated code/serial/QR payload.
- Printable code behavior can be:
  - discount,
  - direct access grant,
  - promotional credit,
  - checkout-bound discount.
- V1 must support discount and direct access for Package/Lesson/SpecificVideo/PublicExam and may reuse Spec 152 promotional-balance service for credit behavior if the plan task selects that path.
- Redemption is idempotent via request id and guarded by unique consumed state. A single-use code cannot be double-spent under concurrent requests.
- Existing `CodeGroup` endpoints remain available; new printable-code endpoints power advanced batch/template behavior. Existing `CodeGroup` can later be migrated, but this spec must not break it.

### 5. Simple Template Designer

- Add `PrintableCodeTemplate` with card dimensions, background settings, and JSON field layout.
- Allowed fields are fixed: QR, code text, serial, owner label, target label, price/value, expiry, and optional short notes.
- A template is usable only if QR or code text is present and every required element is inside printable bounds.
- Frontend uses a bounded canvas/card surface with draggable/resizable fixed fields, numeric position/size values, preview using sample code data, and export preview for a batch.
- Store layout as normalized JSON; backend validates required fields and bounds before save/use.

### 6. Standalone Public Exams

- Extend `Exam` with public-exam product metadata or a one-to-one `PublicExamProduct` linked to `Exam`.
- Product metadata includes publication status, price, free/paid flag, teacherId nullable, subjectId nullable, grade fields, platform-wide flag, availability window, and disable reason.
- Admin can create a public exam by selecting existing questions from the question bank or by reusing inline question creation, but the sold product is the public exam, not the bank item.
- Student public-exam list/detail filters published and available exams by classification; paid exams use checkout and grant access to that exam only.
- `StartExamAttemptCommand` recognizes public exam lifecycle:
  - free public exam allows eligible start without purchase,
  - paid public exam requires a valid public-exam access grant,
  - disabled exam blocks new purchases and new attempts,
  - previous attempts/results remain visible.
- Reports and Admin dashboard distinguish PublicExam attempts from lesson/video exam attempts.

### 7. Checkout, Access, and Financial Effect

- Extend purchase command/preview with optional coupon/code inputs and public-exam content type.
- Checkout order:
  1. Resolve content and authoritative price/teacher/platform context.
  2. Validate sales rule and discounts.
  3. Apply admin stacking policy and cap discount.
  4. Consume promotional balance from Spec 152 if eligible.
  5. Deduct paid balance for the remainder.
  6. Create access grant.
  7. Persist discount usages and `SalesFinancialEffect`.
  8. Emit outbox event with gross price, discount amount, promo amount, paid amount, teacher/platform split evidence.
- `SalesFinancialEffect` records gross amount, coupon/code discounts, promotional amount, paid amount, teacher share impact, platform share impact, and source ids. It does not update teacher daily account balances in this spec.

### 8. Authorization and UI

- Permissions:
  - `sales.manage`: coupons, sales rules, printable batches.
  - `sales.templates.manage`: printable templates.
  - `public_exams.manage`: public exam products.
- Built-in Admin bypass stays authoritative. Delegated staff need explicit permissions.
- Admin Shell gets a Sales group and Public Exams entry. Settings permission map includes new permissions.
- Student UI:
  - purchase modal includes optional coupon/printed-code input and preview feedback.
  - code redemption page supports advanced printable sales code results.
  - `/student/public-exams` lists free/paid standalone exams and routes to checkout/start/result.

## Delivery Sequence

1. Add domain enums/entities/interfaces and EF migration.
2. Implement target resolver, sales-rule validation, discount engine, and financial-effect service with tests.
3. Implement digital coupon admin APIs and checkout preview/final integration.
4. Implement printable code batches/redemption and template APIs.
5. Implement public exam product lifecycle, student list/detail/access, and attempt/report integration.
6. Build Admin sales/public-exam frontend workspaces and Student public-exam/coupon UI.
7. Add focused backend tests and Playwright smoke tests.
8. Run critique, clean-code-guard, test-guard, automated tests, Docker gate, and documentation updates.

## Phase Closure & Verification Plan

**Automated Tests Required**:

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Phase1SalesContentTests|FullyQualifiedName~PublicExamProductTests"
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-restore
dotnet ef migrations has-pending-model-changes --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API --no-build
cd frontend && npm run lint
cd frontend && npx tsc --noEmit
cd frontend && npm run build
cd frontend && npx playwright test tests/e2e/admin-sales.spec.ts tests/e2e/public-exams.spec.ts --project=chromium
```

Critical paths: permission denial/no writes, coupon validation, stacking policy, checkout atomicity, printed-code uniqueness/idempotency, template validation, public exam free/paid access, disabled public exam behavior, report separation, legacy CodeGroup regression, gift/promotional regression.

**Docker Gate Required**:

```bash
docker compose config -q
make up
make migrate
make ps
make verify-surfaces
curl -fsS http://localhost:8740/admin/sales/coupons
curl -fsS http://localhost:8740/admin/public-exams
curl -fsS http://localhost:8739/student/public-exams
```

Run SQL invariants after migration: no duplicate coupon codes; no duplicate printable serials; no negative discount/payable/financial-effect values; no coupon usage without purchase operation; no disabled public exam new attempt after disabled timestamp; no missing audit for financial mutations.

**Manual QA Required**: Product owner must create percentage/fixed coupons, configure stacking policy, create printed batch, create template, redeem code, publish free and paid public exams, buy/attempt/report public exam, test invalid/expired/disabled/over-limit paths, and verify delegated permission denial. Each item remains `pending` until run by the owner.

**End-of-Phase Report Format**: implemented scope; changed artifacts; command/result table; Docker/migration/health result; SQL invariant result; manual QA checklist status; known risks; go/no-go for next platform phase.

## Complexity Tracking

No constitution violations require justification.
