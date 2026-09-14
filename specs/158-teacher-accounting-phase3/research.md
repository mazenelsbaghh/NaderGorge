# Research: Teacher Accounting Phase 3

## Decision 1: Use an append-only teacher financial ledger as the canonical source

**Decision**: Add a canonical `TeacherFinancialEvent` with one or more `TeacherFinancialAllocation` rows. Existing `AccessCodeActivationLog` and `SalesFinancialEffect` remain historical/source records, but teacher account balances and teacher/admin finance views are driven by the new ledger.

**Rationale**: The current system credits teacher balance only during code activation and writes direct purchases to `SalesFinancialEffect` with `TeacherShareImpact = 0`. Phase 3 requires one model that handles codes, direct purchases, public exams, shared packages, refunds, free/discounted operations, review states, and payout status.

**Alternatives considered**:
- Extend `AccessCodeActivationLog`: rejected because it only represents code activation and has one teacher/package shape.
- Extend `SalesFinancialEffect` only: rejected because it is purchase-operation scoped and currently uses a single teacher; it does not model review/payout status or multiple teacher allocations cleanly.

## Decision 2: Teacher account balances are projections updated atomically from ledger transitions

**Decision**: Keep `TeacherAccount` as a fast balance projection, but mutate it only through an application service that writes ledger rows and adjusts `TotalEarnings`, `CurrentBalance`, and `ReservedBalance` in a serializable/idempotent transaction.

**Rationale**: Existing payout logic already reserves balance and uses concurrency safeguards. Phase 3 needs the same rigor for earning creation and reversals.

**Alternatives considered**:
- Calculate balances exclusively from ledger on read: rejected for admin/teacher dashboards and payout requests where indexed balance checks are needed.

## Decision 3: Payout lifecycle becomes Pending -> Approved -> Paid or Pending -> Rejected

**Decision**: Add a ready-for-transfer state, represented as `Approved` or `ReadyForPayout` in `PayoutStatus`. Approval means admin review passed and reserved balance remains held. `Paid` means the actual external transfer happened and balance is deducted. `Rejected` releases the reserve.

**Rationale**: User clarified that approval should make the payout ready, then the admin separately records "تم الصرف" after real transfer.

**Alternatives considered**:
- Keep current `Paid` as approval: rejected because it records money movement before transfer.

## Decision 4: Refund/cancel after payout creates a negative adjustment/debt

**Decision**: If an earning is reversed before payout, create a reversal ledger event that reduces unpaid/current balance. If the earning was already paid, create a negative adjustment/debt carried into the next available balance or payout cycle.

**Rationale**: User chose this rule explicitly. It preserves auditability and avoids mutating paid payout history.

**Alternatives considered**:
- Block refund after payout: rejected because product operations may require cancellation/refund after settlement.
- Edit historical earning rows: rejected because financial history must remain auditable.

## Decision 5: Free and 100% discount operations are zero-value events unless explicit compensation exists

**Decision**: Free/100% discount grants create ledger rows with `GrossAmount` and/or `PaidAmount` as appropriate and zero teacher due by default. If admin enters explicit teacher compensation, create a separate compensation event linked to the source operation.

**Rationale**: User clarified these should appear for tracking only and not add dues unless compensation is explicit.

**Alternatives considered**:
- Hide free events: rejected because the teacher/admin need operational visibility.
- Always calculate teacher share from original price: rejected by the user clarification.

## Decision 6: Shared package is a separate product type with allocation rules

**Decision**: Create separate shared package entities for package header, included teachers/items, and allocation rules. Allocation mode supports percentage or fixed amount per teacher, with platform remainder explicitly computed and validated.

**Rationale**: Existing `Package` has one `TeacherId`. Phase 3 needs many teachers, many subjects/items, and per-teacher allocation decisions while preserving current single-teacher package behavior.

**Alternatives considered**:
- Reuse `Package` by adding many-to-many teachers: rejected because existing package ownership/reporting assumes one teacher and one subject.

## Decision 7: Suspicious events use review status before payout inclusion

**Decision**: Add review status to teacher financial allocations/events: `AutoApproved`, `PendingReview`, `Approved`, `Rejected`, `Reversed`. Suspicious events are visible to admin but excluded from available payout until approved.

**Rationale**: Roadmap requires a queue/review for suspicious transactions before payout. This also provides a clean place to handle mismatched product/teacher ownership.

**Alternatives considered**:
- Fail suspicious purchase/code activation entirely: rejected because operations may be valid but need admin validation.

## Decision 8: Teacher finance reads are paginated and tab-scoped

**Decision**: Add calendar summary endpoints and paginated day transaction endpoints. Frontend admin finance should load only the active tab data and only fetch teacher/package filters where required.

**Rationale**: User reported many 401/unneeded requests and slow loading. Existing admin finance fetches teachers/packages at page mount even when the active tab may not need them.

**Alternatives considered**:
- Keep all current fetches and cache client-side: rejected because unauthorized/unneeded requests still happen and slow initial render.

## Decision 9: Teacher public profile gets scoped moderated community

**Decision**: Extend existing community posts/comments with teacher scope or introduce teacher-scoped community references while keeping moderation status/commands unchanged.

**Rationale**: Roadmap requires teacher community inside the teacher profile while preserving current moderation behavior.

**Alternatives considered**:
- Duplicate community system for teachers: rejected because moderation, comments, and admin workflows already exist.
