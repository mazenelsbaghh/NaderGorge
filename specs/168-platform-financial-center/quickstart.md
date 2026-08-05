# Quickstart: Implementing and Verifying Platform Finance

This artifact is an implementation handoff; no production finance mutation should be enabled until its phase gates and reconciliation evidence pass.

1. Create Phase 0 source inventory and month/source totals before adding posting behavior.
2. Implement foundation entities/configurations and generate an EF migration with `dotnet ef migrations add AddPlatformFinancialCenter` from the repository's normal backend migration project.
3. Add posting engine unit/integration tests first, then source adapters one business flow at a time.
4. Run live shadow posting and compare domain totals before enabling read/write finance screens.
5. Complete historical dry-run, resolve exceptions, post once, reconcile control accounts, and retain exported evidence.
6. Enable cockpit read-only, then expenses/refunds/treasury mutations by permission and feature flag.

## Verification

```bash
dotnet test
cd frontend && npm run lint && npm run typecheck && npm run build
docker compose config -q
make up
make migrate
make ps
make health
make verify-e2e
```

Manual acceptance must cover general and teacher recharge, purchase, unpaid/paid teacher effects, cash and balance refunds, paid/unpaid expenses, transfers, cashbox/wallet reconciliation, denied staff access, budgets, exports, period close/reopen, migration replay, and dashboard-to-journal drill-down.

At every phase, publish scope, migration changes, automated results, Docker health, reconciliation totals, manual checklist, risks, and go/no-go. Stop on any unexplained financial variance.
