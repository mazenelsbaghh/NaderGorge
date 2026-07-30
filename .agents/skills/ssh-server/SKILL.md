---
name: ssh-server
description: Plan affected components, enforce EF migrations, selectively build, and safely inspect, debug, or deploy the three-node Massar Production cluster through strict SSH and reviewed commands. Use for backend, frontend, worker, database, Docker, urgent releases, health, logs, backups, Cloudflare, Redis, SignalR, files, and load tests.
---

# SSH Server — Massar Production

Use this skill for every Production operation. The cluster is exactly
`node-1`, `node-2`, and `node-3`, defined only in
`deploy/production/inventory/production.yml`. Never paste a server address in
an operational command.

## Choose the smallest safe lane first

Classify the request before running any command:

| Request | Lane | Required action |
|---|---|---|
| Read status, audit, backups, Cloudflare health, or bounded/redacted logs | **Quick read-only** | Run the matching `massar.sh` command directly. Do not run `make ops-plan`, `make ops-check`, Docker builds, or a release plan. |
| Preview a restore or failover drill | **Quick preview** | Run the matching `massar.sh` `*-dry` command directly. |
| Change code, configuration, schema, images, database state, or Production services | **Change/release** | Follow the full workflow below. |

Never broaden a small request into a cluster-wide investigation unless the
first focused command reports an unhealthy result. For a node-specific log
request, query only that node and exact service; expand to all nodes only when
the result indicates a distributed problem. The quick lanes are read-only and
retain strict host-key verification, inventory-only targeting, redaction, and
timestamped evidence.

### Quick read-only and preview workflow

For small operational questions, set the SSH environment and run just one
focused command:

```bash
export MASSAR_KNOWN_HOSTS_FILE="/Users/mazenelsbagh/.ssh/massar_prod_known_hosts"
export MASSAR_SSH_IDENTITY_FILE="/Users/mazenelsbagh/.ssh/massar_prod_cluster_ed25519"

# Examples — use only the command that answers the request.
bash .agents/skills/ssh-server/scripts/massar.sh status
bash .agents/skills/ssh-server/scripts/massar.sh logs node-2 backend 20
bash .agents/skills/ssh-server/scripts/massar.sh backups
bash .agents/skills/ssh-server/scripts/massar.sh failover-dry
```

Report the focused result and evidence path. Stop after a healthy result.
Escalate to the change/release workflow only for an unhealthy result, a request
to diagnose beyond the returned evidence, or any requested state change.

## Change/release workflow

For a code, configuration, database, image, or Production-service change,
start with:

```bash
make ops-plan
make ops-check
```

### Required build-scope decision

Before running any build command, ask the operator to explicitly choose the
build scope. Do not assume it from a request such as “a small change” or “one
word changed.” Use exactly one of: `frontend`, `backend`, `worker`, or `all`.

State the detected affected components from `make ops-plan`, then ask: “Which
component should be built first: frontend, backend, worker, or all?” Record
the chosen scope in the release/change note before continuing. Refuse an
incompatible choice:

| Detected change | Allowed choice |
|---|---|
| Only `frontend/` | `frontend` or `all` |
| Only backend/API without EF model change | `backend` or `all` |
| Only `worker/` | `worker` or `all` |
| More than one affected component | `all` |
| EF entities, context, migrations, Compose/Production tooling, or an unknown affected area | `all` |

The scope is an explicit intent and a focused local-verification/build choice.
The current immutable Production release contract still assembles all four
images into one digest-parity manifest; never claim that selecting one scope
alone permits a partial Production deployment. For a genuine faster
Production lane, first implement and test selective artifact reuse in the
release tooling, then update this rule and its tests.

`ops-plan` reads the Git delta and prints:

- affected components: frontend, backend, worker, database, infrastructure;
- a conservative list of local Docker images affected by build-context files;
- whether an EF migration exists or is required;
- the four immutable images required by the Production release contract.

Then choose one path:

```bash
make ops-build                 # build only affected local images
make ops-fast                  # urgent local checks + cached affected build
make prod-db-inventory         # compare expected/live DB tables, read-only
make prod-db-fast-preview REASON="incident or change reference"
make prod-plan                 # Production plan; no state change
make prod-release-preview RELEASE=... MANIFEST=... BACKUP_EVIDENCE=...
```

