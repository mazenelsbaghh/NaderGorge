# Quickstart: Financial and Data Integrity Hardening

## Focused Verification

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~FinancialDataIntegrityTests|FullyQualifiedName~CommissionTests|FullyQualifiedName~BalanceOutboxTests"
dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj
docker compose config -q
```

## Manual QA

1. Teacher requests a payout within available balance.
2. Confirm teacher current balance remains unchanged and reserved/available changes.
3. Admin rejects the payout; confirm reserve is released.
4. Teacher requests again, admin pays; confirm current and reserved balances settle.
5. Student creates/submits recharge with matching SMS; retry match/approval and confirm only one credit exists.
6. Attempt invalid duplicate active access grant through any available admin/code/gift path; confirm it is blocked or existing active grant is reused.
