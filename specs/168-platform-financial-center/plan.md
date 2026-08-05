# Implementation Plan: Platform Financial Center

**Branch**: `168-platform-financial-center` | **Date**: 2026-08-05 | **Spec**: [spec.md](./spec.md)
**Input**: Unified EGP accounting, treasury, expenses, refunds, teacher liabilities, budgets, reconciliation, and historical reconstruction.

## Summary

Add an append-only, balanced general ledger as a financial control layer over the existing recharge, sales, teacher-finance, payroll, and cancellation domains. Revenue is recognized on purchase; recharge is treasury cash against student liability. The release adds platform expenses, cash/balance refunds, cashboxes/wallets/banks, budgets, period close, reconciliation, drill-down reports, Excel/PDF exports, and granular admin/staff permissions. Existing domain records remain authoritative and project exactly once into the ledger.

## Technical Context

**Language/Version**: C# 13/.NET 9; TypeScript 5.9 strict; Next.js 16.2.7/React 19.2.4
**Primary Dependencies**: ASP.NET Core, MediatR, FluentValidation, EF Core 9/Npgsql, Next.js App Router, Axios, Zustand, Tailwind, Lucide React
**Storage**: PostgreSQL 16; existing private attachment storage; Redis only for existing coordination, not accounting authority
**Testing**: xUnit backend tests, frontend lint/typecheck/build, Playwright critical finance flows
**Target Platform**: Linux Docker production cluster and modern desktop/mobile browsers
**Project Type**: Web application with backend API and admin frontend
**Performance Goals**: Financial list/summary APIs p95 <500 ms; bounded pagination and indexed drill-downs
**Constraints**: EGP only; `decimal`/`numeric(18,2)`; transactional balanced posting; immutable posted records; EF migrations only
**Scale/Scope**: All historical platform movements, all teachers/students, multi-year reporting, admin plus explicitly permitted staff

## Constitution Check

### Pre-design gate

- **Architecture — PASS**: Domain/Application/Infrastructure/API boundaries remain intact; the ledger is not embedded in controllers.
- **Security — PASS**: Existing authorization model gains granular finance permissions; every mutation is audited.
- **Database — PASS**: Additive EF Core migrations, constraints, indexes, and deterministic backfill; no manual production DDL.
- **Money integrity — PASS**: EGP decimal arithmetic, balanced journals, transaction boundaries, idempotency, and linked reversals.
- **Layer impact — PASS**: Backend, frontend, PostgreSQL, tests, Docker migration/health gates; worker unchanged for core posting.
- **Phase gates — PASS**: Each phase requires automated tests, Docker validation, owner QA, and a written go/no-go report. A failed gate blocks the next phase unless the owner explicitly accepts documented risk.

### Post-design gate

The research, model, and contracts preserve existing source domains, use one financial posting service, avoid duplicate accounting authorities, and make historical ambiguity explicit. All gates remain **PASS**; no constitutional exception is required.

## Project Structure

```text
backend/
├── Domain/
│   └── Entities/Finance/                 # accounts, journals, treasury, expenses, refunds, budgets, periods
├── Application/
│   ├── Features/Admin/PlatformFinance/   # commands, queries, validators, DTOs
│   └── Interfaces/Finance/               # posting, reconciliation, export contracts
├── Infrastructure/
│   ├── Data/                             # mappings, indexes, EF migration
│   └── Services/Finance/                 # posting engine, source adapters, exports, migration
└── API/Controllers/Admin/                # PlatformFinanceController

frontend/
├── src/app/admin/platform-finance/       # dashboard, ledger, treasury, expenses, refunds, budgets, closing
├── src/components/admin/platform-finance/
└── src/services/platformFinanceService.ts

tests/
├── backend/                              # unit, integration, migration/reconciliation tests
└── frontend/                             # component and Playwright finance journeys
```

**Structure Decision**: Extend the existing backend/frontend projects. No new service or accounting worker is introduced. Large exports may later use existing background infrastructure without changing journal authority.

## Accounting Architecture

- Existing recharge, entitlement/sale, teacher-finance, payroll, and cancellation records remain business sources of truth.
- A single application posting service writes `JournalEntry` and `JournalLine` in the same database transaction as new money-changing operations.
- Legacy sources are reconstructed through source-specific adapters and deterministic keys; replay is safe.
- General-ledger control accounts reconcile to student balances, teacher payables, supplier payables, and treasury subledgers.
- Posted documents and journals are immutable. Corrections create an explicit reversal and, when required, a corrected replacement.
- Dashboard aggregates use bounded server-side projections refreshed from posted journals, never client-side summation of unbounded rows.

