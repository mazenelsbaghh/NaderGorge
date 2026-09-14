# Feature Specification: Financial and Data Integrity Hardening

**Feature Branch**: `156-financial-data-integrity-hardening`  
**Created**: 2026-06-30  
**Status**: Draft  
**Input**: Approved Arabic Feature Brief for Phase 2 in `docs/full-platform-defects-remediation-phases-2026-06-29.md`

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Safe Student Recharge And Balance Changes (Priority: P1)

As a student and finance operator, recharge approval, SMS matching, and content debits must never double-credit or create a negative balance, even when the same payment is submitted, matched, or retried concurrently.

**Why this priority**: Student money is the highest-risk workflow. Duplicate credits or negative balances directly corrupt financial state.

**Independent Test**: Can be tested by approving/matching one recharge twice and attempting an overdraft debit; the second credit is blocked/idempotent and the overdraft fails without changing persisted balances.

**Acceptance Scenarios**:

1. **Given** a pending recharge and one matching SMS, **When** the SMS upload and student submission both try to match it, **Then** only one request/SMS pair is matched and only one student balance credit exists.
2. **Given** a student balance below the purchase price, **When** content purchase tries to debit the balance, **Then** the purchase fails and the balance remains non-negative.
3. **Given** a recharge request already approved, matched, or rejected, **When** it is resolved again, **Then** the operation returns a controlled conflict/failure and does not create another transaction.

---

### User Story 2 - Safe Teacher Payout Reservation (Priority: P1)

As a teacher and finance admin, payout requests must reserve available teacher balance at request time so concurrent requests cannot overcommit the same earnings.

**Why this priority**: Teacher payout overcommitment creates an immediate financial liability and makes reconciliation unreliable.

**Independent Test**: Can be tested by requesting payouts whose combined value exceeds current balance; only requests within available balance become pending, rejection releases reserve, and payment settles the reserved amount.

**Acceptance Scenarios**:

1. **Given** a teacher account with 500 EGP current balance and no reserve, **When** the teacher requests a 200 EGP payout, **Then** pending payout is created and 200 EGP becomes reserved while current balance remains 500 EGP.
2. **Given** 200 EGP is already reserved from a 500 EGP balance, **When** the teacher requests another 350 EGP payout, **Then** the request is rejected because available balance is 300 EGP.
3. **Given** a pending payout with reserved funds, **When** finance rejects it, **Then** the reserve is released and current balance is unchanged.
4. **Given** a pending payout with reserved funds, **When** finance marks it paid, **Then** current balance and reserved balance both decrease by the payout amount.

---

### User Story 3 - Valid, Idempotent Access Grants (Priority: P2)

As a student and admin, each access grant must point to the correct target shape for its grant type and active duplicates must be blocked so entitlement checks stay deterministic.

**Why this priority**: Duplicate or malformed grants can unlock the wrong content or make cancellation/refund logic inconsistent.

**Independent Test**: Can be tested by creating valid package/term/month/lesson/video/exam grants and attempting malformed or duplicate active grants; malformed or duplicate rows are rejected.

**Acceptance Scenarios**:

1. **Given** a package grant, **When** it is saved, **Then** package target is present and unrelated target fields are absent.
2. **Given** an active grant already exists for the same student, grant type, source, and target, **When** another active duplicate is created, **Then** the duplicate is blocked or the existing grant is reused by the originating workflow.
3. **Given** a cancelled or inactive grant exists, **When** a new active grant for the same target is created through a valid workflow, **Then** the new grant can be recorded without reactivating stale audit data.

---

### User Story 4 - Restrict Destructive Deletes For Financial History (Priority: P2)

As finance/admin, deleting users, wallets, accounts, payouts, SMS logs, or grants that are referenced by financial/audit history must be blocked so historical records remain explainable.

**Why this priority**: Cascade deletion of ledger/audit records makes reconciliation and incident review impossible.

**Independent Test**: Can be tested by inspecting the schema/model and attempting to delete referenced principals; relationships use restrict/no-action semantics for financial history.

**Acceptance Scenarios**:

1. **Given** a user has balance transactions, access grants, recharge requests, or payout audit records, **When** a delete is attempted, **Then** the system blocks direct deletion and preserves the history.
2. **Given** financial or audit rows reference SMS/recharge/wallet/payout records, **When** parent rows are deleted directly, **Then** the delete is rejected unless a documented soft-delete workflow is used.

### Edge Cases

