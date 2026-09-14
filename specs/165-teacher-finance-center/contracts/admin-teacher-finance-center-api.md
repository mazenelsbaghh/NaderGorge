# Admin Teacher Finance Center API Contract

All routes require authenticated Admin authorization. Non-admin callers receive `403` and no financial payload.

## Agreements

- `GET /api/admin/teacher-finance-center/teachers/{teacherId}/agreements`
- `POST /api/admin/teacher-finance-center/teachers/{teacherId}/agreements`
- `PUT /api/admin/teacher-finance-center/agreements/{agreementId}`

Request includes scope, trigger, allocation mode/value, price basis, effective dates and reason. Invalid dates, overlap, negative values, percentage over 100, or an incompatible scope return validation errors. Editing an agreement creates a new effective version instead of changing allocations already recorded.

## Financial dashboard and ledger

- `GET /api/admin/teacher-finance-center/dashboard?from&to&teacherId&targetType&targetId`
- `GET /api/admin/teacher-finance-center/teachers/{teacherId}/ledger?from&to&status&page&pageSize`
- `GET /api/admin/teacher-finance-center/teachers/{teacherId}/summary`

Responses expose EGP cash/receivable/payable values separately from `bunnyCostUsd`, `isBandwidthEstimated`, `bandwidthSource` and missing-data metadata.

## Code batch financial trigger

- `PUT /api/admin/teacher-finance-center/code-groups/{codeGroupId}/financial-terms`
- `POST /api/admin/teacher-finance-center/code-groups/{codeGroupId}/confirm-delivery`

Delivery confirmation requires recipient and optionally attachment. A repeated confirmation returns the original result or conflict, and must never create a second due. A group configured for delivery never creates a due again at activation.

## Shared package preview and acknowledgement

- `POST /api/admin/teacher-finance-center/shared-packages/{id}/allocation-preview`
- Existing purchase flow accepts `confirmLoss: true` only after preview reports `requiresLossAcknowledgement`.

If teacher allocation exceeds sale basis, server returns `409 FINANCE_LOSS_CONFIRMATION_REQUIRED` with total teacher share and negative platform share. The explicit confirmation is auditable.

## Settlements and invoices

- `POST /api/admin/teacher-finance-center/settlements/preview`
- `POST /api/admin/teacher-finance-center/settlements`
- `POST /api/admin/teacher-finance-center/settlements/{id}/review`
- `POST /api/admin/teacher-finance-center/settlements/{id}/approve`
- `POST /api/admin/teacher-finance-center/settlements/{id}/pay`
- `POST /api/admin/teacher-finance-center/settlements/{id}/cancel`
- `POST /api/admin/teacher-finance-center/invoices/{id}/attachments`

Only Draft → Reviewed → Approved → Paid transitions are valid. Pay requires payment method/reference and records exactly the reserved settlement lines. Cancel is forbidden after Paid and releases unresolved reservations.

## Selected-line reversal

`POST /api/admin/teacher-finance-center/reversals` accepts original allocation IDs, amount per selected line, reason, and disposition `TeacherDebt` or `NextSettlementDeduction`. It rejects non-positive amounts, lines belonging to another sale, amounts above remaining reversible value, or completed reversals.

## Bunny reporting

- `POST /api/admin/teacher-finance-center/bunny/sync`
- `GET /api/admin/teacher-finance-center/bunny/costs?from&to&teacherId&packageId&lessonId`

Sync failures retain last successful snapshots and return a user-safe error. Reports never label estimated or unavailable data as actual.
