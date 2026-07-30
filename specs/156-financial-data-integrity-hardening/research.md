# Research: Financial and Data Integrity Hardening

## Decision: Use `TeacherAccount.ReservedBalance` for payout reservation

**Rationale**: The existing model already has one account row per teacher with `CurrentBalance`; adding `ReservedBalance` keeps available balance as a simple invariant: `Available = CurrentBalance - ReservedBalance`. Requesting a payout increments reserve; rejection decrements reserve; payment decrements both reserve and current balance.

**Alternatives considered**:
- Separate payout ledger reservation rows: stronger audit but larger model/API change and unnecessary because `TeacherPayout` already represents the reservation.
- Deduct current balance at request time: obscures unpaid liabilities and changes existing meaning of current balance.

## Decision: Keep balance idempotency on `(TransactionType, ReferenceId)` when reference exists

**Rationale**: Recharge credits already pass `TransactionType = DigitalRecharge` and `ReferenceId = RechargeRequest.Id`. A filtered unique index blocks duplicate credits without impacting non-referenced manual adjustments.

**Alternatives considered**:
- Add new idempotency key column: broader but larger migration and caller updates.
- Rely only on handler status checks: unsafe under concurrent request races.

## Decision: Use atomic status predicates for recharge/request/SMS transitions

**Rationale**: `Pending -> Matched/Approved/Rejected` must be single-writer. Updating by primary key plus current status and checking affected row count avoids read-then-write races. SMS matching also needs a filtered unique index on `MatchedRechargeRequestId`.

**Alternatives considered**:
- Serializable transactions only: helpful but not sufficient without idempotent constraints.
- In-memory locks: not safe across multiple API instances.

## Decision: Map expected database conflicts to controlled conflict responses

**Rationale**: PostgreSQL `40001`, unique violations, and check constraint violations are expected in concurrent financial workflows. Middleware should map them to `409 Conflict` when they escape command handlers; command handlers should return domain failures when they detect them locally.

**Alternatives considered**:
- Return 400 for all `InvalidOperationException`: already exists but hides conflict semantics.
- Swallow and retry indefinitely: risks duplicated side effects and poor user feedback.

## Decision: Enforce grant target shape with check constraints and active duplicate indexes

**Rationale**: Access checks depend on `GrantType` matching the correct target field. Database checks prevent malformed rows from any code path. Filtered active unique indexes prevent duplicated active grants while preserving inactive/cancelled history.

**Alternatives considered**:
- Application-only validation: misses direct imports/migrations and concurrent races.
- One polymorphic target column: larger refactor across access checks and content hierarchy.

## Decision: Use Restrict/NoAction for finance/audit relationships

**Rationale**: Financial history must remain explainable; deleting referenced principals should fail instead of cascading ledger/audit rows away. Existing model already uses many `Restrict` relationships, so this expands that pattern.

**Alternatives considered**:
- Cascade delete: unacceptable for financial/audit data.
- Full soft-delete UX in this phase: broader than Phase 2 hardening; current phase blocks unsafe deletes and leaves owner-facing UX for later.
