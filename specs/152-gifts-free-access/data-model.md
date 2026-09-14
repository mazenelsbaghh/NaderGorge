# Data Model: Gifts and Free Access

## GiftIssuance

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| RequestId | UUID | Required, unique idempotency key |
| TargetType | GiftTargetType | Package, Lesson, Video, Exam, GeneralBalance, TeacherBalance |
| PackageId/LessonId/LessonVideoId/ExamId | UUID? | Exactly one for matching content target; null for balance |
| TeacherId | UUID? | Required only for TeacherBalance |
| Amount | decimal(18,2)? | Positive only for balance targets |
| ExpiresAt | timestamptz? | Future at creation |
| MaxUses | int? | Positive; only Video, Exam, or balance targets |
| Reason | varchar(500) | Required administrative reason |
| IssuedByUserId | UUID | FK User, immutable |
| Status | GiftIssuanceStatus | Active, PartiallySuccessful, Completed, Expired, Revoked |
| CreatedAt/UpdatedAt | timestamptz | UTC |

Validation constraint: target discriminator and nullable foreign keys must agree. Amount is present only for balance targets. Recipient count is derived from rows.

## GiftRecipient

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| GiftIssuanceId | UUID | FK cascade restricted; unique with StudentId |
| StudentId | UUID | FK User |
| Status | GiftRecipientStatus | Granted, AlreadyEntitled, Failed, Active, PartiallyUsed, Completed, Expired, Revoked |
| OutcomeCode | varchar(80) | Stable machine-readable result |
| OutcomeMessage | varchar(500)? | Safe Admin explanation |
| AccessGrantId | UUID? | Unique link for content gifts |
| UsesConsumed | int | Non-negative |
| RevokedAt/RevokedByUserId/RevocationReason | nullable | Reason required when revoked |
| CreatedAt/UpdatedAt | timestamptz | UTC |

Unique index: `(GiftIssuanceId, StudentId)`.

## StudentAccessGrant Changes

| Field | Type | Rules |
|---|---|---|
| GiftRecipientId | UUID? | Unique FK to GiftRecipient |
| MaxUses | int? | Positive when present |
| UsesConsumed | int | Non-negative and no greater than MaxUses |

The existing `ExpiresAt`, `IsActive`, and cancellation fields remain authoritative. Non-gift grants keep `GiftRecipientId = null` and existing behavior.

## PromotionalBalanceAllocation

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| GiftRecipientId | UUID | Required unique FK |
| StudentId | UUID | Required FK/index |
| TeacherId | UUID? | Null means general; value means restricted |
| OriginalAmount | decimal(18,2) | Positive |
| AvailableAmount | decimal(18,2) | Non-negative |
| ConsumedAmount | decimal(18,2) | Non-negative |
| ExpiredAmount | decimal(18,2) | Non-negative |
| RevokedAmount | decimal(18,2) | Non-negative |
| ExpiresAt | timestamptz? | Optional |
| MaxPurchaseCount | int? | Positive when present |
| PurchaseCount | int | Non-negative and <= cap |
| Status | PromotionalBalanceStatus | Active, PartiallyUsed, Consumed, Expired, Revoked |
| CreatedAt/UpdatedAt | timestamptz | UTC |

Conservation check:

```text
OriginalAmount = AvailableAmount + ConsumedAmount + ExpiredAmount + RevokedAmount
```

Eligibility index: `(StudentId, TeacherId, Status, ExpiresAt)` with positive available amount filtered where supported.

## PromotionalBalanceUsage

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| AllocationId | UUID | Required FK |
| GiftRecipientId | UUID | Required FK/evidence |
| PurchaseOperationId | UUID | Required |
| ContentType | PurchasableContentType | Existing supported purchase type |
| ContentId | UUID | Required |
| Amount | decimal(18,2) | Positive |
| CreatedAt | timestamptz | UTC, immutable |

Unique index: `(PurchaseOperationId, AllocationId)` prevents duplicate allocation consumption on a replay.

## State Transitions

### Direct Content Recipient

```text
Granted/Active -> PartiallyUsed -> Completed
Granted/Active/PartiallyUsed -> Expired
Granted/Active/PartiallyUsed -> Revoked
```

AlreadyEntitled and Failed are terminal issuance outcomes and create no new access value.

### Promotional Allocation

```text
Active -> PartiallyUsed -> Consumed
Active/PartiallyUsed -> Expired
Active/PartiallyUsed -> Revoked
```

Expiration moves all `AvailableAmount` to `ExpiredAmount`. Revocation moves all `AvailableAmount` to `RevokedAmount`. Consumed value never moves backward.

## Relationship Summary

```text
GiftIssuance 1 ── * GiftRecipient
GiftRecipient 1 ── 0..1 StudentAccessGrant
GiftRecipient 1 ── 0..1 PromotionalBalanceAllocation
PromotionalBalanceAllocation 1 ── * PromotionalBalanceUsage
User(Student) 1 ── * GiftRecipient/PromotionalBalanceAllocation
User(Issuer) 1 ── * GiftIssuance
Teacher 1 ── * PromotionalBalanceAllocation (restricted only)
```

## Migration and Backfill

- Add new tables, enums stored as integers, foreign keys, indexes, and check constraints.
- Add nullable gift fields to `StudentAccessGrant` with `UsesConsumed = 0` default.
- Existing access grants and paid balances require no backfill.
- Migration down path removes new links before new tables; no production data transformation is implied.
