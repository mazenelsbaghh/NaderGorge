# Platform Finance API Contract

Base path: `/api/admin/platform-finance`. All endpoints require authenticated admin/staff plus the named permission. Money is decimal EGP; dates are ISO-8601; list endpoints are bounded and paginated.

## Cockpit and ledger

- `GET /bootstrap` — account/treasury/category/cost-center filter metadata (`finance.dashboard.view`).
- `GET /dashboard?from&to&teacherId&treasuryId` — cash, liabilities, payable, revenue, expense, refunds, profit, cash flow (`finance.dashboard.view`).
- `GET /ledger?...&cursor&pageSize` and `GET /journals/{id}` — drill-down and source links (`finance.ledger.view`).
- `GET /teachers/{teacherId}/summary?from&to` — sales/share/reversals/debt/paid/outstanding (`finance.ledger.view`).

## Treasury

- `GET|POST /treasury-accounts`, `PUT /treasury-accounts/{id}` (`finance.treasury.manage`).
- `POST /treasury-transfers`, `POST /treasury-transfers/{id}/post|reverse` (`finance.treasury.manage`).
- `POST /reconciliations`, `POST /reconciliations/{id}/complete` (`finance.treasury.reconcile`).

## Expenses

- `GET|POST /expenses`, `GET|PUT /expenses/{id}` (`finance.expenses.view|create`).
- `POST /expenses/{id}/post`, `/payments`, `/reverse` (`finance.expenses.post`).
- CRUD `/expense-categories`, `/cost-centers`, `/vendors` (`finance.expenses.manage`).

## Refunds

- `GET|POST /refunds`, `GET /refunds/{id}` (`finance.refunds.view|create`).
- `POST /refunds/{id}/post|reverse` (`finance.refunds.post`). Cash posting requires treasury/reference; balance posting requires scope and optional teacher.

## Budgets, periods, migration, and reports

- CRUD `/budgets` and `POST /budgets/{id}/activate|archive` (`finance.budgets.manage`).
- `GET /periods`, `POST /periods/{id}/close|reopen` (`finance.periods.close|reopen`).
- `POST /migration/dry-run`, `GET /migration/{id}/exceptions`, `POST /migration/{id}/exceptions/{exceptionId}/resolve`, `POST /migration/{id}/post` (`finance.migration.manage`).
- `GET /reports/{profit-loss|cash-flow|financial-position|teacher|refunds|expenses|budget-variance}` (`finance.reports.view`).
- `POST /exports` with report/filter/`xlsx|pdf` (`finance.export`); detailed personal data additionally requires `finance.export.details`.

## Mutation envelope

Mutation requests include `idempotencyKey`, optional `reason`, `occurredAt`, and concurrency token for drafts. Responses return document ID/status, journal ID/number when posted, and audit correlation ID.

## Errors

- `400 FINANCE_UNBALANCED_ENTRY`, `FINANCE_INVALID_SCOPE`, `FINANCE_AMOUNT_EXCEEDED`
- `403 FINANCE_PERMISSION_DENIED`
- `404 FINANCE_SOURCE_NOT_FOUND`
- `409 FINANCE_DUPLICATE_SOURCE`, `FINANCE_PERIOD_CLOSED`, `FINANCE_CONCURRENCY_CONFLICT`, `FINANCE_ALREADY_POSTED`
- `422 FINANCE_MIGRATION_EXCEPTIONS_REMAIN`, `FINANCE_RECONCILIATION_MISMATCH`

Retries with the same idempotency key return the canonical result. The same key with different payload returns conflict. No endpoint updates or deletes a posted journal.
