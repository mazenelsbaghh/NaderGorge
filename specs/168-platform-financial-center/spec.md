# Feature Specification: Platform Financial Center and General Ledger

**Feature Branch**: `168-platform-financial-center`
**Created**: 2026-08-05
**Status**: Planned
**Input**: Build a unified platform financial center covering platform cash and profit, teacher liabilities and settlements, student recharge liabilities, cash and balance refunds, platform expenses, staff permissions, flexible budgets, historical migration, reconciliation, and EGP reports/exports.

## Confirmed Business Decisions

- Revenue is recognized at purchase, not at recharge.
- Recharge remains visible as collected cash and an unused student balance liability until spent.
- Recharges may target general student balance or a specific teacher balance; the scope must remain explicit and immutable after proof submission.
- Refunds may be returned to student balance or paid as cash.
- Platform expenses are independent from teacher expenses and are not deducted from teacher dues.
- Admins and explicitly permitted staff can operate finance functions.
- Expense entry does not require a mandatory approval workflow, but every mutation requires permission, actor identity, reason, and audit evidence.
- The platform has physical cash in addition to digital wallets; bank accounts may be added through the same treasury-account abstraction.
- Budgets may be created for weekly, monthly, annual, or arbitrary date ranges.
- Historical financial data must be reconstructed from all trustworthy platform transactions and imported with traceable opening/reconstruction entries.
- Reports must export to Excel and PDF.
- Currency is Egyptian Pound (EGP) only in this phase.
- No tax/VAT calculation is required in this phase.

## User Scenarios & Testing

### User Story 1 - Owner sees the platform financial position (Priority: P1)

As the platform owner, I need one financial cockpit that distinguishes cash held, unused student balances, teacher liabilities, platform revenue, expenses, refunds, and net profit so I can know what money belongs to the platform and what money is owed to others.

**Independent Test**: Create a teacher-scoped recharge, leave part unused, purchase content with part, pay a teacher settlement, record an expense, and issue a refund. Verify the cockpit and account balances reconcile to the journal.

**Acceptance Scenarios**:

1. A recharge increases treasury cash and student-balance liability but does not increase platform revenue.
2. A purchase consumes student liability and creates teacher payable plus platform revenue according to the locked allocation.
3. Dashboard totals show cash, liabilities, revenue, expenses, refunds, teacher payable, and net profit without double counting.
4. Every displayed total drills down to journal lines and the original business document.

### User Story 2 - Finance staff records platform expenses (Priority: P1)

As an authorized staff member, I need to record cash or payable expenses with category, cost center, beneficiary, payment source, receipt, and notes so operating costs appear in the application and profit reports.

**Independent Test**: Grant a staff user expense permissions, record a paid cash expense and an unpaid supplier expense, then verify treasury, payable, expense, budget, and audit views.

**Acceptance Scenarios**:

1. An authorized user can create, edit while draft, post, void by reversal, and view expenses.
2. Posting a paid expense debits an expense account and credits the selected treasury account.
3. Posting an unpaid expense credits accounts payable; later payment clears the payable and reduces treasury.
4. Unauthorized staff cannot view or mutate financial data.
5. Posted financial documents cannot be deleted or edited in place; corrections use reversing entries.

### User Story 3 - Admin refunds a student by balance or cash (Priority: P1)

As an admin, I need to refund a purchase either into student balance or as cash while reversing platform revenue and teacher share correctly so the student, teacher, treasury, and reports remain consistent.

**Independent Test**: Refund one unpaid-teacher purchase to balance and one already-paid-teacher purchase in cash, then verify access cancellation, student liability/cash, teacher reversal/debt, platform revenue reversal, and refund counters.

**Acceptance Scenarios**:

1. Refund requires the original purchase/grant, amount, reason, method, actor, and idempotency key.
2. Balance refund creates student balance liability without reducing treasury cash.
3. Cash refund records the treasury account, payment reference, and optional attachment and reduces cash.
4. Teacher allocation is reversed immediately if unpaid; if already paid, a teacher debt/next-settlement adjustment is created.
5. Partial refunds cannot exceed the remaining refundable amount.
6. Reports show refund count, unique students, cash-refund total, balance-refund total, and affected teacher/content.

### User Story 4 - Owner sees each teacher's complete financial relationship (Priority: P1)

As the platform owner, I need each teacher's sales, teacher share, platform share, reversals, debts, settlements, and payments together so teacher money remains separate from platform money.

**Independent Test**: Generate purchases from direct balance, access code, public exam, and shared package; reverse one and settle another; verify teacher and aggregate reports.

**Acceptance Scenarios**:

1. Teacher ledger remains the source of teacher earnings and settlement eligibility.
2. Platform journal mirrors teacher payable creation, reversal, reservation, and payment without replacing teacher-finance domain records.
3. Shared-package allocations reconcile exactly: teacher shares plus platform share equal recognized sale amount.
4. Teacher summary exposes gross sales, teacher due, platform share, refunds, debt, paid, and outstanding.

### User Story 5 - Owner manages flexible budgets (Priority: P2)

