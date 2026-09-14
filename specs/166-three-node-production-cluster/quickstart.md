# Production Cluster Quickstart

This is the execution order, not permission to skip a failed gate. Run against
the explicit production inventory only. Never paste passwords into commands.

## 0. Prerequisites

- Cloudflare zone ownership for `massar-academy.net` and permission to create
  one tunnel with eight public hostnames.
- At least 50 GB of protected backup capacity on each approved server. The
  bootstrap creates one internal Garage bucket with replication factor 3.
- Operator SSH public key and a reviewed rescue path in Hostinger hPanel.
- All application/provider secrets available in a root-only secret directory.

Cloudflare access is required only for the protected rehearsal and domain
cutover. No external S3/R2 account is required.

Export references to the protected operator files before using `clusterctl`:

```bash
export MASSAR_KNOWN_HOSTS_FILE=/protected/path/massar_prod_known_hosts
export MASSAR_SSH_IDENTITY_FILE=/protected/path/massar_prod_cluster_ed25519
```

These variables contain file locations, not secret values. The identity and
known-hosts files must already exist with operator-only permissions.

## 1. Read-only audit and inventory

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml audit \
  --node all \
  --evidence-dir artifacts/production/audit
```

Confirm the three approved hosts, pinned host keys, 8 vCPU/31 GiB/387 GiB,
Ubuntu 26.04, inter-node reachability, clock sync and empty production state.

## 2. Bootstrap secure hosts

First establish and rescue-test the `massar-ops` Ed25519 access path using the
reviewed `ssh-server` access runbook. Only after that path works, initialize the
idempotent cluster marker, directories, clock service and Cairo timezone:

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml bootstrap \
  --node all \
  --dry-run

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml bootstrap \
  --node all \
  --yes
```

Verify key-based `massar-ops` and rescue sudo before disabling routine
root/password SSH. Rotate the credential already exposed during test setup.

## 3. Establish quorum services

Bootstrap and verify:

1. WireGuard full mesh and public-interface firewall.
2. etcd quorum, Patroni/PostgreSQL members and one writer.
3. Redis replicas and three Sentinels with one master.
4. GlusterFS primary data brick, live data brick and arbiter; mount the same
   volume on every node.
5. HAProxy pools and local stable DB writer endpoint.
6. Internal three-node Garage bucket, pgBackRest/Restic, and timers.

Create the internal bucket only after the read-only capacity plan and dry-run:

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  backup-repository-plan --node all

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  backup-repository --node all \
  --secret-dir /protected/path/massar_prod_secrets \
  --capacity-per-node 50GB --dry-run

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  backup-repository --node all \
  --secret-dir /protected/path/massar_prod_secrets \
  --capacity-per-node 50GB --yes

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  backup-schedules-activate --node all --dry-run

# Run this only after one encrypted DB/file backup and both isolated restores pass.
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  backup-schedules-activate --node all --yes

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  backup-schedules-status --node all
```

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml status \
  --evidence-dir artifacts/production/baseline
```

Do not proceed if etcd/Sentinel/Gluster/Garage quorum or backup encryption
fails.

## 4. Build once and initialize the empty database

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml build \
  --release "git-$(git rev-parse HEAD)" --yes

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml migrate \
  --release "git-$(git rev-parse HEAD)" \
  --manifest "artifacts/production/git-$(git rev-parse HEAD)/manifest.json" \
  --yes
```

The migration gate first migrates an isolated empty audit database, compares the
schema and checks for unapproved demo/default users. Only then may it migrate
the empty production database once.

This gate must specifically prove that the known legacy fixed Admin and
teacher/subject bootstrap rows from historical migrations are absent. Do not
work around that failure by deleting rows manually in Production.

## 5. Create the first Admin safely

Use the no-echo helper:

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml bootstrap-admin \
  --node all --yes
```

Before running it, establish a protected SSH tunnel to a node-local PostgreSQL
writer endpoint and set `ConnectionStrings__DefaultConnection` from the
operator's external secret store. The command never discovers or prints a
database password. Enter the owner-approved phone/password only at the
protected prompt. Verify login and Admin role, rotate the initial password if
policy requires, and confirm no value appeared in history/logs/artifacts.

## 6. Rolling application deploy

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml deploy \
  --release "git-$(git rev-parse HEAD)" \
  --manifest "artifacts/production/git-$(git rev-parse HEAD)/manifest.json" \
  --backup-evidence artifacts/production/backup-gate.json \
  --evidence-dir artifacts/production/deploy \
  --yes
```

The order is node-3, node-2, node-1. Each node must be drained, updated by exact
image digest, ready/smoke-tested and returned before the next node.

## 7. Acceptance before DNS

Run:

- `make verify`;
- production compose/config tests;
- 300-request distribution sample with all three node headers;
- one-at-a-time app/ingress/PostgreSQL/Redis/file failure drills;
- cross-node SignalR and BullMQ duplicate/retry tests;
- public/private upload checks from every node;
- isolated DB PITR and hourly file restore;
- 30-minute 2× single-node baseline load test;
- public port/direct-origin negative scan.

Every result must be attached to one immutable release. Any critical failure or
unverified restore keeps status `NO-GO`.

## 8. Cloudflare cutover after GO

1. Create one locally managed Tunnel from the protected operator workstation
   and keep its generated credentials JSON outside the repository with mode
   `0600`.
2. Install the same tunnel UUID, credentials and rendered config as three
   replicas, one per server.
3. Add the eight public hostname mappings to each connector's local HAProxy:
   root, `app`, `admin`, `teacher`, `staff`, `api`, `ws`, `assets`.
4. Use proxied records created by Tunnel; do not add three raw public-origin A
   records.
5. Set SSL/TLS to Full (strict), enable Always Use HTTPS, WAF managed rules and
   suitable auth/API rate limits.
6. Verify HTTP, cookies, CORS, WebSocket, uploads and protected assets.
7. Deny direct public origin HTTP/HTTPS and all internal ports; keep outbound
   tunnel connectivity.
8. Repeat node/tunnel failure and external synthetic checks before announcing
   production.

Cloudflare Tunnel replicas provide connector failover. Per-node HAProxy pools
perform the actual load distribution across all three application servers.

## 9. Ongoing operations

- Monitor DB sync/quorum, Redis Sentinel, Gluster heal/split-brain, disk/inodes,
  queue backlog, certificates/tunnel, backup age and load.
- Daily DB differential and weekly full in measured quiet windows; continuous WAL; hourly files.
- Rolling 30-day retention in the encrypted internal three-node Garage
  repository. This is not an off-site disaster copy.
- Monthly isolated DB+file restore test and documented failover drill.
- Rolling releases only; no automatic destructive DB down-migrations.
