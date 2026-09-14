# Data Model: Phase 1 Sales and Content Completion

## Enums

### SalesTargetType

`Package`, `Term`, `ContentSection`, `Lesson`, `SpecificVideo`, `VideoType`, `PublicExam`, `Teacher`, `Platform`

### DiscountType

`Percentage`, `FixedAmount`

### SalesOwnerType

`Platform`, `Teacher`

### SalesStatus

`Draft`, `Active`, `Disabled`, `Expired`, `Archived`

### StackingMode

`SingleOnly`, `AllowCouponAndPrintedCode`, `AllowMultipleWithCap`

### PrintableCodeBehavior

`Discount`, `DirectAccess`, `PromotionalCredit`

## SalesRule

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| TargetType | SalesTargetType | Required |
| TargetId | UUID? | Required except Platform and Teacher-wide targets |
| TeacherId | UUID? | Required for teacher-owned rule; resolved for content targets |
| SubjectId | UUID? | Required where classification is available |
| GradeLevel | string? | Optional classification |
| VideoTypeId | UUID? | Required for VideoType target |
| IsActive | bool | Default true |
| CreatedByUserId | UUID | Required |
| CreatedAt/UpdatedAt | timestamptz | UTC |

Unique active rule should prevent conflicting duplicate target/scope rows.

## DiscountStackingPolicy

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| Name | varchar(120) | Required, unique normalized |
| Mode | StackingMode | Required |
| MaxDiscountPercentage | decimal? | 0-100 when present |
| MaxDiscountAmount | decimal? | Positive when present |
| PriorityJson | jsonb | Ordered source preference: coupon, printableCode, promotional |
| IsDefault | bool | At most one active default |
| IsActive | bool | Default true |
| CreatedByUserId | UUID | Required |

Default row: `SingleOnly`.

## SalesCoupon

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| Code | varchar(80) | Required; stored display value |
| NormalizedCode | varchar(80) | Required; unique |
| Name | varchar(160) | Required |
| DiscountType | DiscountType | Required |
| DiscountValue | decimal(18,2) | Positive; percentage <= 100 |
| TargetType | SalesTargetType | Required |
| TargetId | UUID? | Required when target-specific |
| OwnerType | SalesOwnerType | Required |
| TeacherId | UUID? | Required when OwnerType=Teacher |
| StackingPolicyId | UUID? | Optional; falls back to default policy |
| StartsAt/ExpiresAt | timestamptz? | Optional date window |
| GlobalUsageLimit | int? | Positive |
| PerStudentUsageLimit | int? | Positive |
| UsedCount | int | Non-negative, <= GlobalUsageLimit when present |
| Status | SalesStatus | Draft/Active/Disabled/Expired/Archived |
| DisableReason | varchar(500)? | Required when disabled |
| CreatedByUserId | UUID | Required |
| CreatedAt/UpdatedAt | timestamptz | UTC |

## SalesCouponUsage

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| CouponId | UUID | Required FK |
| StudentId | UUID | Required FK User |
| PurchaseOperationId | UUID | Required idempotency key |
| TargetType | SalesTargetType | Required |
| TargetId | UUID | Required |
| GrossAmount | decimal(18,2) | Non-negative |
| DiscountAmount | decimal(18,2) | Positive |
| CreatedAt | timestamptz | UTC |

Unique indexes:

- `(CouponId, PurchaseOperationId)`
- `(CouponId, StudentId, PurchaseOperationId)`

## PrintableCodeBatch

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| Name | varchar(160) | Required |
| Behavior | PrintableCodeBehavior | Required |
| DiscountType/DiscountValue | nullable | Required only for Discount behavior |
| CreditAmount | decimal? | Required only for PromotionalCredit behavior |
| TargetType | SalesTargetType | Required |
| TargetId | UUID? | Required when target-specific |
| OwnerType | SalesOwnerType | Required |
| TeacherId | UUID? | Required when owner teacher |
| TemplateId | UUID? | Optional until export |
| StackingPolicyId | UUID? | Optional |
| TotalCodes | int | 1-10000 |
| UsedCount | int | Non-negative |
| StartsAt/ExpiresAt | timestamptz? | Optional |
| Status | SalesStatus | Draft/Active/Disabled/Expired/Archived |
| DisableReason | varchar(500)? | Required when disabled |
| CreatedByUserId | UUID | Required |

