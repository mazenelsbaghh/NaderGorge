# Implementation Plan: Teacher Accounting Phase 3

**Branch**: `158-teacher-accounting-phase3` | **Date**: 2026-07-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/158-teacher-accounting-phase3/spec.md`

## Summary

Implement Phase 3 teacher economy end to end: every paid, discounted, free, code, lesson, package, public exam, and shared-package event creates an auditable teacher financial event; teachers get daily income, calendar, transaction, and payout views; admins review questionable/ready transactions before payout; shared multi-teacher packages distribute revenue by admin-defined percentage or fixed amount; public teacher profiles expose teacher content and moderated community. The implementation extends the existing finance/domain model instead of relying on the current code-only `AccessCodeActivationLog` path.

## Technical Context

**Language/Version**: C# 13 on .NET 9 backend; TypeScript 5.x with Next.js 16.2.7 and React 19.2.4 frontend  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, FluentValidation, EF Core 9.0.6, Npgsql, Next.js App Router, Axios service layer, Zustand where existing surfaces use it, Tailwind CSS, Lucide React  
**Storage**: PostgreSQL via EF Core migrations; existing Redis/worker infrastructure unchanged for this feature  
**Testing**: `dotnet test` for backend application tests; `dotnet build` for API; `cd frontend && npm run lint && npm run build`; targeted browser/manual QA for financial and profile flows  
**Target Platform**: Nader Gorge backend API, PostgreSQL schema, admin/teacher/student Next.js surfaces  
**Project Type**: Full-stack web application with backend domain/application/infrastructure layers and Next.js frontend  
**Performance Goals**: Teacher finance calendar and transaction pages must be paginated/indexed and avoid N+1 queries; admin finance pages must not fetch inactive tab datasets; payout and earning writes must remain single-transaction/idempotent flows  
**Constraints**: Financial history is append-only; payouts require explicit admin review then explicit paid marking; free/100% discount operations record zero-value tracking but do not add dues unless admin compensation is explicit; teacher-visible student data is limited to name, phone, and content/code context approved by the user  
**Scale/Scope**: Phase 3 roadmap items 3.1 through 3.4: teacher daily accounts, teacher balance/payout review, shared multi-teacher package, teacher public profile/community, and roadmap checkbox closure after verification

## Constitution Check

- **Layer impact**: Backend Domain adds teacher financial ledger/allocation, payout lifecycle fields, shared-package entities, and profile/community scope fields. Application adds finance write service, admin/teacher finance queries/commands, shared package commands/queries, and teacher public profile queries. Infrastructure adds EF mappings, constraints, indexes, migration, and model snapshot. API exposes admin, teacher, student/public endpoints. Frontend updates admin finance/shared-package screens, teacher finance calendar/ledger, student package purchase/browse, and teacher profile/community surfaces. Worker and Docker runtime are not functionally changed, but Docker configuration is verified.
- **Automated tests required**: Backend tests for earning creation from code activation, direct purchase, public exam, shared package allocation, zero-value event behavior, refund/cancel adjustment rules, payout Pending -> Approved -> Paid and Rejected transitions, suspicious event review queue, and EF constraints/indexes. Frontend lint/build required after UI changes.
- **Manual QA required**: Student buys a single-teacher lesson/package/public exam; student activates a code; student buys shared package; teacher sees daily/calendar transactions and only their share; admin reviews suspicious events and payout reports; admin approves then marks paid; admin rejects payout; teacher public profile opens before/after purchase and community moderation still applies.
- **Docker gate required**: `docker compose config -q`; if services are available, run the repository migration/startup flow (`make up`, `make migrate`, health checks) before deployment or document the owner-approved blocker.
- **No-next-phase gate**: Phase 4 must not start until Phase 3 roadmap checkboxes are updated with evidence or explicitly left unchecked with blockers.

## Project Structure

### Documentation (this feature)

```text
specs/158-teacher-accounting-phase3/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── teacher-accounting-api.md
│   ├── shared-package-api.md
│   └── teacher-profile-community-api.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/src/NaderGorge.Domain/
├── Entities/
│   ├── TeacherAccount.cs
│   ├── TeacherPayout.cs
│   ├── SalesEntities.cs
│   ├── ContentEntities.cs
│   ├── CodeEntities.cs
│   └── CommunityPost.cs
└── Enums/

