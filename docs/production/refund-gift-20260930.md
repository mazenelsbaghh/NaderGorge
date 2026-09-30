# Refund and gift release review — 2026-09-30

The owner requested an urgent refund and gift correction before the next month. The selected build scope is `all` because backend and frontend change together. The candidate is limited to these features and their tests; the ongoing Admin AI rollout remains behind its existing activation gate.

## Changes

- Keep refunds reachable from the Admin home and finance navigation. Show the refunded amount, reason, and the actor who posted the journal entry, falling back to the draft creator or the legacy balance transaction actor.
- Include the entered cancellation reason in new legacy balance refund descriptions. Existing historical descriptions cannot recover an unstored reason.
- Let gift issuance search for a teacher and then choose package, term, section, lesson, and optionally video. Child lookups stay bound to the selected parent and expose system containers used by direct lessons.

## Local verification

- `make ops-check`: passed; API build and EF pending-model check passed, 1,651 application tests passed with 20 environment-dependent skips, frontend lint and typecheck passed (one existing hook warning), 230 worker tests passed, and Compose validated.
- `RefundLedgerDisplayTests` and `RefundLedgerPostgresTests`: passed; both PostgreSQL tests used a disposable PostgreSQL 16.10 instance. One checked the posting actor, amount, and reason in the listing; the other exercised the external package refund action through cancellation, cash journal posting, and final grant state in a real transaction.
- Chrome browser tests: Admin refund visibility and refund form submission passed 2/2 with mocked API data; gift ledger, promotional balance, and teacher-to-lesson issuance passed 3/3 with mocked API data. The refund form labels are now associated with their fields.
- Three-node read-only status: success. The bounded backend log sample on node-2 did not contain a refund event, so it cannot establish the cause of the reported live failure. The local PostgreSQL path passes, but a real authenticated production submission still needs post-release acceptance.

## Release gates

Source publication and server rollout require the exact reviewed source commit, a passing release preview, fresh migration/backup evidence, and post-deployment health. The local mocked browser tests do not verify a real authenticated refund submission; that production acceptance remains required.
