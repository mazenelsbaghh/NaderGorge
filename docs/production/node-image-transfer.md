# Direct image transfer between production nodes

Release image archives travel from node-3 to node-1 and node-2 over `wg0`.
The operator sends the source snapshot, small release metadata, and control
commands through the existing pinned SSH connection. Image bytes do not pass
through the operator workstation. Building, loading images, migration gates,
and the node-3 → node-2 → node-1 application rollout keep their existing behavior.

## Installation

Configure `MASSAR_KNOWN_HOSTS_FILE` and `MASSAR_SSH_IDENTITY_FILE` as described
in the [SSH operations skill](../../.agents/skills/ssh-server/SKILL.md).
Review the affected changes with `make ops-plan` and `make ops-check`, and
capture a healthy `make prod-status` before installation.

```bash
make prod-image-transfer-preview
make prod-image-transfer-install
```

The installer runs sequentially as `massar-ops`. It installs
`node_image_transfer.py` at
`/home/massar-ops/.local/libexec/massar-node-image-transfer.py`, with its
inventory-derived configuration under `.config/massar-image-transfer.json`.
It does not restart an application, install a package, alter sudo permissions,
or change the database. Re-run the installer when this helper or inventory
changes; an existing dedicated transfer identity is reused.

Node-3 creates its own mode-0600 Ed25519 identity at
`.ssh/massar-image-transfer-ed25519`. The private key stays on node-3.
Receiver host keys are derived from the operator's already-pinned Ed25519
keys, including hashed known-hosts entries; the installer does not trust
unauthenticated network key scans. They are installed as node aliases in
`.ssh/massar-image-transfer-known-hosts`.

The receiver authorization is restricted to the builder's WireGuard source
address and a forced receive command. OpenSSH `restrict` disables shell-adjacent
facilities such as forwarding and PTYs; the forced receiver also rejects
arbitrary commands. Existing unrelated authorized keys are preserved.

## Release behavior and failures

The existing `make prod-build RELEASE=auto` and `prod-small` workflows use
direct transfer automatically. No transfer flag is required. Transfers remain
sequential, and matching images or completed release nodes are still reused.

The sender checks that the receiver route uses `wg0` and the builder address
from the installed inventory configuration. It uses the dedicated identity,
strict host-key checking, no SSH agent forwarding, and a bounded timeout.
The receiver accepts only the four known image names and full immutable
release IDs. It requires an operator-created, owned mode-0700 staging
directory under `/tmp/massar-<release>`.

Each archive is limited to 16 GiB, with a 20-minute sender timeout. Each
transfer has a declared byte count and SHA-256. The receiver rejects
truncation, extra bytes, checksum mismatches, unsafe paths, and existing
destination files. Verified bytes are published atomically without replacing
another file. A handled interruption removes the operation's partial file;
the existing distribution cleanup removes staging if a release fails.
After transfer, the release runner independently checks the archive against
the release manifest, loads Docker, and verifies the image digest.

A missing helper, identity, pin, or WireGuard route stops distribution.
There is no automatic fallback through the workstation. Inspect the reported
node, re-run the reviewed installer if configuration is missing, then retry
the same release. Do not disable host-key checks or replace a changed pin
without verifying the server identity.

## Measuring transfer time

Use an already-built release whose archives remain on node-3. Set `RELEASE_ID`
to that exact release ID and `RELEASE_MANIFEST` to its verified local manifest.
Run this while no build for that release is active:

```bash
python3 deploy/production/scripts/benchmark_node_image_transfer.py \
  --inventory deploy/production/inventory/production.yml \
  --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
  --identity "$MASSAR_SSH_IDENTITY_FILE" \
  --release "$RELEASE_ID" --manifest "$RELEASE_MANIFEST" \
  --output artifacts/production/image-transfer/benchmark.json --dry-run

python3 deploy/production/scripts/benchmark_node_image_transfer.py \
  --inventory deploy/production/inventory/production.yml \
  --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
  --identity "$MASSAR_SSH_IDENTITY_FILE" \
  --release "$RELEASE_ID" --manifest "$RELEASE_MANIFEST" \
  --output artifacts/production/image-transfer/benchmark.json --yes
```

The benchmark transfers the four image archives to each receiver, checks
their receipts against the manifest, and removes its staging. It refuses an
existing staging directory and does not load images or deploy an application.
Evidence includes bytes, SHA-256, elapsed seconds, and MiB/s for each transfer.
These timings exclude build, Docker load, backup/restore gates, and rollout.

The [2026-09-12 measurement](node-image-transfer-benchmark-2026-09-12.json)
transferred the same 87,163,904-byte frontend archive from node-3 to node-2
in 154.263 seconds through the workstation and 2.863 seconds over WireGuard:
a 98.14% reduction in transfer time for that sample. Transferring all four
archives to both receivers, with checksum verification and staging cleanup,
took 68.147 seconds for 1,923,766,272 bytes. These are measured transfer
results, not a guarantee for future complete release duration.

Tests use the repository's existing Python environment:

```bash
PYTHONPATH=deploy/production/scripts .venv/bin/python -m pytest -q deploy/production/tests
```