backend/src/NaderGorge.Application/
├── Features/Teacher/Finance/
├── Features/Admin/Finance/
├── Features/Student/Commands/PurchaseContentCommand.cs
├── Features/Codes/Commands/ActivateCodeCommand.cs
├── Features/Admin/SharedPackages/
├── Features/Student/SharedPackages/
├── Features/Public/Teachers/
└── Services/

backend/src/NaderGorge.Infrastructure/
├── Data/AppDbContext.cs
└── Migrations/

backend/src/NaderGorge.Api/
├── Controllers/TeacherFinanceController.cs
├── Controllers/AdminFinanceController.cs
├── Controllers/AdminSharedPackagesController.cs
├── Controllers/StudentSharedPackagesController.cs
└── Controllers/PublicTeachersController.cs

backend/tests/NaderGorge.Application.Tests/
├── Finance/CommissionTests.cs
├── FinancialDataIntegrityTests.cs
├── TeacherAccountingPhase3Tests.cs
├── SharedPackageAccountingTests.cs
└── PublicTeacherProfileTests.cs

frontend/src/
├── services/finance-service.ts
├── services/shared-package-service.ts
├── services/teacher-service.ts
├── app/admin/finance/AdminFinancePageClient.tsx
├── app/admin/shared-packages/
├── app/teacher/finance/TeacherFinancePageClient.tsx
├── app/student/shared-packages/
└── app/student/teachers/
```

**Structure Decision**: Full-stack feature across existing backend domain/application/API/infrastructure layers and existing Next.js admin/teacher/student surfaces. No worker project changes are expected.

## Phase 0: Research Output

Research decisions are captured in [research.md](./research.md), including ledger ownership, payout lifecycle, shared-package allocation, zero-value/free operations, suspicious-event review, query optimization, and public profile/community scoping.

## Phase 1: Design Output

Design artifacts are captured in [data-model.md](./data-model.md), [contracts/teacher-accounting-api.md](./contracts/teacher-accounting-api.md), [contracts/shared-package-api.md](./contracts/shared-package-api.md), [contracts/teacher-profile-community-api.md](./contracts/teacher-profile-community-api.md), and [quickstart.md](./quickstart.md).

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TeacherAccountingPhase3Tests|FullyQualifiedName~SharedPackageAccountingTests|FullyQualifiedName~CommissionTests|FullyQualifiedName~FinancialDataIntegrityTests|FullyQualifiedName~PublicTeacherProfileTests"`
- `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj`
- `cd frontend && npm run lint && npm run build`
- `docker compose config -q`

**Docker Gate Required**: Validate Compose with `docker compose config -q`. If database services are available, run `make up`, apply the new migration through the repository migration command, and verify API/frontend health. If not available, document the blocker and run build/model tests instead.

**Manual QA Required**:
- Teacher opens `/teacher/finance`, sees today's income, totals, calendar day buckets, and day transaction detail with student name/phone/content/price/discount/teacher share/platform share.
- Student activates a code and directly buys lesson/package/public exam; teacher finance receives the correct event, and unrelated teachers do not see it.
- Student buys a shared multi-teacher package; each selected teacher sees only their own allocation, and platform share balances the purchase.
- Admin opens finance review, sees pending/suspicious events, approves payout to ready-for-transfer, then marks paid only after the real transfer.
- Admin rejects payout and verifies reserved balance is released.
- Free/100% discount purchase appears as a zero-value event without increasing dues unless explicit compensation is entered.
- Student opens teacher public profile before and after purchase; profile content visibility and community moderation remain correct.

**End-of-Phase Report Format**: implemented scope, roadmap checkboxes updated, migrations generated, commands run with pass/fail, Docker/migration status, manual QA checklist, residual risks, and go/no-go for Phase 4.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New append-only teacher financial ledger in addition to existing activation logs and sales effects | Existing finance is split between `AccessCodeActivationLog` and `SalesFinancialEffect`, and direct purchases currently do not credit teacher dues consistently | Extending code activation logs would keep purchases, public exams, free events, shared packages, adjustments, and payout review fragmented |
| Separate shared-package entities instead of overloading `Package.TeacherId` | Existing `Package` is single-teacher and tied to subject/teacher ownership; Phase 3 requires many teachers with independent allocations | Making `Package.TeacherId` nullable/multi-purpose would weaken existing single-teacher content guarantees and reporting |
