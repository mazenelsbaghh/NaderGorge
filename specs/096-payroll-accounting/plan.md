# Implementation Plan: Payroll, Teacher Finance, and Activated Code Accounting

**Branch**: `096-payroll-accounting` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/096-payroll-accounting/spec.md`

## Summary

This feature completes the internal financial cycle: staff monthly payroll, teacher balance and payouts, and access code activation commission tracking.
- **Backend**: Add new domain entities for payroll records, payroll adjustments, teacher accounts, teacher payouts, and access code activation logs. Register these in the DbContext. Hook commission calculations directly into the code activation flow inside a serializable transaction to ensure atomicity. Create CQRS handlers and controllers for admin finance management and teacher self-service.
- **Frontend**: Create the `/admin/finance` interface for staff payroll generation, additions/deductions management, and payout reviews. Create `/teacher/finance` for teachers to view their earnings history, transaction ledger (EGP only, no points), and request payouts. Enforce role-based access checks.
- **Tests**: Write unit tests for payroll computations, locked month restrictions, and commission activation rates. Create a Python integration test suite checking role isolation and API end-to-end flows.

## Technical Context

**Language/Version**: C# 13 (.NET 9), TypeScript 5.x (React 19 / Next.js 16.2)  
**Primary Dependencies**: MediatR, Entity Framework Core 9.0, Tailwind CSS, Lucide React, Axios, Zustand  
**Storage**: PostgreSQL (for persistent entities), Redis (optional local caching/queueing)  
**Testing**: xUnit for C# tests, pytest for API smoke/permissions tests  
**Target Platform**: Linux server (Docker) / Web browsers  
**Performance Goals**: Balance queries and transaction listing in under 200ms; payroll generation under 1 second  
**Constraints**: Keep EGP completely separated from gamification points; strict role segregation (Assistants/Students have 0 access to finance)  
**Scale/Scope**: ~10 teachers, ~50 staff/assistants, thousands of daily student activations  

## Constitution Check

- **Layer impact**: Domain (Entities and Enums), Infrastructure (DbContext & Migrations), Application (CQRS handlers), API (Controllers), and Frontend (Service layer, Admin & Teacher pages).
- **Automated tests**: Backend unit tests covering payroll net calculations, locked status edits, and commission logging. Python integration test asserting HTTP 403 when assistants/students call finance APIs and verifying teacher data isolation.
- **Manual QA**: Admin generates payroll, adds adjustment, approves it. Student redeems code. Teacher views balance increase and requests payout. Admin approves payout.
- **Docker Gate**: Ensure containerized environment builds cleanly and migrations run correctly.

## Project Structure

### Documentation (this feature)

```text
specs/096-payroll-accounting/
├── plan.md              # This file
├── research.md          # Research on schemas, transactions, and isolation
├── data-model.md        # Database schema definitions
├── quickstart.md        # Quick guide for running and verifying the feature
└── contracts/
    └── endpoints.md     # API endpoints design
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   ├── PayrollRecord.cs
│   │   │   ├── PayrollAdjustment.cs
│   │   │   ├── TeacherAccount.cs
│   │   │   ├── TeacherPayout.cs
│   │   │   └── AccessCodeActivationLog.cs
│   │   └── Enums/
│   │       ├── PayrollStatus.cs
│   │       ├── PayrollAdjustmentType.cs
│   │       └── PayoutStatus.cs
│   │   └── Interfaces/
│   │       └── IAppDbContext.cs
│   ├── NaderGorge.Infrastructure/
│   │   └── Data/
│   │       └── AppDbContext.cs
│   ├── NaderGorge.Application/
│   │   └── Features/
│   │       ├── Admin/
│   │       │   └── Finance/
│   │       │       ├── Commands/
│   │       │       │   ├── GeneratePayrollCommand.cs
│   │       │       │   ├── AddPayrollAdjustmentCommand.cs
│   │       │       │   ├── DeletePayrollAdjustmentCommand.cs
│   │       │       │   ├── ApprovePayrollCommand.cs
│   │       │       │   └── ResolvePayoutCommand.cs
│   │       │       └── Queries/
│   │       │           ├── GetPayrollQuery.cs
│   │       │           ├── GetPayoutsQuery.cs
│   │       │           └── GetCodeAccountingQuery.cs
│   │       ├── Teacher/
│   │       │   └── Finance/
│   │       │       ├── Commands/
│   │       │       │   └── RequestPayoutCommand.cs
│   │       │       └── Queries/
│   │       │           ├── GetTeacherAccountQuery.cs
│   │       │           ├── GetTeacherTransactionsQuery.cs
│   │       │           └── GetTeacherPayoutsQuery.cs
│   │       └── Codes/
│   │           └── Commands/
│   │               └── ActivateCodeCommand.cs
│   └── NaderGorge.API/
│       └── Controllers/
│           ├── AdminFinanceController.cs
│           └── TeacherFinanceController.cs
└── tests/
    └── NaderGorge.Application.Tests/
        └── Finance/
            ├── PayrollTests.cs
            └── CommissionTests.cs

frontend/
├── src/
│   ├── app/
│   │   ├── admin/
│   │   │   └── finance/
│   │   │       └── page.tsx
│   │   └── teacher/
│   │       └── finance/
│   │           └── page.tsx
│   ├── components/
│   │   └── finance/
│   │       ├── AdminPayrollTable.tsx
│   │       ├── AdminPayoutRequests.tsx
│   │       ├── TeacherFinanceDashboard.tsx
│   │       └── TeacherPayoutModal.tsx
│   └── services/
│       └── finance-service.ts

tests/
└── test_teacher_finance.py
```

**Structure Decision**: Fits into the existing Next.js frontend and the 4-project clean architecture of the C# backend.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- C# Tests: `dotnet test backend/tests/NaderGorge.Application.Tests --filter Category=Finance`
- Python Tests: `python3 -m pytest tests/test_teacher_finance.py -v`

**Docker Gate Required**:
- `docker compose config -q`
- `make up` and `make migrate`
- Verify services status and logs.

**Manual QA Required**:
1. Log in as Admin -> Go to `/admin/finance` -> Click Generate Payroll for Current Month.
2. Add a Deduction (e.g. absent day penalty) and Addition (e.g. bonus), check Net Salary calculation.
3. Click Approve to lock the record. Verify that editing or adding adjustments is now blocked.
4. Log in as a Student -> Redeem a package access code.
5. Log in as the Teacher -> Go to `/teacher/finance` -> Check that balance and total earnings are updated correctly, showing the code activation under transaction logs.
6. Request a payout for an amount greater than the balance (must be rejected by client & API).
7. Request a valid payout -> Log in as Admin -> Go to `/admin/finance` -> Payout requests tab -> Approve the payout request -> Verify teacher balance is deducted.

**End-of-Phase Report Format**:
Provide a summary of the scope completed, test results showing green, ESLint results, Next.js build output, and a visual rundown of the UI styling.
