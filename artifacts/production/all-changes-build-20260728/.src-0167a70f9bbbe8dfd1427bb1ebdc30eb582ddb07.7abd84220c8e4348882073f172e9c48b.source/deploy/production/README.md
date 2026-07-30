# Massar Three-Node Production Operations

This directory is the source of truth for the pre-domain three-node cluster.
It contains no secret values. Runtime secrets live on the operator host and
servers in root-only files referenced by `config/secrets.manifest.example.yml`.

Safety rules:

- Run every command with an explicit inventory and target.
- `audit`, `status`, and `cloudflare-status` are read-only. State-changing commands require
  `--yes`; use `--dry-run` first.
- SSH host identities are pinned. Unknown or changed keys are a hard failure.
- Never operate on more than one quorum member at a time.
- Never restore into the production database or shared mount.
- Evidence is written beneath a caller-selected directory and is redacted.
- The final domain remains blocked until `accept` returns `GO`.

Entry point:

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  audit --node all --evidence-dir artifacts/production/audit
```

Failover command scope is explicit:

- `failover-test --node all` resolves and drills the current PostgreSQL writer,
  waits for full recovery, then resolves and drills the current Redis master.
  It rejects a fixed node because the two leaders are dynamic and may be on
  different nodes.
- `file-failover-test --node node-1|node-2` isolates exactly one full Gluster
  data brick, proves checksum continuity and heal, then restores it. The
  node-3 arbiter is always refused.
- Application/ingress drain uses `drain` for exactly one named node. Full
  app, worker, and tunnel connector failures remain separate acceptance drills
  from `tests/chaos/scenarios.json`; neither `failover-test` command claims to
  execute them.

Before the first strict rollback rehearsal, collect the already-running
previous release manifest from all three nodes:

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  collect-current-manifest --node all \
  --manifest-output artifacts/production/current-release/manifest.json \
  --output artifacts/production/current-release/evidence.json
```

If the legacy release root exists but its manifest/sidecars are absent, the
collector intentionally fails. Install the reviewed helpers with
`backup-repository-sync-clients`, then seal that exact running release before
collecting again:

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  seal-legacy-release --node all \
  --output artifacts/production/current-release/legacy-seal.json \
  --dry-run

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  seal-legacy-release --node all \
  --output artifacts/production/current-release/legacy-seal.json \
  --yes
```

The seal derives identity only from the exact eight healthy running services,
requires the backend/frontend/worker image IDs and the three-file recovery
bundle (base compose, app compose, and Nginx template) to match on all nodes,
creates standard `massar/...` aliases for those exact existing image IDs, then
creates only the three missing metadata files with
no-overwrite semantics. Its manifest uses strict
`sealedLegacyProvenance`; it never invents a Git commit or source snapshot.
Any partial operation is reconciled by its root-only inode journal or removed
by compensating cleanup. Run `collect-current-manifest` again after sealing and
use only that fresh evidence for normalization.

When `/opt/massar/current` exists, the collector accepts only that controlled
symlink and never falls back around an invalid or broken pointer. On an initial
legacy bootstrap where the pointer is absent, it derives the release only from
the exact eight healthy `massar_production` Docker services, requires identical
release/node labels, checks the manifest's strict image set (four for a source
build, three for sealed Legacy recovery) against local tagged image IDs, and
verifies `.release-files.sha256` when the sidecar exists. It
publishes neither local output if the three nodes differ or either output
already exists.

After a successful fallback collection, install the reviewed root helper with
`backup-repository-sync-clients`, dry-run the normalization, and then create
the missing pointer on all three nodes:

```bash
python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  normalize-current-manifest --node all \
  --manifest artifacts/production/current-release/manifest.json \
  --collector-evidence artifacts/production/current-release/evidence.json \
  --output artifacts/production/current-release/normalization.json \
  --dry-run

python3 deploy/production/scripts/clusterctl.py \
  --inventory deploy/production/inventory/production.yml \
  normalize-current-manifest --node all \
  --manifest artifacts/production/current-release/manifest.json \
  --collector-evidence artifacts/production/current-release/evidence.json \
  --output artifacts/production/current-release/normalization.json \
  --yes
```

Normalization accepts collector evidence no older than 15 minutes, only
the `docker-label-fallback` mode, and a verified `.release-files.sha256` equal
to the manifest on every node. It refuses any existing `current` path,
publishes each symlink without overwrite, verifies the exact inode and target
on all nodes, and uses a root-only operation marker for compensating rollback
if any node or evidence publication fails.

Topology and recovery decisions are documented in
`specs/166-three-node-production-cluster/`.
