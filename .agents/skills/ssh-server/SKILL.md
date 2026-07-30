---
name: ssh-server
description: Safely inspect, debug, deploy, fail over, back up, restore-test, and operate the three-node Massar Production cluster through strict SSH and the reviewed cluster commands. Use for production bugs, health, logs, releases, Cloudflare, database, Redis, SignalR, files, and load tests.
---

# SSH Server — Massar Production

Use this skill for every Production operation. The cluster is exactly
`node-1`, `node-2`, and `node-3`, defined only in
`deploy/production/inventory/production.yml`. Never paste a server address in
an operational command.

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

Build only on remote builder `node-3`; never build application images locally.

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
python3 deploy/production/scripts/prepare_release_migration_gate.py --help

# 4. Migrate once, then rolling deploy node-3 → node-2 → node-1.
$CLUSTER --inventory "$INVENTORY" migrate --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gate.json --dry-run
$CLUSTER --inventory "$INVENTORY" migrate --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gate.json --yes
$CLUSTER --inventory "$INVENTORY" deploy --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gate.json --dry-run
$CLUSTER --inventory "$INVENTORY" deploy --node all --release "src-<sha>" \
  --manifest artifacts/production/build/src-<sha>/manifest.json \
  --backup-evidence artifacts/production/migration-gate.json --yes
```

Verify `status --node all` after every release. If a gate fails, stop; do not
force deployment or hand-edit a remote release.

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
