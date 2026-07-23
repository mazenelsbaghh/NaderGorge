# Contract: Migration, Reconciliation and Rollout

## Module order

`people-organization` → `shifts-attendance` → `leave` → `payroll` → `lifecycle-remaining`. A later module cannot cut over while a prerequisite is not `NewActive`.

## Endpoints

- `GET /api/hr/admin/rollout/modules` — state/read target/write target/last reconciliation.
- `POST /api/hr/admin/migrations/dry-run` — `{module,sourceSnapshot}`.
- `GET /api/hr/admin/migrations/{batchId}` — counts, totals, hashes, conflicts and mappings.
- `POST /api/hr/admin/migrations/{batchId}/resolve-conflict` — decision/reason.
- `POST /api/hr/admin/migrations/{batchId}/execute` — requires matching completed dry-run and unchanged source checksum.
- `POST /api/hr/admin/rollout/{module}/activate` — requires zero unexplained material differences.
- `POST /api/hr/admin/rollout/{module}/rollback` — affects this module only; reason required.

Permission: read `hr.migration.read`; mutations `hr.migration.manage`; all actions audited.

## Reconciliation

Every batch reports source/target counts, accepted/skipped/conflict counts, payroll gross/deduction/net sums where relevant, per-status totals and deterministic hash. A difference is either resolved with mapping/reason or blocks execution/cutover.

Migration record identity is `(sourceSystem,module,sourceKey)`; rerun returns existing mapping rather than duplicate target row. Historical employee records without login receive an inactive archival User in the same transaction before EmployeeProfile creation.

## State machine

`Legacy` → `ShadowValidated` → `NewActive` → `RollingBack` → `Legacy`. `Failed` may occur before activation. Writer switches atomically with rollout state. No state permits simultaneous legacy and new writers.

## Rollback guarantee

Rollback changes routing and feature visibility, not immutable migration evidence. New rows remain quarantined/read-only for investigation. Other module states do not change. Re-activation requires a fresh reconciliation against current source checksum.

## Stable failures

`ROLLOUT_ORDER_VIOLATION`, `DRY_RUN_REQUIRED`, `SOURCE_CHANGED`, `RECONCILIATION_FAILED`, `UNRESOLVED_CONFLICTS`, `DUAL_WRITE_FORBIDDEN`, `MODULE_NOT_ACTIVE`, `ROLLBACK_NOT_SAFE`.
