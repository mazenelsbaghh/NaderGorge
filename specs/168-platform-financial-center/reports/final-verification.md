# Verification record

## Passed locally

- `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj --no-restore` — passed, 0 warnings, 0 errors.
- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-restore` — passed before final verification; finance-focused suite passed after the latest changes.
- `cd frontend && npm run lint && npm run typecheck` — passed.
- `dotnet ef migrations has-pending-model-changes ...` — no pending model changes.
- `make ops-check` — passed local DB guard, API build, migration check, and application tests.

## Environment-gated

- Full integration tests require `ConnectionStrings__DefaultConnection`; without it the repository intentionally fails fast rather than using EF InMemory. The local run reported 48 environment failures and 29 passes.
- Production migration gate, remote immutable build, and rolling deployment require the configured three-node SSH/backup environment and are not represented as local test success.

## Safety notes

- Historical reconstruction is dry-run first and uses source-scoped journal idempotency keys.
- Refunds require an existing `SalesFinancialEffect` and cannot exceed the paid source remainder.
- Closed accounting periods reject new postings.