Every helper prints timestamped steps. Local checks and Docker output stream
live. Long remote operations emit a heartbeat every 15 seconds and then print
the final release/evidence result; the strict SSH transport may buffer remote
Docker output until that bounded step finishes.

## Ready-to-use environment

At the beginning of every terminal session run exactly:

```bash
export MASSAR_KNOWN_HOSTS_FILE="/Users/mazenelsbagh/.ssh/massar_prod_known_hosts"
export MASSAR_SSH_IDENTITY_FILE="/Users/mazenelsbagh/.ssh/massar_prod_cluster_ed25519"
```

Then use these two references:

```bash
CLUSTER="python3 deploy/production/scripts/clusterctl.py"
INVENTORY="deploy/production/inventory/production.yml"
```

For routine, safe commands use the bundled wrapper:

```bash
bash .agents/skills/ssh-server/scripts/massar.sh status
bash .agents/skills/ssh-server/scripts/massar.sh logs node-2 backend 20
bash .agents/skills/ssh-server/scripts/massar.sh backups
bash .agents/skills/ssh-server/scripts/massar.sh failover-dry
```

Run `bash .agents/skills/ssh-server/scripts/massar.sh --help` for all safe
commands. The wrapper uses the inventory and strict SSH transport, checks the
two local SSH files (including key mode `0600`), stores evidence under
`artifacts/production/`, redacts sensitive log fields, and intentionally
offers no mutating command.

The private key is a path, not a value. Do not print, copy, commit, upload, or
change it. It must remain mode `0600`; strict host-key verification is always
required. Strict host-key verification is mandatory.

The higher-level helpers are:

```bash
bash .agents/skills/ssh-server/scripts/ops.sh --help
bash .agents/skills/ssh-server/scripts/database.sh --help
bash .agents/skills/ssh-server/scripts/deploy.sh --help
```

`ops.sh` is local/read-only except the explicit `db-add` scaffold and Docker
build commands. `deploy.sh` is preview-only unless `--yes` is supplied.

## Non-negotiable safety rules

1. Begin with `status` or `audit`; save evidence under `artifacts/production/`.
2. For every state change: run `--dry-run`, inspect it, then run `--yes`.
3. Use `massar-ops` and the strict transport only. Root/password SSH is rescue
   only; never put passwords, tokens, private keys, or admin values in argv,
   logs, evidence, source, or chat.
4. Never mutate two quorum members at once. Stop on host-key mismatch, missing
   etcd/Sentinel/Gluster quorum, duplicate PostgreSQL writer/Redis master,
   stale backup, failed isolated restore, or image-digest mismatch.
5. A Patroni replica and a Gluster brick are not backups. Never restore against the production DB;
   use only the isolated restore commands.
6. No domain/DNS cutover until `accept` produces signed `GO` evidence.
7. Never deploy an EF model change without a new migration. `make
   ops-db-guard` is mandatory and fails closed.
8. Never install `dotnet-ef`, SDKs, packages, or container images implicitly.
   `ops-db-migration` uses only an already-installed tool and otherwise stops.
   Local Docker builds require every base image to exist, use `--pull=false`
   and `--network=none`, and stop instead of downloading.
9. “Fast” means focused verification and cached builds. It never skips
   Production health, backup/restore evidence, migration coverage, dry-run,
   rolling node order, or automatic application rollback.
10. Missing Production tables are repaired only by repository EF migrations.
    Never synthesize a table with ad-hoc SQL. Unknown server migrations or a
    missing table with no pending reviewed migration fail closed.

## Change and Docker decision table

| Changed area | Focused checks | Local Docker | Production Docker |
|---|---|---|---|
| `frontend/` | offline lint + typecheck | shared `frontend` | all four immutable images |
| backend/API/application | `.NET` application tests, `--no-restore` | `backend` | all four immutable images |
| EF entities/context/migrations | migration pair + snapshot + EF pending-model check | `backend`, `migrator` | all four immutable images |
| `worker/` | offline worker build/tests | `worker` | all four immutable images |
| `docker/nginx/` | Compose contract | `gateway` | foundation workflow, not app release |
| Production/Compose/skill tooling | contract checks | only changed build contexts | all four immutable images |

Production intentionally rebuilds backend, frontend, worker, and migrator
together. Selective Production images would break the single immutable release
manifest and digest-parity guarantees. Remote build cache provides speed while
retaining one auditable release identity.

