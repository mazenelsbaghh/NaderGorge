# Contract: Teacher Accounting API

## Teacher Endpoints

### GET `/api/teacher/finance/account`

Returns teacher account totals.

Response data:

```json
{
  "teacherId": "guid",
  "teacherName": "string",
  "todayEarnings": 120.0,
  "totalEarnings": 5000.0,
  "currentBalance": 1500.0,
  "reservedBalance": 300.0,
  "availableBalance": 1200.0,
  "debtBalance": 0.0,
  "commissionRate": 0.15
}
```

### GET `/api/teacher/finance/calendar?from=2026-07-01&to=2026-07-31`

Returns daily buckets for the teacher.

Response data item:

```json
{
  "date": "2026-07-04",
  "grossAmount": 700.0,
  "teacherShareAmount": 105.0,
  "platformShareAmount": 595.0,
  "transactionCount": 4,
  "pendingReviewCount": 1
}
```

### GET `/api/teacher/finance/transactions`

Query parameters:
- `page`, `pageSize`.
- `date`: optional day filter.
- `from`, `to`: optional date range.
- `status`: optional review/payout status.

Response data item:

```json
{
  "id": "guid",
  "occurredAt": "2026-07-04T10:15:00Z",
  "sourceType": "DirectPurchase",
  "contentName": "Lesson 1",
  "studentName": "Student Name",
  "studentPhone": "01000000000",
  "codeSerialNumber": 1000028,
  "grossAmount": 200.0,
  "discountAmount": 50.0,
  "paidAmount": 150.0,
  "teacherShareAmount": 22.5,
  "platformShareAmount": 127.5,
  "allocationMode": "CommissionRate",
  "allocationValue": 0.15,
  "reviewStatus": "Approved",
  "payoutStatus": "Unpaid"
}
```

Teacher-visible student fields are limited to the user-approved name, phone, and content/code context.

### POST `/api/teacher/finance/payouts`

Request:

```json
{
  "amount": 500.0
}
```

Creates a pending payout and reserves available balance.

### GET `/api/teacher/finance/payouts`

Returns payout history with statuses: `Pending`, `Approved`, `Paid`, `Rejected`.

## Admin Endpoints

### GET `/api/admin/finance/teacher-events`

Query parameters:
- `teacherId`, `studentId`, `sourceType`, `reviewStatus`, `payoutStatus`, `from`, `to`, `page`, `pageSize`.

Returns financial events and allocations for review.

### POST `/api/admin/finance/teacher-events/{id}/review`

Request:

```json
{
  "status": "Approved",
  "note": "Checked against purchase and teacher ownership"
}
```

Allowed statuses: `Approved`, `Rejected`, `PendingReview`.

### GET `/api/admin/finance/teacher-payouts`

Query parameters: `status`, `teacherId`, `from`, `to`.

Returns payout requests with teacher account balance context and linked allocations.

### POST `/api/admin/finance/payouts/{id}/approve`

Moves a pending payout to approved/ready-for-transfer. Reserved balance remains held.

### POST `/api/admin/finance/payouts/{id}/mark-paid`

Request:

```json
{
  "transferReference": "bank-transfer-123",
  "adminNote": "Transferred manually"
}
```

Marks an approved payout as paid after the real transfer and deducts current/reserved balances.

### POST `/api/admin/finance/payouts/{id}/reject`

Request:

```json
{
  "rejectionReason": "Missing payout details"
}
```

Rejects a pending payout and releases reserved balance.