## PrintableSalesCode

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| BatchId | UUID | Required FK |
| CodeHash | varchar(256) | Required unique |
| CodePlaintext | varchar(80)? | Present until first export or admin display policy |
| SerialNumber | bigint | Required unique |
| QrPayload | varchar(500) | Required |
| UsedCount | int | Non-negative |
| UsageLimit | int | Default 1 |
| ConsumedByUserId | UUID? | Present when single-use consumed |
| ConsumedAt | timestamptz? | Present when consumed |
| Status | SalesStatus | Active/Disabled/Expired/Archived |

## PrintableCodeRedemption

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| PrintableCodeId | UUID | Required FK |
| StudentId | UUID | Required |
| RequestId | UUID | Required idempotency key |
| PurchaseOperationId | UUID? | Present for checkout-bound discount |
| TargetType/TargetId | required | Resolved redemption target |
| AppliedAmount | decimal(18,2) | Non-negative |
| CreatedAt | timestamptz | UTC |

Unique `(PrintableCodeId, RequestId)` and single-use guard when `UsageLimit = 1`.

## PrintableCodeTemplate

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| Name | varchar(160) | Required |
| WidthMm/HeightMm | decimal | Positive |
| BackgroundColor | varchar(32)? | Optional |
| BackgroundImageUrl | string? | Optional |
| LayoutJson | jsonb | Required; fixed allowed elements only |
| IsActive | bool | Default true |
| CreatedByUserId | UUID | Required |

Validation:

- Layout must include `qr` or `code`.
- Elements must stay inside printable bounds.
- Element keys must be from the approved fixed list.

## PublicExamProduct

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| ExamId | UUID | Required unique FK |
| Slug | varchar(160) | Required unique |
| IsPublished | bool | Default false |
| IsPaid | bool | Required |
| Price | decimal(18,2) | 0 for free, positive for paid |
| TeacherId | UUID? | Optional unless teacher-scoped |
| SubjectId | UUID? | Optional unless subject-scoped |
| GradeLevel | string? | Optional |
| IsPlatformWide | bool | Default false |
| AvailableFrom/AvailableUntil | timestamptz? | Optional |
| DisabledAt | timestamptz? | Blocks new starts/purchases |
| DisabledByUserId | UUID? | Required if disabled |
| DisableReason | varchar(500)? | Required if disabled |
| CreatedByUserId | UUID | Required |

## StudentAccessGrant Changes

Existing `StudentAccessGrant.ExamId` is reused for paid/free public exam access. Add optional `PublicExamProductId` only if needed to distinguish public product grants from lesson/video exam grants in queries.

## SalesFinancialEffect

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Primary key |
| PurchaseOperationId | UUID | Required unique per purchase |
| StudentId | UUID | Required |
| TargetType/TargetId | required | Purchased item |
| GrossAmount | decimal(18,2) | Non-negative |
| CouponDiscountAmount | decimal(18,2) | Non-negative |
| PrintableCodeDiscountAmount | decimal(18,2) | Non-negative |
| PromotionalAmount | decimal(18,2) | Non-negative |
| PaidAmount | decimal(18,2) | Non-negative |
| TeacherId | UUID? | Nullable for platform-only |
| TeacherShareImpact | decimal(18,2) | Non-negative |
| PlatformShareImpact | decimal(18,2) | Non-negative |
| DetailsJson | jsonb | Source ids, split rates, policy id |

Check: `GrossAmount >= CouponDiscountAmount + PrintableCodeDiscountAmount + PromotionalAmount + PaidAmount` is not always true if paid amount is remainder; exact formula is captured in service invariant: `GrossAmount = TotalDiscount + PromotionalAmount + PaidAmount`.

## State Transitions

### Coupon / Batch

```text
Draft -> Active -> Disabled
Draft -> Active -> Expired
Draft -> Archived
Disabled/Expired -> Archived
```

### Printable Code

```text
Active -> Consumed (when UsedCount reaches UsageLimit)
Active -> Disabled
Active -> Expired
```

### Public Exam Product

```text
Draft -> Published
Published -> Disabled
Published -> Expired by availability window
```

Disabled public exam blocks new purchases and attempts but keeps previous attempts/results.

## Migration Notes

- Add new tables and indexes without deleting legacy `CodeGroup`.
- Add public exam product FK/indexes without changing existing lesson/video exam relationships.
- Use check constraints for non-negative monetary fields and valid percentage ranges.
- Seed default discount stacking policy: `SingleOnly`.
- Existing data needs no destructive backfill.
