# Data Model: Teacher Accounting Phase 3

## TeacherFinancialEvent

- `Id`: unique event id.
- `SourceType`: enum, e.g. `AccessCodeActivation`, `DirectPurchase`, `PublicExamPurchase`, `SharedPackagePurchase`, `Refund`, `Cancellation`, `ManualCompensation`, `ManualAdjustment`.
- `SourceId`: source operation id or entity id.
- `StudentId`: optional user id for student-visible operations.
- `TargetType`: purchased/granted target type.
- `TargetId`: purchased/granted target id.
- `GrossAmount`: original listed amount, non-negative.
- `DiscountAmount`: total discount, non-negative.
- `PaidAmount`: amount paid by student/wallet, non-negative.
- `PromotionalAmount`: promotional/internal funding amount, non-negative.
- `PlatformShareAmount`: platform share after teacher allocations.
- `Currency`: default `EGP`.
- `ReviewStatus`: `AutoApproved`, `PendingReview`, `Approved`, `Rejected`, `Reversed`.
- `PayoutInclusionStatus`: `NotEligible`, `Eligible`, `Reserved`, `Paid`, `Reversed`.
- `OccurredAt`: event time used for calendar grouping.
- `IdempotencyKey`: unique stable key per source operation.
- `DetailsJson`: source metadata.

## TeacherFinancialAllocation

- `Id`: unique allocation id.
- `TeacherFinancialEventId`: parent event.
- `TeacherId`: teacher profile id.
- `AllocationMode`: `CommissionRate`, `Percentage`, `FixedAmount`, `ManualCompensation`, `Reversal`.
- `AllocationValue`: percentage/rate/fixed configured value.
- `GrossBasisAmount`: amount used to calculate the share.
- `TeacherShareAmount`: signed amount; positive for earnings/compensation, negative for reversals/debt.
- `PlatformShareAmount`: platform remainder attributed alongside this allocation when applicable.
- `StudentNameSnapshot`: name at operation time for teacher transaction view.
- `StudentPhoneSnapshot`: phone at operation time for teacher transaction view.
- `ContentNameSnapshot`: product/content label at operation time.
- `CodeSerialNumber`: optional printable/access code serial.
- `ReviewStatus`: mirrors or narrows event review status.
- `PayoutStatus`: `Unpaid`, `Reserved`, `Paid`, `Reversed`, `Debt`.
- `PayoutId`: optional payout that reserved/paid this allocation.

## TeacherAccount

- Existing fields remain: `TeacherId`, `TotalEarnings`, `CurrentBalance`, `ReservedBalance`, `CommissionRate`, `Version`.
- Derived values:
  - `AvailableBalance = CurrentBalance - ReservedBalance`.
  - `DebtBalance` may be stored or computed from negative unpaid allocations.
- Validation:
  - balances are non-negative unless a separate debt field is introduced.
  - reserved balance cannot exceed current balance.
- Mutation rule: only the teacher accounting service updates balances.

## TeacherPayout

- Existing fields remain: `TeacherId`, `Amount`, `Status`, `RejectionReason`, `HandledByUserId`, `HandledAt`.
- Added/changed fields:
  - `ApprovedByUserId`, `ApprovedAt`.
  - `PaidByUserId`, `PaidAt`.
  - `TransferReference`.
  - `AdminNote`.
- State:
  - `Pending`: requested by teacher and amount is reserved.
  - `Approved`: reviewed and ready for external transfer; reserve remains held.
  - `Paid`: external transfer completed; current and reserved balances decrease.
  - `Rejected`: request rejected; reserved amount is released.
- Validation: cannot skip from `Pending` directly to balance deduction without recording admin actor/time.

## TeacherPayoutAdjustment

- `Id`: unique adjustment id.
- `TeacherId`: teacher profile id.
- `RelatedFinancialEventId`: optional source event.
- `RelatedPayoutId`: optional paid payout impacted by later reversal.
- `Amount`: signed amount.
- `Reason`: required.
- `Status`: `Open`, `Applied`, `Voided`.
- Used when a refund/cancel occurs after teacher earnings were already paid.

## SharedTeacherPackage

- `Id`: unique product id.
- `Name`, `Slug`, `Description`, `ImageUrl`.
- `Price`: paid price before coupons/discounts.
- `IsPublished`, `AvailableFrom`, `AvailableUntil`.
- `CreatedByUserId`, `UpdatedByUserId`.
- `DistributionMode`: `Percentage`, `FixedAmount`, or `Mixed`.
- Validation:
  - percentage allocations cannot exceed 100%.
  - fixed allocations cannot exceed package price after explicit admin confirmation; platform remainder cannot be negative.

## SharedTeacherPackageTeacher

- `Id`.
- `SharedTeacherPackageId`.
- `TeacherId`.
- `SubjectId`: optional subject context.
- `AllocationMode`: `Percentage` or `FixedAmount`.
- `AllocationValue`: percentage or amount.
- `DisplayOrder`.
- Validation: one teacher may appear once per package/subject grouping unless explicitly separate items are modeled.

## SharedTeacherPackageItem

- `Id`.
- `SharedTeacherPackageId`.
- `TeacherId`.
- `ContentType`: package, term, month, lesson, lesson video, exam, or future content type.
- `ContentId`.
- `SubjectId`.
- `IsIncluded`.
- Validation: included content must belong to the selected teacher or be platform-wide by explicit admin choice.

## StudentSharedPackageGrant

- May reuse `StudentAccessGrant` if `GrantType` supports the shared package target, or add a link table from grant to `SharedTeacherPackageId`.
- Grants student access to included content according to the package item list.
- Cancellation/revocation links back to teacher financial reversal events.

## TeacherPublicProfile

- Can be implemented by extending `TeacherProfile`.
- Fields:
  - `PublicSlug`.
  - `PublicBio`.
  - `IntroVideoUrl` or existing media reference.
  - `IsPublicProfileEnabled`.
  - `RatingAverage`, `RatingCount` if ratings already exist or are added.
- Relationships:
  - subjects taught.
  - single-teacher packages.
  - shared packages.
  - public lessons/content allowed for browsing.
  - teacher-scoped community posts.

## CommunityPost Teacher Scope

- Add optional `TeacherId` to `CommunityPost`, or equivalent scoped relation.
- Existing moderation state remains authoritative.
- Rule: public profile shows only approved/visible posts; admin moderation sees teacher scope for filtering.
