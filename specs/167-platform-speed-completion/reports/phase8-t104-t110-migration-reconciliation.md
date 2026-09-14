# Phase 8 — Migration and Release-Input Reconciliation

Date: 2026-07-29  
Scope: T104 and T110  
Status: **LOCAL RECONCILIATION PASS / REMOTE POSTGRESQL AND DOCKER GATES PENDING**

## Reconciled findings

- `20260729220000_AddWebVitalsDimensions` had no EF migration metadata, so the
  model snapshot contained its fields while the production migrator could not
  discover or apply it. It now has the `AppDbContext` and migration ID
  metadata.
- Four older migration classes are intentionally superseded:
  `AddUserSecurityStampVersion`, `AddVideoTypeCodeGrants`,
  `EnforceSingleTeacherStaffMembership`, and
  `GrantStaffStudentManagementAndReports`. Their forward repairs are
  consolidated in the registered
  `20260729151000_RepairVideoTypeCodeGrantSchema` migration. The inventory test
  fixes this as the only allowed unregistered set, so another orphan migration
  fails the gate.
- The API, migrator, admin bootstrap, and migration audit tests now use the
  Npgsql timestamp mapping expected by the model snapshot.
  Pending-model-change suppression was removed from the API and migrator; a
  snapshot/model drift now fails instead of being hidden.
- N-1 compatibility no longer calls migration `Down`. The test performs
  legacy-shaped reads and writes against the retained forward schema.
- The production-like upgrade fixture preserves an old Web Vitals row, verifies
  additive defaults, and verifies that an N-1-shaped insert still succeeds
  after the new columns are applied.
- SQLite outbox tests translate database time to `CURRENT_TIMESTAMP` only in
  their command interceptor. Production continues to use PostgreSQL database
  time and `FOR UPDATE SKIP LOCKED`. Tests reload persisted rows after
  conditional updates and use `NextAttemptAt` as the retry eligibility
  contract.
- The production readiness contract now follows the outbox locking SQL in
  `OutboxLeaseStore`, where ownership moved during the short-lease refactor.

## Local no-download evidence

| Gate | Result |
|---|---|
| API build with `--no-restore` | PASS — 0 warnings, 0 errors |
| Migration registration, complete idempotent script, and snapshot parity | PASS — 3/3 |
| Outbox processor SQLite compatibility | PASS — 11/11 |
| Shared-file storage contracts | PASS — 7/7 |
| Cluster health and bootstrap migration contracts | PASS — 6/6 |
| Production Python contract suite | PASS — 418 passed, 6 suite-declared skips |
| Static Compose YAML and required service inventory | PASS — 11/11 services |
| Complete solution build with `--no-restore` | PARTIAL — all projects with local assets built; Migrator and AdminBootstrap assets are absent |

The missing files are
`backend/src/NaderGorge.Migrator/obj/project.assets.json` and
`backend/src/NaderGorge.AdminBootstrap/obj/project.assets.json`. No restore was
run because local downloads and installs are prohibited.

No Docker daemon, image build, image pull, PostgreSQL instance, Redis instance,
or remote node was used.

## Mandatory remote gates

1. Restore from the reviewed lock inputs on the remote builder and build the
   complete solution, including Migrator and AdminBootstrap, from the sealed
   source.
2. Run `PerformanceSchemaCompatibilityTests` against isolated PostgreSQL
   databases for:
   - the complete migration chain on an empty database;
   - the production-like pre-Web-Vitals schema and representative legacy row;
   - N-1 legacy reads and writes after the forward schema is retained.
3. Apply the complete idempotent migration script to a sanitized
   production-like snapshot and prove there are no pending migrations or model
   differences afterward.
4. Run current and prior verified application smokes against the expanded
   schema. Do not invoke `Down`, restore PostgreSQL, or remove the new columns
   during application rollback.
5. Run PostgreSQL/Redis cluster coordination tests, cross-node SignalR, outbox
   lease takeover, and shared-file failover against the reviewed three-node
   topology.
6. Run Docker Compose validation, build the backend/frontend/worker/migrator
   images, start the complete stack, and verify readiness, queues, shared
   assets, and release/node identity.
7. Repeat the migration and release gates if the sealed source changes.

T104 and T110 cover the source reconciliation and test contracts only. They do
not substitute for the remote execution, sealed-candidate, migration-apply, or
production rollout gates in T113–T122.
