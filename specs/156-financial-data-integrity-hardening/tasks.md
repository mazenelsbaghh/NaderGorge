# Tasks: Financial and Data Integrity Hardening

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Phase 1: Setup

- [x] T001 Add current feature evidence entries to `achievements.md`.
- [x] T002 Update `AGENTS.md` Spec Kit marker with `specs/156-financial-data-integrity-hardening/plan.md`.

## Phase 2: Foundational Database Invariants

- [x] T003 Add `ReservedBalance` to `backend/src/NaderGorge.Domain/Entities/TeacherAccount.cs`.
- [x] T004 Add EF check constraints/indexes for student balance, balance transactions, teacher accounts, digital wallets, recharge/SMS, and grants in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T005 Add EF migration and snapshot updates under `backend/src/NaderGorge.Infrastructure/Migrations/`.
- [x] T006 Add expected database conflict mapping to `backend/src/NaderGorge.API/Middleware/ExceptionHandlingMiddleware.cs`.

## Phase 3: User Story 1 - Safe Student Recharge And Balance Changes

- [x] T007 [US1] Harden `backend/src/NaderGorge.Application/Services/BalanceService.cs` so duplicate referenced credit is idempotent/conflict-safe and overdraft remains atomic.
- [x] T008 [US1] Harden `backend/src/NaderGorge.Application/Features/Admin/Recharge/ResolveRechargeRequestCommand.cs` to reject non-pending transitions and duplicate SMS/credit safely.
- [x] T009 [US1] Harden `backend/src/NaderGorge.Application/Features/Android/AndroidUploadSmsCommand.cs` for one SMS to one recharge and one recharge credit.
- [x] T010 [US1] Harden `backend/src/NaderGorge.Application/Features/Student/Recharge/SubmitRechargeCommand.cs` for one SMS to one recharge and one recharge credit.
- [x] T011 [US1] Add recharge/balance tests in `backend/tests/NaderGorge.Application.Tests/FinancialDataIntegrityTests.cs`.

## Phase 4: User Story 2 - Safe Teacher Payout Reservation

- [x] T012 [US2] Update `backend/src/NaderGorge.Application/Features/Teacher/Finance/Commands/RequestPayoutCommand.cs` to reserve available balance atomically.
- [x] T013 [US2] Update `backend/src/NaderGorge.Application/Features/Admin/Finance/Commands/ResolvePayoutCommand.cs` to release reserve on rejection and settle current plus reserve on payment.
- [x] T014 [US2] Update `backend/src/NaderGorge.Application/Features/Teacher/Finance/Queries/GetTeacherAccountQuery.cs` to expose reserved/available balance in DTO.
- [x] T015 [US2] Update payout tests in `backend/tests/NaderGorge.Application.Tests/Finance/CommissionTests.cs`.

## Phase 5: User Story 3 - Valid, Idempotent Access Grants

- [x] T016 [US3] Add model/schema checks for grant target shape and active duplicate protection in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T017 [US3] Add model tests for grant constraints/indexes in `backend/tests/NaderGorge.Application.Tests/FinancialDataIntegrityTests.cs`.

## Phase 6: User Story 4 - Restrict Destructive Deletes For Financial History

- [x] T018 [US4] Confirm and adjust financial/audit relationships in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` to `Restrict` or `NoAction`.
- [x] T019 [US4] Add model tests for delete behavior on finance/audit relationships in `backend/tests/NaderGorge.Application.Tests/FinancialDataIntegrityTests.cs`.

## Final Phase: Polish & Verification

- [x] T020 Run deep critique and fix every issue before guards.
- [x] T021 Run `clean-code-guard` against changed production files and fix findings.
- [x] T022 Run `test-guard` against changed test files and fix findings.
- [x] T023 Run feature tests: `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~FinancialDataIntegrityTests|FullyQualifiedName~CommissionTests|FullyQualifiedName~BalanceOutboxTests"`.
- [x] T024 Run final build/Docker checks: `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj` and `docker compose config -q`.
- [x] T025 Update `docs/full-platform-defects-remediation-phases-2026-06-29.md` Phase 2 checkboxes only for verified completed tasks.

## Dependencies

- T003-T006 block all user stories.
- US1 and US2 can proceed independently after foundations.
- US3 and US4 are schema/model hardening and can proceed after T004.
- Final guards/tests run after all implementation tasks.

## Independent Test Criteria

- **US1**: duplicate recharge/SMS attempts create one credit and overdraft debit fails without mutation.
- **US2**: payout reserve, reject, and paid transitions preserve `CurrentBalance`, `ReservedBalance`, and available balance invariants.
- **US3**: EF model exposes check/unique constraints for valid grant shape and duplicate active grant blocking.
- **US4**: EF model uses restrict/no-action for finance/audit relationships.
