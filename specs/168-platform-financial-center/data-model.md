# Data Model: Platform Financial Center

## Core ledger

- `FinancialAccount`: `Id`, `Code` unique, Arabic/English name, `Type` (Asset/Liability/Equity/Revenue/ContraRevenue/Expense), parent, normal side, control role, active flag.
- `JournalEntry`: immutable posted header with number, occurrence/posting timestamps, source type/ID, idempotency key unique, description, actor, correlation, migration batch, reversal links, status.
- `JournalLine`: entry, account, debit, credit, teacher/student/treasury/category/cost-center dimensions, memo. Exactly one positive side; entry debit sum equals credit sum.
- `AccountingPeriod`: start/end unique non-overlap, `Open|Closed|Reopened`, close/reopen actor/time/reason and concurrency token.

## Treasury and operating documents

- `TreasuryAccount`: `DigitalWallet|Cashbox|BankAccount`, mapped financial account, optional existing wallet ID, display name, masked identifier, opening date, active flag.
- `TreasuryTransfer`: source/destination, amount, date, reference, attachment, `Draft|Posted|Reversed`, journal link.
- `TreasuryReconciliation`: treasury, range, system/count/statement balances, variance, adjustment journal, evidence, actor, status.
- `ExpenseCategory`: hierarchical category and mapped expense account.
- `CostCenter`: reporting dimension, active dates.
- `Vendor`: name/contact/tax metadata optional, active flag.
- `PlatformExpense`: number, vendor/beneficiary, category, cost center, occurrence date, amount, description, recurring metadata, attachments, `Draft|PostedUnpaid|PartiallyPaid|Paid|Reversed`, payable/journal links and concurrency token.
- `ExpensePayment`: expense, treasury, amount, date, reference, attachment, journal link; cumulative payments cannot exceed payable amount.

## Refunds and budgets

- `RefundRequest`: original source/grant/purchase, student, teacher/content dimensions, requested/posted amount, `StudentBalance|Cash`, target balance scope or treasury, reason, `Draft|Posted|PartiallyPosted|Reversed`, idempotency key and journal links.
- `RefundPayment`: cash-payment evidence or balance-credit reference, amount/date/actor/journal.
- `BudgetPlan`: name, `Week|Month|Year|Custom`, start/end, version, status `Draft|Active|Archived`, owner.
- `BudgetLine`: plan, account/category, optional cost center/teacher, planned amount. Actual is derived, never stored as financial truth.

## Migration and projections

- `FinancialMigrationBatch`: source range, dry-run/final mode, counts/totals, checksum, status, actor/timestamps.
- `FinancialMigrationItem`: source key, proposed/posting result, journal link, checksum.
- `FinancialMigrationException`: source key, reason code, evidence, resolution, resolver, optional approved suspense decision.
- `FinancialProjectionCheckpoint`: projection name, last journal sequence, rebuilt timestamp/version.

## Existing source relationships

`RechargeRequest`, incoming wallet evidence, balance transactions, sales effects, teacher events/allocations, settlements/payments/payouts, cancellations, payroll, and usable invoices link to journals by source type/ID without losing their current lifecycle authority.

## Posting templates

| Event | Debit | Credit |
|---|---|---|
| Recharge | Treasury | Student balance liability (general/teacher) |
| Purchase | Student liability | Teacher payable + platform revenue |
| Teacher payout | Teacher payable | Treasury |
| Paid expense | Expense | Treasury |
| Unpaid expense | Expense | Supplier payable |
| Supplier payment | Supplier payable | Treasury |
| Balance refund | Platform contra-revenue + teacher payable/debt | Student liability |
| Cash refund | Platform contra-revenue + teacher payable/debt | Treasury |
| Payroll payment | Payroll expense | Treasury |
| Treasury transfer | Destination treasury | Source treasury |

Exact refund debit allocation is capped by the remaining refundable platform/teacher shares and preserves paid-teacher debt handling.

## Constraints and indexes

- Unique `(SourceType, SourceId, PostingKind)` and `IdempotencyKey`.
- Check nonnegative money, debit XOR credit, positive document amounts, valid date ranges, distinct transfer accounts, and teacher required only for teacher scope.
- Index journal occurrence/posting dates, account/date/sequence, source, teacher, student, treasury, category/cost center, status, and migration batch.
- Posted rows are application-immutable; database permissions/triggers may enforce journal immutability after rollout validation.
- Refund sum cannot exceed original refundable amount; expense payments cannot exceed expense payable; entry posting and source mutation share a transaction.

## State transitions

- Expense: `Draft → PostedUnpaid/PartiallyPaid/Paid → Reversed`; only drafts edit in place.
- Refund: `Draft → Posted/PartiallyPosted → Reversed`; posted correction is reversal.
- Period: `Open → Closed → Reopened → Closed`; every transition audited.
- Migration: `Prepared → DryRun → ExceptionsResolved → Posted → Reconciled`; failed batches do not partially post.
- Reconciliation: `Draft → Completed`; correction creates linked adjustment/reversal, never overwrites evidence.