The explicit DB-only repair lane is the exception because it changes no
application image or release. It can apply only migrations already embedded in
the migrator image of the current immutable Production release. This repairs a
server that missed a reviewed migration without rebuilding anything. A new
repository migration that was never shipped still requires the immutable
release path; the helper fails closed instead of running unbound SQL.

## Database and EF migrations

Before any build or release:

```bash
make ops-db-guard
```

If it reports an EF model change without a migration:

```bash
make ops-db-migration NAME=DescribeTheSchemaChange
make ops-db-guard
```

The guard includes additions, edits, renames, and deletions from the reviewed
Git merge-base. For schema inputs it requires a newly added main migration,
its new `.Designer.cs` pair, and a changed model snapshot. It then builds with
`--no-restore` and runs EF's pending-model check. Missing tools or stale/missing
artifacts fail closed. The scaffold command never downloads `dotnet-ef`; if
the tool is unavailable it stops and explains the prerequisite. Review the
generated migration and snapshot before commit. Never generate or edit a
migration directly on a Production node.

### Expected tables versus the live server

```bash
make prod-db-inventory
```

This writes two timestamped files under
`artifacts/production/schema-inventory/`: the raw read-only server catalog and
the comparison with `AppDbContextModelSnapshot.cs` plus repository migration
IDs. The comparison lists expected/actual/missing/extra tables and
pending/unexpected migrations. It does not expose row values or credentials.

If a missing table is covered by migrations already shipped in the current
Production migrator, the DB-only lane can create it. If the current release
does not contain the migration, it stops and requires the immutable release
path:

```bash
make prod-db-fast-preview REASON="Backward-compatible schema incident"
make prod-db-fast \
  REASON="Backward-compatible schema incident" \
  CONFIRM=DB-ONLY
```

This path does not build, distribute, restart, or roll backend, frontend, or
worker images. It proves identical current manifests and digests on all three
nodes, checks that every pending migration is in that current manifest, runs
three-node health and a read-only pre-inventory, then previews the existing
migration gate. The confirmed command creates a fresh encrypted full backup,
migrates an isolated restored copy, boots the exact current backend against it
for N-1 compatibility, destroys the copy, performs a migrate dry-run, runs the
reviewed current migrator once under its advisory lock, and requires the
post-inventory to match. Database Down/restore rollback remains prohibited; a
failure requires a reviewed forward-fix migration. The current application
release remains unchanged.

## Urgent safe path

For a time-sensitive fix, obtain the required build-scope decision first. Then:

```bash
make ops-fast
make prod-fast-release \
  RELEASE=src-... \
  MANIFEST=artifacts/production/build/src-.../manifest.json \
  BACKUP_EVIDENCE=artifacts/production/migration-gates/src-....json \
  REASON="Customer-facing incident reference"
```

`prod-fast-release` requires an explicit reason and `--yes` internally. It
still runs the DB guard, three-node status, migrate dry-run, deploy dry-run,
serialized migration, node-3 → node-2 → node-1 rollout, final health, and the
existing application-only automatic rollback contract.

## Fast daily workflow

```bash
$CLUSTER --inventory "$INVENTORY" status --node all \
  --evidence-dir artifacts/production/status

$CLUSTER --inventory "$INVENTORY" backup-schedules-status --node all \
  --evidence-dir artifacts/production/backup-status

$CLUSTER --inventory "$INVENTORY" cloudflare-status --node all \
  --evidence-dir artifacts/production/cloudflare-status
```

Interpretation:

- `status: success` means the three-node health/quorum/release gate passed.
- `backup-schedules-status: success` means the scheduled encrypted backup
  checks are healthy; it does not replace an isolated restore test.
- `cloudflare-status: success` means all configured tunnel replicas are
  reachable. It does not by itself authorize a DNS cutover.

## Debug a Production bug

1. Capture `status` first.
2. Identify the affected surface: public root, `app`, `admin`, `teacher`,
   `staff`, `api`, `ws`, or `assets`.
3. Read logs and health through `deploy/production/scripts/ssh_transport.py`
   using an inventory-selected `SshTarget`; scope commands to an exact service,
   time window, and node. Never use raw addresses or a permissive SSH command.
4. Check all three nodes for errors before assuming one node is responsible.
5. Reproduce with a harmless read-only request where possible. For API errors,
   record status code and correlation/time window, never user data or tokens.
6. Fix locally, run focused checks, then use the release flow below. Do not
   patch application containers by hand.