As the platform owner, I need budgets for weekly, monthly, annual, or custom periods by category and cost center so I can compare planned and actual spending/revenue.

**Independent Test**: Create overlapping weekly and annual plans, post expenses and revenue, then verify actuals are calculated from journal dimensions and variance is correct.

**Acceptance Scenarios**:

1. Budget periods support Week, Month, Year, and Custom.
2. Budget lines target account/category and optional cost center.
3. Actuals come from posted journal lines only.
4. Reports show planned, actual, remaining, variance percentage, and forecast.
5. Budgets are versioned and never rewrite journal history.

### User Story 6 - Finance staff reconciles cash, wallets, and historical records (Priority: P1)

As finance staff, I need to reconcile physical cash and digital wallets against system balances and initialize the ledger from historical transactions so the financial center starts with defensible numbers.

**Independent Test**: Run historical reconstruction in dry-run, resolve exceptions, post an opening batch, then reconcile a wallet and cashbox statement with an explained adjustment.

**Acceptance Scenarios**:

1. Migration dry-run reports source counts, totals, duplicates, missing ownership, unmatched refunds, and unreconciled differences without posting.
2. Historical entries retain source type, source ID, original timestamp, and deterministic idempotency key.
3. Unsupported or ambiguous history goes to an exception queue and is not silently treated as platform profit.
4. Treasury reconciliation records opening system balance, counted/statement balance, explained items, adjustment, actor, and evidence.
5. Re-running migration or projections does not duplicate entries.

### User Story 7 - Authorized users export and close periods (Priority: P2)

As the owner or finance staff, I need Excel/PDF exports and period closure so reports can be shared with the accountant and historical months cannot drift.

**Independent Test**: Export a period, close it, attempt a backdated post, reopen with a privileged permission and reason, then verify audit history.

**Acceptance Scenarios**:

1. Exports include filters, generation timestamp, currency, totals, and traceable document references.
2. Closed periods reject new or edited postings dated inside the period.
3. Reopening requires a dedicated permission and reason and produces an audit event.
4. Corrections after close are posted in an open period with reference to the original entry unless the period is explicitly reopened.

## Functional Requirements

### Ledger and accounting

- **FR-001**: Maintain an immutable, balanced double-entry general ledger in EGP.
- **FR-002**: Every journal entry MUST have at least two lines and total debit MUST equal total credit to 0.01 EGP.
- **FR-003**: Posted journal entries MUST NOT be updated or deleted; corrections MUST use linked reversals.
- **FR-004**: Each business source MUST use a deterministic idempotency key and create at most one canonical journal effect.
- **FR-005**: Journal entries MUST link to `SourceType`, `SourceId`, occurrence time, posting time, actor, description, and optional correlation ID.
- **FR-006**: The chart of accounts MUST cover treasury assets, student liabilities, teacher payables, supplier payables, platform revenue, contra-revenue/refunds, operating expenses, payroll expense, and opening equity/suspense.
- **FR-007**: General and teacher-scoped student balances MUST be represented as separate liability dimensions.
- **FR-008**: Teacher payable subledger totals MUST reconcile to the general-ledger teacher-payable control account.
- **FR-009**: Student balance subledger totals MUST reconcile to the relevant student-liability control accounts.

### Revenue and recharge

- **FR-010**: Recharge MUST post cash received against student balance liability and MUST NOT recognize revenue.
- **FR-011**: Recharge scope MUST be `General` or `Teacher`, with teacher required for teacher scope.
- **FR-012**: Purchase MUST recognize revenue at successful entitlement completion, using paid/promotional funding and locked teacher allocation data.
- **FR-013**: Failed, cancelled, or duplicate purchase attempts MUST NOT create posted revenue.
- **FR-014**: Platform revenue and teacher payable MUST be separated for direct purchases, code activations/delivery terms, public exams, shared packages, and supported content types.

### Refunds

- **FR-015**: Refund methods MUST support `StudentBalance` and `Cash`.
- **FR-016**: Cash refund MUST require a treasury account and payment reference; balance refund MUST require target balance scope.
- **FR-017**: Refund posting MUST reverse the correct remaining platform share and teacher share and preserve the original sale.
- **FR-018**: Refunds after teacher payment MUST create teacher debt or next-settlement deduction according to selected disposition.
- **FR-019**: Refund reporting MUST distinguish requested, approved/posted, voided, cash paid, balance credited, partial, and full refunds.

### Expenses and treasury

- **FR-020**: Support expense categories, cost centers, beneficiaries/vendors, attachments, paid/unpaid status, recurring metadata, and notes.
- **FR-021**: Expense operation does not require workflow approval in this phase, but posting requires `finance.expenses.post` permission.
- **FR-022**: Support treasury account types `DigitalWallet`, `Cashbox`, and `BankAccount` while only requiring existing wallets and at least one cashbox in initial rollout.
- **FR-023**: Transfers between treasury accounts MUST not affect revenue or expense.
- **FR-024**: Treasury reconciliation MUST preserve counted/statement balance, system balance, variance, adjustment, evidence, and actor.
- **FR-025**: Existing payroll remains its domain source of truth; paid payroll MUST create payroll-expense and treasury journal effects exactly once.
- **FR-026**: Platform expenses MUST NOT reduce teacher dues unless a separate future business rule is explicitly introduced.

