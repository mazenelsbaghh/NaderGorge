# Phase 3 dashboard evidence

The cockpit exposes cash/treasury, general student liability, teacher-scoped student liability, teacher payable, supplier payable, revenue, refunds, expenses, net profit, account balances, and journal drill-down. Query results are date-bounded and ledger pages are clamped to 200 entries.

Local frontend lint/typecheck and backend build pass. Production-like p95 evidence requires the PostgreSQL performance fixture and is not claimed locally.

The dashboard now also has a dedicated report dataset endpoint and a bounded source/month reconciliation endpoint, so Excel/PDF and screen totals share the same posted-journal source.