Useful service names are `massar_production-backend-1`, the surface containers,
and `massar_production-gateway-1`. Keep log output redacted; do not include
Authorization, cookies, phone numbers, or result records in evidence.

## Release flow (only after the user requests deployment)

Build Production release images only on remote builder `node-3`; local
offline Docker builds are disposable developer checks, never release inputs.

```bash
# 1. Compute the immutable source release ID locally (does not build).
python3 - <<'PY'
import sys
from pathlib import Path
sys.path.insert(0, "deploy/production/scripts")
from release_images import source_state
print(source_state(Path("."))["releaseId"])
PY

# 2. Build once remotely, distribute exact digests to all nodes.
$CLUSTER --inventory "$INVENTORY" build --node all --release "src-<sha>" \
  --remote-builder --dry-run --evidence-dir artifacts/production/build
$CLUSTER --inventory "$INVENTORY" build --node all --release "src-<sha>" \
  --remote-builder --yes --evidence-dir artifacts/production/build

# 3. Produce a fresh encrypted-backup + isolated-restore migration gate.
make prod-gate RELEASE=src-<sha>

# 4. Migrate once, then rolling deploy node-3 → node-2 → node-1.
$CLUSTER --inventory "$INVENTORY" migrate --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gates/src-<sha>.json --dry-run
$CLUSTER --inventory "$INVENTORY" migrate --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gates/src-<sha>.json --yes
$CLUSTER --inventory "$INVENTORY" deploy --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gates/src-<sha>.json --dry-run
$CLUSTER --inventory "$INVENTORY" deploy --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gates/src-<sha>.json --yes
```

Verify `status --node all` after every release. If a gate fails, stop; do not
force deployment or hand-edit a remote release.

The same flow is available through Make with live local output and remote
heartbeats:

```bash
make prod-build-preview RELEASE=src-...
make prod-build RELEASE=src-...
make prod-gate RELEASE=src-...
make prod-release-preview RELEASE=src-...
make prod-release RELEASE=src-...
```

`MANIFEST` and `BACKUP_EVIDENCE` default to the release-bound artifact paths
shown by `make help` and can be overridden explicitly.

## Data, file, and recovery operations

```bash
# Safe read-only planning/status
$CLUSTER --inventory "$INVENTORY" backup-repository-plan --node all
$CLUSTER --inventory "$INVENTORY" backup-schedules-status --node all

# Bounded drills: always dry-run first.
$CLUSTER --inventory "$INVENTORY" restore-test --node node-3 --dry-run
$CLUSTER --inventory "$INVENTORY" file-failover-test --node node-1 \
  --maximum-outage-seconds 30 --dry-run
$CLUSTER --inventory "$INVENTORY" failover-test --node all --dry-run
```

`failover-test` is a real bounded PostgreSQL/Redis drill and may briefly move
one writer. Run it only with a healthy preflight and wait for full recovery
before any other mutation.

## SignalR, files, and load tests

- SignalR/load probes require a disposable test account and externally stored
  `0600` token files. Create them with
  `deploy/production/scripts/prepare_load_test_tokens.py`; never use a real
  user account or place credentials in the repository.
- File validation must prove upload → read from another node → delete, without
  logging file contents or user identifiers.
- Run the 30-minute load plan only after approval and during a safe window.
  It must collect p95/p99/errors, CPU steal, database replication, Redis, and
  queue evidence. See `docs/production/performance-and-ha-validation.md`.

## Cloudflare and domains

Use the one tunnel with replicas on all three nodes. The connector targets each
node's local HAProxy, which balances across all three application nodes. The
only final public hostnames are the root plus `app`, `admin`, `teacher`,
`staff`, `api`, `ws`, and `assets`.

Before any DNS change, run `accept` and require signed `GO`. Then follow
`docs/production/cloudflare-cutover.md`; use Full (strict), proxy web hosts,
keep origin locked down, and verify HTTP, WebSocket, cookies, uploads, and
protected assets after cutover.

## References

- Architecture and exact roles: `specs/166-three-node-production-cluster/architecture.md`
- Database/schema audit implementation:
  `deploy/production/scripts/audit_database.py`
- Historical generated comparison (not current production evidence):
  `.agents/skills/ssh-server/docs/database_schema.md`
- Backup and recovery: `docs/production/backup-and-restore.md`
- Performance/HA test procedure: `docs/production/performance-and-ha-validation.md`
