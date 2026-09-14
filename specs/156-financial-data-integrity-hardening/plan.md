# Implementation Plan: Financial and Data Integrity Hardening

**Branch**: `156-financial-data-integrity-hardening` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/156-financial-data-integrity-hardening/spec.md`

## Summary

Harden Phase 2 finance/data workflows by moving critical invariants into atomic state transitions and database constraints: student balance idempotency, recharge/SMS one-to-one matching, teacher payout reservation, grant target-shape/active uniqueness, and restrict/no-action financial relationships. Existing backend command handlers and EF mappings are updated; no new frontend UI is required.

## Technical Context

**Language/Version**: C# 13 on .NET 9 backend; TypeScript frontend untouched except no UI change expected  
**Primary Dependencies**: ASP.NET Core, MediatR, EF Core 9, Npgsql/PostgreSQL, existing `ApiResponse<T>` and middleware  
**Storage**: PostgreSQL via EF Core migrations; EF InMemory remains used by existing application tests  
**Testing**: `dotnet test` for application tests, `dotnet build` for API, `docker compose config -q`  
**Target Platform**: Backend API and PostgreSQL schema used by Docker/local deployment  
**Project Type**: Web-service backend with existing frontend/mobile callers  
**Performance Goals**: Pending recharge matching must stay indexed by wallet/status/amount/sender/created time; financial mutation commands remain single request/transaction flows  
**Constraints**: No data-loss cascade deletes; expected concurrency conflicts must not become unhandled 500s; changes must respect existing Clean Architecture layering where practical  
**Scale/Scope**: Phase 2 financial/data integrity only; no new product surfaces beyond DTO fields needed for reserved/available payout balance

## Constitution Check

- **Layer impact**: Backend Domain entities, Application command handlers/services/tests, Infrastructure EF mappings/migration, API middleware conflict mapping. Frontend/worker/mobile unchanged unless compile-time contracts require no-op adaptation.
- **Automated tests required**: Finance payout reservation tests, recharge idempotency/state tests, schema/model invariant tests, API middleware conflict mapping test if directly testable.
- **Manual QA required**: Teacher payout request/reject/pay flow; student recharge retry/duplicate SMS flow; admin duplicate approval denial.
- **Docker gate required**: `docker compose config -q`; migration creation verified by compiling Infrastructure model; full migration apply is owner/runtime dependent if database service not running.
- **No-next-phase gate**: Phase 3 remediation must not start until Phase 2 checkboxes in the remediation document are either checked with evidence or left unchecked with explicit blockers.

## Project Structure

### Documentation (this feature)

```text
specs/156-financial-data-integrity-hardening/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── financial-integrity-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/src/NaderGorge.Domain/
├── Entities/StudentBalance.cs
├── Entities/TeacherAccount.cs
├── Entities/TeacherPayout.cs
├── Entities/DigitalWallet.cs
├── Entities/RechargeRequest.cs
├── Entities/IncomingSmsLog.cs
└── Entities/CodeEntities.cs

backend/src/NaderGorge.Application/
├── Common/
├── Services/BalanceService.cs
├── Features/Admin/Recharge/ResolveRechargeRequestCommand.cs
├── Features/Android/AndroidUploadSmsCommand.cs
├── Features/Student/Recharge/SubmitRechargeCommand.cs
├── Features/Teacher/Finance/Commands/RequestPayoutCommand.cs
├── Features/Admin/Finance/Commands/ResolvePayoutCommand.cs
└── Features/Teacher/Finance/Queries/GetTeacherAccountQuery.cs

backend/src/NaderGorge.Infrastructure/
├── Data/AppDbContext.cs
└── Migrations/

backend/src/NaderGorge.API/
└── Middleware/ExceptionHandlingMiddleware.cs

backend/tests/NaderGorge.Application.Tests/
├── Finance/CommissionTests.cs
├── BalanceOutboxTests.cs
├── SmsParserTests.cs
└── FinancialDataIntegrityTests.cs
```

**Structure Decision**: Backend-only hardening with schema/model changes in Infrastructure, behavior changes in Application handlers/services, and tests in existing Application test project.

## Phase 0: Research Output

Research decisions are captured in [research.md](./research.md), including payout reservation strategy, recharge idempotency, expected conflict mapping, access grant shape constraints, and delete behavior.

## Phase 1: Design Output

Design artifacts are captured in [data-model.md](./data-model.md), [contracts/financial-integrity-contract.md](./contracts/financial-integrity-contract.md), and [quickstart.md](./quickstart.md).

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~FinancialDataIntegrityTests|FullyQualifiedName~CommissionTests|FullyQualifiedName~BalanceOutboxTests"`
- `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj`
- `docker compose config -q`

**Docker Gate Required**: `docker compose config -q`; if database is available, run migration apply through project migration command or `dotnet ef database update`.

**Manual QA Required**:
- Teacher requests payout, sees reserved/available balance impact, then admin rejects and reserve returns.
- Teacher requests payout, admin marks paid, current and reserved balances settle.
- Student recharge duplicate retry does not add a second balance transaction.
- Admin duplicate resolve of same recharge returns a controlled failure.

**End-of-Phase Report Format**: list implemented files, checked Phase 2 remediation items, commands run, pass/fail, blockers, and readiness for Phase 3.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
