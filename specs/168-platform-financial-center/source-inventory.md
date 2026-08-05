# Historical finance source inventory

| Source | Authoritative amount | Scope | Posting status |
|---|---:|---|---|
| `recharge_requests` | `Amount` | `WalletId`, optional `TeacherId`, `UserId` | Approved rows are eligible for repeat-safe reconstruction; rejected/pending rows are ignored |
| `sales_financial_effects` | `PaidAmount` | `StudentId`, optional `TeacherId`, purchase operation | Eligible when paid and not already journaled |
| `balance_transactions` | Balance movement | Student balance only | Kept as operational evidence; not reposted automatically because it has no authoritative wallet/source mapping |
| `teacher_financial_events` | Teacher allocation | Teacher subledger | Read-only reconciliation source; no duplicate GL posting in this phase |
| `platform_expenses` | Expense/payment documents | Platform cost center/vendor/treasury | Created directly in the finance center |
| `platform_refunds` | Platform + teacher portions | Purchase source/student/treasury | Created directly in the finance center; source and remaining amount are validated |

Rows without a reliable amount, source identity, or wallet mapping remain exceptions. The historical migration endpoint reports them instead of inventing a general-platform movement.
