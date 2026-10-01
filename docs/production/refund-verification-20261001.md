# Refund safety release — 2026-10-01

The owner requested verification and production deployment of the financial refund fixes. Build scope is `all`, carrying forward the operator choice recorded in `docs/production/refund-gift-20260930.md`. The four-image immutable release contract remains mandatory.

The candidate is one source-only commit on the latest shared `codex/production` parent, prepared through a temporary Git index in the canonical repository. It includes refund safety changes, their regression tests, Arabic balance-history labels, and the missing finance navigation group. Other ongoing local work is preserved and excluded from this refund repair.

## Reviewed behavior

- Legacy cancellation claims an active access grant and credits balance in one transaction, preventing duplicate credit from stale overlapping requests.
- Reversing a student-balance refund removes the credit and reverses the journal atomically. Concurrent requests debit once; insufficient available balance rejects the reversal and preserves posted state.
- External refund submissions round both split amounts to two decimal places, avoiding floating-point drift for the 290 EGP cases.
- Advanced finance routes are available in a separate “تفاصيل الحسابات” navigation group; refund access continues to use the canonical permission policy.

## Verification before publication

- Real PostgreSQL refund suite: 24 passed, including seven real JWT/Redis HTTP authorization cases, cash/balance posting, duplicate requests, rounding, rollback, concurrent reversal, and spent-balance rejection.
- `make ops-check`: API build, EF pending-model check, 1,657 Application tests (20 environment-dependent skips), frontend lint/typecheck, 230 worker tests, and Compose validation passed. One existing frontend hook warning remains.
- Frontend route-permission contracts passed after repairing the missing navigation group. Eight focused refund checks and four Chrome UI scenarios passed previously; the Chrome scenarios use synthetic HTTP responses.
- Three-node pre-release health passed. No real student financial record is used for acceptance testing.

Publication, immutable build, migration/backup gate, serialized rolling rollout and post-release health evidence are required before reporting deployment success. Admin AI remains disabled under its existing governance gate.
