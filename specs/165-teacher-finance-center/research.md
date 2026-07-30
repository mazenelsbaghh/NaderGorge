# Research: مركز حسابات المدرسين والمالية

## Decisions

### Extend the existing ledger, do not create a second ledger

- **Decision**: Use `TeacherFinancialEvent`, `TeacherFinancialAllocation`, `TeacherAccount`, `TeacherPayout`, and `TeacherPayoutAdjustment` as the authoritative accounting lineage.
- **Rationale**: Purchase, code activation, shared-package and manual accounting already emit or consume these records. A parallel ledger would create duplicate balances and reconciliation ambiguity.
- **Alternatives considered**: A new isolated finance ledger was rejected because it would need unreliable back-links to every existing financial event.

### Use effective-dated agreement snapshots

- **Decision**: Add a teacher agreement model with scoped overrides and effective dates; resolve once during the sale/code trigger and snapshot the resolved terms on the allocation.
- **Rationale**: Changing a teacher's current percentage must never recalculate historical dues.
- **Alternatives considered**: Reading `TeacherProfile.CommissionRate` at report time was rejected because it rewrites business history implicitly.

### Confirm code delivery separately from code generation

- **Decision**: Code groups whose settlement timing is delivery require one audited delivery confirmation before a due is created.
- **Rationale**: Generated codes are not proof of handover; the confirmation prevents premature or duplicate dues.
- **Alternatives considered**: Immediate accounting at code generation was rejected because the user explicitly distinguishes delivery.

### Reserve allocations through settlement lines

- **Decision**: A draft/review/approved/paid/cancelled settlement owns exact allocation/adjustment lines and a payment reference; line reservation is protected by transaction and uniqueness.
- **Rationale**: The current payout amount reserves an aggregate balance but does not prove which earnings it paid, making paid reversals unsafe.
- **Alternatives considered**: Keep aggregate payouts only was rejected because the same allocation can be reported as unpaid after cash was sent.

### Preserve EGP and Bunny USD separately

- **Decision**: Reports expose EGP revenue/teacher/platform fields separately from `bunnyCostUsd`; no automatic FX conversion.
- **Rationale**: User requested Bunny in USD. Combining currencies yields a false profit number.
- **Alternatives considered**: A daily FX rate was rejected as not matching invoices and not requested.

### Treat Bunny per-video bandwidth quality honestly

- **Decision**: Reuse `BunnyUsageSnapshot` and propagate its `BandwidthSource` and `IsBandwidthEstimated`; retain the last valid snapshots when sync fails.
- **Rationale**: Current Bunny integration allocates library traffic using watch-time where per-video traffic is unavailable. It cannot be called actual per-video cost without evidence.
- **Alternatives considered**: Display all Bunny costs as actual was rejected because it would misstate data quality.

### Shared-package over-allocation needs explicit acknowledgement

- **Decision**: Preview all teacher allocations server-side. If teacher total exceeds the sale basis, return a warning and require an explicit admin loss acknowledgement to complete the operation.
- **Rationale**: The approved product rule allows deliberate platform loss but prevents accidental negative platform share.
- **Alternatives considered**: Silent approval and absolute rejection were rejected by the user.

### Partial refunds are selected-line reversals

- **Decision**: Admin selects the affected teacher allocation lines and supplies a reason; the system writes immutable reversal/debt entries for selected lines only.
- **Rationale**: User chose manual responsibility for partial refunds, especially in shared packages.
- **Alternatives considered**: Automatic pro-rata reversal was rejected by the user.

## Risks and mitigations

- Existing immediate code events and activation events can double count: centralize trigger enforcement with unique source/trigger keys and migrate old paths carefully.
- Existing payout records may lack allocation links: preserve them and create auditable opening settlement allocations rather than fabricate historical line matches.
- Concurrency in settlement/payment: serializable transaction, version checks, unique `SettlementLine.AllocationId`, and explicit line states.
- Attachment storage: reuse approved asset-storage references and metadata, never local arbitrary paths.
- Admin-only scope conflicts with teacher finance screens: do not expose new center data to teachers; preserve existing legacy endpoints only until separately retired.

## Current-state record (documentation audit, 2026-07-24)

This record describes the repository state inspected during implementation; it is
not a pre-change execution baseline.

- The existing teacher-finance lineage is `TeacherFinancialEvent` →
  `TeacherFinancialAllocation`, with `TeacherAccount`, `TeacherPayout`, and
  `TeacherPayoutAdjustment` retaining the account, aggregate payout, and debt
  workflows. The existing admin ledger endpoint reads allocations directly.
- Existing teacher payout requests reserve an aggregate amount on
  `TeacherAccount`; they do not identify the individual allocations being paid.
  The finance-center settlement tables add that missing per-allocation lineage
  without deleting or rewriting legacy payout records.
- Wallet recharge requests are handled by the wallet/recharge workflow, rather
  than by a teacher-financial-event source. They must remain outside teacher
  revenue/allocation reporting.
- Code activation remains an independently idempotent application command. The
  finance-center code-group terms and delivery-confirmation tables provide the
  additional accounting evidence needed to distinguish delivery-triggered from
  activation-triggered dues.
- Bunny reporting is based on `BunnyUsageSnapshot`. Per-video bandwidth may be
  estimated, so reports must preserve the source/estimate flag and retain the
  last valid snapshot when a sync fails.

## Cutover boundary

- `20260724185632_AddTeacherFinanceCenter` is additive for the new finance-center
  tables and adds nullable agreement/settlement snapshot references to existing
  allocations. It contains no data backfill, payout-to-allocation matching, or
  mutation of historical amounts.
- Therefore deployment must preserve all legacy events and payouts as historical
  records. Any opening settlement for a legacy balance must be created through
  an audited, separately approved reconciliation process; it must not fabricate
  historic allocation links. The implementation task for that reconciliation is
  still outstanding.
