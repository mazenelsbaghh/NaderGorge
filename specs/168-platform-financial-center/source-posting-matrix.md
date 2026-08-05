# Source posting matrix

| Source event | Debit | Credit | Idempotency key | Notes |
|---|---|---|---|---|
| Approved general recharge | Wallet treasury | `1100` general student liability | `recharge:{id}:approved` | No teacher is required |
| Approved teacher recharge | Wallet treasury | `1110` teacher-scoped student liability | `recharge:{id}:approved` | `TeacherId` is preserved on the line |
| Paid purchase | `1100` | `4000` platform revenue + `2000` teacher payable | `purchase:{operationId}` | Negative share is represented as a debit line |
| Teacher-scoped promotional purchase | `1110` | `2000` teacher payable | `purchase:{operationId}` | Moves the teacher-specific liability into teacher payable |
| Platform expense paid immediately | Expense category | Treasury | `ExpensePost` request key | Posted document is immutable |
| Platform expense payable payment | `2100` supplier payable | Treasury | Payment request key | Overpayment is rejected |
| Balance refund | Refunds + teacher payable | `1100` | Refund request key | Also credits the operational student balance |
| Cash refund | Refunds + teacher payable | Treasury | Refund request key | Treasury account is required |
| Treasury transfer | Destination treasury | Source treasury | Transfer request key | Asset-to-asset only |

Historical operations use the same account templates and journal idempotency keys as live operations.
