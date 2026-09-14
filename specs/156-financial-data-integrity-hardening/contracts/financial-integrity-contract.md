# Financial Integrity Contract

## Student Recharge Resolution

### Success

- A pending recharge can become `Matched` or `Approved`.
- Exactly one `BalanceTransaction` with `TransactionType = DigitalRecharge` and `ReferenceId = RechargeRequest.Id` exists after successful credit.
- If an SMS is linked, the SMS is `IsMatched = true` and references the recharge request.

### Conflict/Failure

- Re-resolving a non-pending recharge returns failure and creates no new credit.
- Reusing a matched SMS returns failure and creates no new credit.
- Database unique/check/serialization conflicts return a controlled `409 Conflict` at HTTP boundary if not handled earlier.

## Teacher Payout

### Request

- Request amount must be greater than zero.
- Request amount must be less than or equal to `CurrentBalance - ReservedBalance`.
- Successful request creates a pending payout and increments `ReservedBalance`.

### Resolve Paid

- Only pending payouts can be paid.
- Payment decrements both `CurrentBalance` and `ReservedBalance` by the payout amount.

### Resolve Rejected

- Only pending payouts can be rejected.
- Rejection requires reason and decrements `ReservedBalance` by the payout amount.

## Access Grant

- Grant target fields must match `GrantType`.
- Active duplicate grant rows for the same student/source/target are rejected.
- Inactive/cancelled history remains queryable and is not cascaded away by deleting referenced records.
