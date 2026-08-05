# Phase 2 foundation

- Additive migrations exist for the ledger/control tables, operations, and planning/treasury tables.
- EF reports no pending model changes after the latest model snapshot.
- Posting tests cover balance, retry idempotency, malformed lines, closed periods, and reversal construction.
- The full PostgreSQL integration suite is intentionally environment-gated by `ConnectionStrings__DefaultConnection`; it must not silently fall back to InMemory.