- PostgreSQL serialization failures (`40001`) during financial state transitions retry once through the platform transaction helper, then surface as a controlled conflict if still unresolved.
- Unique and check-constraint violations from idempotency, SMS matching, non-negative balances, or grant shape rules surface as a controlled conflict/failure instead of an unhandled server error.
- Pending recharge lookup remains efficient for amount, wallet, sender phone, status, and creation time.
- A matched SMS must satisfy `IsMatched = true` and `MatchedRechargeRequestId IS NOT NULL`; unmatched SMS must have `IsMatched = false` and no matched request.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Teacher finance surface: request payout within available balance, verify pending status and available balance reduction, then admin rejects and reserve is released.
- **Manual QA Role/Flow 2**: Student recharge surface: submit proof for a pending recharge that has a matching SMS, verify one credit only and no duplicate credit on retry.
- **Manual QA Negative Check**: Attempt payout above available balance and duplicate recharge approval; both must be denied with a clear message and no balance mutation.
- **Docker Acceptance**: `docker compose config -q`; migration can be generated/applied; backend tests for finance/recharge/grants pass.
- **External Dependencies**: Live mobile SMS upload device and real payment gateway are not required for automated validation; SMS upload is validated through command handlers and seeded entities.

## Clarifications

### Session 2026-06-30

- Q: Should Phase 2 be implemented completely or only the critical core? -> A: Full Phase 2 is in scope.
- Q: What payout reservation model should be used? -> A: Reserve on teacher account with `ReservedBalance`; available balance is current minus reserved.
- Q: How should duplicate active access grants behave? -> A: Database blocks duplicate active grants for the same student/source/target; workflows may reuse the existing active grant when they intentionally provide idempotency.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST protect every student balance debit with an atomic available-balance check so balances never become negative.
- **FR-002**: System MUST prevent duplicate recharge credits for the same recharge request by enforcing idempotency on balance transactions with a recharge reference.
- **FR-003**: System MUST prevent one incoming SMS from being matched to more than one recharge request.
- **FR-004**: System MUST ensure SMS match state is internally consistent: matched rows have a matched request and unmatched rows do not.
- **FR-005**: System MUST transition recharge requests from pending to matched, approved, or rejected only once; repeated or concurrent resolution attempts must be blocked without additional balance mutations.
- **FR-006**: System MUST use controlled conflict/failure responses for serialization conflicts and database invariant violations that are expected in concurrent financial workflows.
- **FR-007**: System MUST reserve teacher payout funds at request time and compute payout eligibility from available balance, not raw current balance.
- **FR-008**: System MUST release reserved teacher payout funds on rejection and settle reserved plus current balance on payment.
- **FR-009**: System MUST reject payout resolution for non-pending payouts without changing balances.
- **FR-010**: System MUST enforce non-negative balance constraints for student balances, teacher current/reserved balances, and digital wallet balances.
- **FR-011**: System MUST enforce access grant target shape according to grant type.
- **FR-012**: System MUST prevent duplicate active grants for the same student, grant type, source, and target where a target exists.
- **FR-013**: System MUST restrict or no-action cascade deletes for financial and audit relationships so historical financial records are not deleted by deleting a referenced principal.
- **FR-014**: System MUST include a pending recharge lookup index covering wallet, status, amount, sender phone, and creation time.
- **FR-015**: System MUST include automated tests for recharge idempotency, payout reservation, access grant shape/uniqueness model, non-negative constraints, and conflict mapping.

### Key Entities *(include if feature involves data)*

- **StudentBalance**: Student wallet balance used for purchases and recharge credits; must remain non-negative.
- **BalanceTransaction**: Immutable student balance ledger entry with transaction type and optional reference for idempotency.
- **DigitalWallet**: Payment-listener wallet and observed provider balance; must remain non-negative.
- **RechargeRequest**: Student recharge workflow request with pending/matched/approved/rejected lifecycle.
- **IncomingSmsLog**: Uploaded SMS payment evidence that may match one recharge request.
- **TeacherAccount**: Teacher finance account with total earnings, current balance, reserved balance, and commission rate.
- **TeacherPayout**: Teacher payout request with pending/paid/rejected lifecycle.
- **StudentAccessGrant**: Student entitlement row with grant type, target identifiers, source references, usage/cancellation audit, and active state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Duplicate or concurrent recharge matching produces exactly 1 items credit and 1 requests match pair in automated tests.
- **SC-002**: Overdraft student debits and over-available payout requests fail in under 5 seconds without changing current balances.
- **SC-003**: Payout request, reject, and paid transitions leave teacher current/reserved balances matching 4 acceptance scenarios.
- **SC-004**: Invalid grant target shapes and duplicate active grant rows are rejected by at least 2 schema-level safeguards.
- **SC-005**: Expected financial conflict cases return controlled conflict/failure behavior for 100% of covered tests rather than unhandled internal server errors.
- **SC-006**: The full backend build and at least 3 focused feature test groups pass before Phase 2 is marked complete in the remediation document.

## Assumptions

- Existing roles and authorization gates for recharge, teacher payout, and admin finance commands remain unchanged.
- This phase is backend/database focused; no new frontend surface is required unless existing DTOs must expose available/reserved balances.
- PostgreSQL is the production source of truth; EF InMemory tests are used only where existing test patterns already do so, while schema invariants are verified through model/migration tests.
- Existing direct delete APIs for users/principals are not expanded; this phase blocks unsafe cascade behavior and leaves broader soft-delete UX to future phases if needed.
