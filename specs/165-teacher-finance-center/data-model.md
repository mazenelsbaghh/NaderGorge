# Data Model: مركز حسابات المدرسين والمالية

## TeacherFinancialAgreement

Effective-dated source of teacher entitlement terms.

| Field | Rules |
|---|---|
| `TeacherId` | Required teacher profile owner. |
| `ScopeType`, `ScopeId` | Null scope is the teacher default; otherwise Package, Term, ContentSection, Lesson, LessonVideo, PublicExam, SharedPackage, or CodeGroup. |
| `SettlementTrigger` | `ContentSale`, `CodeDelivery`, or `CodeActivation`. |
| `AllocationMode` | `Percentage`, `FixedPerSale`, `FixedPerCode`, or `FixedPerBatch`. |
| `AllocationValue` | Non-negative. Percentage is 0–100. |
| `PriceBasis` | Gross or net sale basis. |
| `EffectiveFrom`, `EffectiveTo` | No overlapping active terms for the same teacher, scope and trigger. |
| Audit fields | Creator/updater, reason, timestamps and active state. |

Resolution precedence is video/lesson item, then section, term, package, then teacher default. The selected agreement is snapshotted on every new allocation.

## TeacherFinancialEvent and TeacherFinancialAllocation extensions

Keep existing entities as the authoritative ledger. Add immutable snapshot data:

- Agreement reference and resolved scope/mode/value/basis.
- Gross price, total discount, platform-borne discount, teacher-borne discount, split amount, paid amount, promotional amount and platform share.
- Stable source trigger key that is unique for a teacher/source/trigger.
- Event/allocation state for eligibility, settlement reservation, payment, reversal and debt.

Each purchase, delivery confirmation, activation, compensation and reversal has one stable idempotency key. A wallet recharge does not create an event or allocation.

## CodeGroup financial terms and delivery confirmation

`CodeGroupFinancialTerms` stores settlement trigger and optional agreement override. `CodeGroupDeliveryConfirmation` stores code group, confirmed by, confirmed at, recipient/teacher, optional attachment, and immutable source key. One confirmed delivery is allowed per financial batch/trigger.

## TeacherSettlement, TeacherSettlementLine and settlement payment

| Entity | Purpose |
|---|---|
| `TeacherSettlement` | Teacher, period, currency EGP, state Draft → Reviewed → Approved → Paid or Cancelled, totals and audit fields. |
| `TeacherSettlementLine` | One selected allocation/adjustment, signed amount, reservation state and snapshot label. Unique for a live settlement line per source. |
| `TeacherSettlementPayment` | Payment method, transfer reference, optional attachment, paid actor/time and amount. |

Draft creation reserves eligible lines atomically. Approved/paid transitions require no duplicate reservation. Cancelling releases only unpaid reservations.

## Debt and reversal

Retain `TeacherPayoutAdjustment` and extend it with disposition `TeacherDebt` or `NextSettlementDeduction`, selected original allocation links, reason, actor and state. Partial refund operations select exact allocation lines and create immutable reversal records; they never overwrite original events.

## FinancialInvoice and FinancialExpense

`FinancialInvoice` represents teacher settlements, production expenses, Bunny/hosting/bandwidth expenses, and general expenses. It has document number, owner/reference, amount, currency, status Draft → Reviewed → Approved → Paid or Cancelled, attachments and audit metadata. EGP invoices and USD Bunny invoices remain separate by currency.

## Bunny cost reporting

Reuse `BunnyVideoAsset` and `BunnyUsageSnapshot` as the source per video and period. Rollups join Video → Lesson → ContentSection → Term → Package and shared-package item references. Output `bunnyCostUsd`, `BandwidthSource`, `IsBandwidthEstimated`, missing-data count and latest synchronization timestamp. A rollup must not multiply the same video cost within one aggregation level.

## Migration and legacy data

- Retain current financial events, allocations, payouts, adjustments and Bunny snapshots unchanged.
- Create nullable agreement snapshot columns for legacy rows.
- Create opening settlement lines only for confirmed aggregate payouts whose source allocations cannot be reliably reconstructed.
- Use a cutover date/feature setting: new writes use agreements and settlement lines; legacy reports remain readable.
