# Research: Platform Financial Center

## Decisions

### Balanced general ledger, not dashboard-only aggregates

Use immutable double-entry journals because cash, unused student money, teacher obligations, refunds, and expenses cannot be safely distinguished from mutable aggregate queries. Direct aggregates were rejected because corrections and historical audit would drift.

### Existing domains remain authoritative

Recharge, sales/entitlements, teacher finance, payroll, and cancellation keep their business state. A common posting service mirrors their financial effect exactly once. Replacing them with the ledger was rejected as an unnecessarily risky rewrite.

### Recognition and posting rules

- Recharge: debit selected treasury; credit general or teacher-scoped student liability. It is not revenue.
- Purchase: debit student liability; credit teacher payable and platform revenue using the locked allocation.
- Teacher payment: debit teacher payable; credit treasury.
- Paid expense: debit expense; credit treasury. Unpaid expense credits supplier payable, later cleared on payment.
- Balance refund: reverse the sale and credit student liability; treasury is unchanged.
- Cash refund: reverse the sale and credit treasury; already-paid teacher share becomes teacher debt/settlement adjustment.
- Payroll payment: debit payroll expense; credit treasury.
- Treasury transfer: debit destination asset; credit source asset; no profit effect.

### Recharge scope

Support both `General` and `Teacher`. The scope and teacher ID are explicit and immutable after proof submission. This avoids silently attributing money to the wrong teacher while preserving the confirmed general-wallet use case.

### Treasury abstraction

Model digital wallets, physical cashboxes, and bank accounts uniformly, each mapped to one GL asset account. This allows cash refunds and reconciliation without special-case accounting.

### Permissions without mandatory approval

Use granular existing permissions for view/create/post/reverse/export/close/reopen. Expenses do not require an approval chain, but posted mutations require actor, reason where relevant, audit record, and reversal rather than deletion.

### Flexible budgets

Store versioned plans with arbitrary date boundaries and a convenience period kind. Actuals are computed from posted journal dimensions, so weekly, monthly, yearly, and custom periods share one model.

### Historical reconstruction

Use source adapters, deterministic keys, dry-run totals, an exception queue, and repeat-safe batches. Preserve original dates and source links. Ambiguous data is excluded until resolved or placed in explicitly approved opening suspense; it is never guessed as income.

### Period close

Closed occurrence dates reject posting. Reopening requires a dedicated permission and reason. Later corrections normally post in an open period and link to the original.

### Exports and performance

Excel/PDF are generated from the same server-side filtered query contract as the UI. Aggregated projections and indexed cursor/page queries prevent unbounded scans. Core postings stay synchronous and transactional; only unusually large export generation may become background work.

### Precision, retries, and concurrency

Use C# `decimal` and PostgreSQL `numeric(18,2)`, database balance/check constraints, unique source idempotency keys, optimistic concurrency on mutable drafts, and serializable/advisory locking where allocation/refund limits demand it. Floating point and client-calculated totals are rejected.

## Resolved Questions

Currency is EGP; tax is out of scope; cash exists; refunds support cash and balance; expenses are separate from teacher dues; admin and selected staff operate the module; no mandatory expense approval exists; all trustworthy history is included; Excel and PDF are required. No planning clarification remains.