### Budgets and reporting

- **FR-027**: Support weekly, monthly, annual, and custom budget periods.
- **FR-028**: Budget actuals MUST derive from posted journal lines by account, category, cost center, and date.
- **FR-029**: Financial cockpit MUST expose cash, unused balances, teacher liabilities, payables, revenue, expenses, refunds, net profit, and cash flow.
- **FR-030**: Reports MUST support date range, teacher, account, category, cost center, source, refund method, and posting status filters.
- **FR-031**: Reports MUST export Excel and PDF using the same filtered server-side dataset as the screen.
- **FR-032**: Dashboard totals MUST drill down to entries and source documents.

### Permissions, periods, and audit

- **FR-033**: Add granular permissions for dashboard view, ledger view, expense create/post, refund create/post, treasury manage/reconcile, budget manage, export, period close, and period reopen.
- **FR-034**: Admin retains all finance permissions; staff receive only explicitly assigned permissions through the existing authorization model.
- **FR-035**: Every state mutation MUST record actor, time, reason where applicable, before/after state, and correlation ID through the existing audit system.
- **FR-036**: Accounting periods MUST support Open, Closed, and Reopened states.
- **FR-037**: Closed periods MUST reject backdated posting; reopening requires dedicated permission and a reason.

### Historical migration

- **FR-038**: Reconstruct historical journal effects from recharge requests, incoming wallet evidence, balance transactions, sales financial effects, teacher financial events/allocations, teacher settlements/payments/payouts, refunds/cancellations, payroll, and usable financial invoices.
- **FR-039**: Migration MUST run in dry-run, exception-review, and final-post modes.
- **FR-040**: Historical events MUST preserve original occurrence dates and deterministic source keys.
- **FR-041**: Ambiguous records MUST post to neither revenue nor teacher payable until resolved; opening suspense is allowed only with explicit owner-approved batch evidence.
- **FR-042**: Migration totals MUST reconcile per source and date range, and the final batch MUST be repeat-safe.

## Non-Functional Requirements

- **NFR-001**: Standard financial list and summary APIs target p95 below 500 ms using indexed, bounded queries and projections.
- **NFR-002**: Monetary values use PostgreSQL numeric precision and C# decimal; floating point is forbidden.
- **NFR-003**: Every posting operation is transactional and safe under retries/concurrency.
- **NFR-004**: Financial screens are Arabic-first, RTL, accessible, and use the existing admin design system.
- **NFR-005**: Sensitive attachments use the existing private file-storage path and permission-checked downloads.
- **NFR-006**: No raw student phone or sensitive evidence appears in aggregate exports unless the caller has the dedicated detailed-export permission.
- **NFR-007**: Worker changes are not required for core posting; optional scheduled budget snapshots or exports may use existing background infrastructure only if synchronous limits are exceeded.

## Key Entities

- **FinancialAccount**: Chart-of-accounts node and control/subledger role.
- **JournalEntry / JournalLine**: Immutable balanced accounting event and debit/credit lines.
- **TreasuryAccount**: Wallet, cashbox, or bank account mapped to a financial asset account.
- **ExpenseCategory / CostCenter / Vendor**: Expense reporting dimensions.
- **PlatformExpense / ExpensePayment**: Expense document and payment events.
- **RefundRequest / RefundPayment**: Refund case, selected method, posting, and evidence.
- **BudgetPlan / BudgetLine**: Planned amounts by period and dimensions.
- **AccountingPeriod**: Open/close/reopen control.
- **TreasuryReconciliation**: Statement/count comparison and adjustments.
- **FinancialMigrationBatch / FinancialMigrationException**: Historical reconstruction evidence and exception handling.

## Out of Scope

- Tax/VAT calculations and statutory tax filing.
- Multi-currency accounting.
- Mandatory expense approval chains.
- Deducting platform operating expenses from teacher dues.
- External bank-feed integrations in the first release.
- Replacing existing teacher-finance, payroll, sales, recharge, or entitlement domains; they remain business sources and publish/project into the ledger.

## Success Criteria

- **SC-001**: 100% of covered financial workflows create balanced journal entries with zero duplicate postings under retry tests.
- **SC-002**: Recharge, purchase, teacher settlement, expense, payroll, cash refund, and balance refund scenarios reconcile to 0.01 EGP.
- **SC-003**: General-ledger teacher payable equals teacher subledger outstanding across representative fixtures.
- **SC-004**: Historical migration dry-run accounts for every selected source row as posted, ignored with reason, or exception; no silent loss.
- **SC-005**: Finance cockpit values drill to source evidence and return under 500 ms p95 for agreed production-like dataset sizes.
- **SC-006**: Unauthorized staff receive forbidden responses for every finance permission boundary in automated tests.
- **SC-007**: Excel and PDF exports match on-screen filtered totals exactly.
- **SC-008**: Every implementation phase passes automated tests, Docker gate, manual QA, and end-of-phase report before the next phase starts.