## Delivery Phases

### Phase 0 — Evidence freeze and mapping

Inventory every money source and status transition; snapshot row counts/totals by month; define chart of accounts and posting matrix; identify duplicate, missing, and ambiguous legacy records. Deliver a signed dry-run baseline before schema posting starts.

### Phase 1 — Ledger foundation

Add accounts, journals/lines, periods, treasury accounts, permissions, audit events, constraints, indexes, and the transactional/idempotent posting engine. Seed the EGP chart of accounts and at least one cashbox while mapping existing digital wallets.

### Phase 2 — Live source adapters

Integrate general/teacher recharge, direct purchases, codes, public exams, shared packages, teacher allocation/reversal/settlement/payment, payroll payment, and cancellation. Restore controlled general recharge support while retaining explicit immutable scope. Run source-versus-ledger reconciliation tests for every adapter.

### Phase 3 — Expenses, treasury, and refunds

Implement draft/post/reverse expenses, vendors, categories, cost centers, paid/unpaid/AP flows, cashbox/wallet/bank transfers, reconciliation, cash refunds, balance refunds, partial-refund limits, receipts, and teacher debt behavior after prior payout.

### Phase 4 — Historical reconstruction

Run dry-run adapters over all trustworthy movements; show totals, duplicates, gaps, and exception reasons. Resolve or explicitly suspense only owner-approved ambiguity, post one repeat-safe migration batch preserving original occurrence dates, and reconcile source totals/control accounts before cutover.

### Phase 5 — Cockpit, reports, budgets, and exports

Build RTL dashboard and drill-downs for treasury, unused balances, teacher obligations, AP, revenue, refunds, expenses, profit, and cash flow. Add weekly/monthly/yearly/custom versioned budgets and server-generated Excel/PDF exports using identical filters and privacy permissions.

### Phase 6 — Close, hardening, and rollout

Add close/reopen controls and backdated-post rejection; exercise concurrency, retries, reversals, permissions, large periods, and export privacy. Deploy additively behind a finance feature flag, compare shadow totals, enable read-only views, then mutations. Roll back application code only; retain forward-compatible schema and journals.

## Migration and Reconciliation Strategy

1. Add schema and seed accounts without changing existing behavior.
2. Enable live shadow posting with feature flag and compare each source event.
3. Run historical dry-run by source/month and export exceptions.
4. Resolve exceptions or document approved opening suspense; never infer profit from ambiguity.
5. Post deterministic migration batches, then verify debit=credit and subledger/control totals.
6. Freeze a cutover checkpoint, rerun delta sources, enable read-only cockpit, then authorized mutations.
7. Keep source IDs, timestamps, migration batch, actor, and correlation IDs for audit and replay proof.

## Phase Closure & Verification Plan

**Automated Tests Required**:

```bash
dotnet test
cd frontend && npm run lint && npm run typecheck && npm run build
make verify-e2e
```

Tests must cover every posting template, balance equality, duplicate retry, concurrency, closed periods, permissions, partial/full cash and balance refunds, paid-teacher reversal/debt, expense/AP/payment, treasury transfer/reconciliation, migration replay, dashboard reconciliation, and export filters.

**Docker Gate Required**:

```bash
docker compose config -q
make up
make migrate
make ps
make health
```

Verify backend, frontend, PostgreSQL, Redis/backplane, migration status, and admin/student surfaces. Production rollout uses the existing three-node immutable rolling procedure.

**Manual QA Required**: Owner and permitted staff test dashboard drill-down, denied permissions, general and teacher recharge, purchase recognition, cash/balance refund, already-paid teacher adjustment, paid/unpaid expense, cashbox/wallet transfer and reconciliation, budget variance, Excel/PDF export, period close/reopen, and historical exception review.

**End-of-Phase Report Format**: implemented scope; migrations; commands/results; reconciliation evidence; Docker/health result; owner QA checklist; unresolved exceptions/risks; explicit go/no-go. No later phase begins while a required gate is failing without owner-approved documented risk.

## Complexity Tracking

No constitution violations. The general ledger is necessary to separate owned cash, liabilities, income, and expenses; it reuses existing projects and source domains instead of introducing another service.
