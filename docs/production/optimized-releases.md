# Incremental production releases

The four-image release manifest and node-3 → node-2 → node-1 rollout are unchanged.
Fresh backup, isolated restore, N-1 compatibility, serialized EF migration,
health checks and application rollback still run for each deployment.

## Build and distribution

`optimized_builder.py` runs only through the root-owned builder on node-3.
It refreshes base images, then fingerprints each complete immutable build context,
the base image configuration digests, architecture, and builder policy. A matching
root-owned cache record must agree with the registry manifest and Docker image identity
before reuse. Missing cache records build normally; corrupt records or a failed
registry stop the release. There is no fallback to workstation image transfers.

Backend and migrator use the `backend` and `migrator` targets in
`backend/Dockerfile`. They share the compiled stage, including common project
outputs. npm, NuGet and Next.js use persistent BuildKit cache mounts.
Frontend web-vitals `releaseId` identifies its content-addressed image inputs
(`src-<input hash prefix>`); the deployed release is still recorded by the full
four-image manifest and container labels. Unrelated releases can reuse that image.

The node-3 registry binds only its inventory WireGuard address on port 5443.
It requires a verified client certificate. Docker pulls by registry manifest
digest and downloads missing layers; the distributor separately verifies the
Docker image identity and platform on all three nodes. Final v2 manifests
accept either historical archive evidence or `imageDigest`, `registryDigest`,
and `inputSha256` evidence. Historical release validation remains supported.

## Install and verify

Use the operator SSH environment from the [operations skill](../../.agents/skills/ssh-server/SKILL.md).
Run `make ops-plan`, `make ops-check`, and capture healthy three-node status first.
The owner selected `all` for this change, which affects every build context and
release infrastructure. No EF schema changes are included.

```bash
make prod-registry-preview
make prod-registry-install
make prod-registry-status
```

Installation explicitly pulls the pinned registry image on node-3, installs
per-node Docker client trust, and starts `massar-image-registry.service`.
It does not restart Docker or application services. Install the reviewed builder
with `install_remote_builder.py --dry-run` followed by `--yes`, passing its
required inventory, known-hosts, identity, and `--node node-3` arguments.
This installer also installs the SHA-256-verified official Buildx v0.37.1
plugin on node-3, without restarting Docker. The builder explicitly enables
BuildKit. Normal `prod-build`, `prod-gate`, and `prod-release` commands then use this path.

Certificate keys live outside Git at `~/.config/massar/image-registry` with
private permissions. The CA key stays on the operator machine; each node
receives only its own client key, and node-3 receives the server key. Preserve
this directory securely. Installation refuses an incomplete credential set or
a different installed CA identity. The initial CA key-usage compatibility repair is available through
`install_image_registry.py --dry-run --repair-ca-usage` followed by
`--yes --repair-ca-usage`. It preserves the signing key, verifies existing leaf
certificates strictly, and restarts only the registry. Its recovery marker
limits accepted previous trust to the saved CA fingerprint.

Leaf certificates last 825 days; renewal
requires a reviewed rotation before expiry. The registry data directory is
`/var/lib/massar/image-registry`; no automatic deletion or garbage collection
is enabled, so monitor its disk usage. Running applications do not depend on
registry availability; new releases do.

## Measure a warm build

After a successful optimized build, use its exact manifest path:

```bash
python3 deploy/production/scripts/benchmark_release_cache.py \
  --manifest "$MANIFEST" --output "$EVIDENCE" --dry-run
python3 deploy/production/scripts/benchmark_release_cache.py \
  --manifest "$MANIFEST" --output "$EVIDENCE" --yes
```

The probe refreshes base identities, requires all four cached input fingerprints,
verifies registry/image digests, and records elapsed seconds and zero image builds.
It refuses changed inputs instead of rebuilding. It neither deploys applications
nor overwrites the immutable release manifest. Initial cache population and later
warm runs are different measurements; do not describe the first run as warm.

Upstream references: [Docker build cache](https://docs.docker.com/build/cache/optimize/)
and [Distribution TLS client authentication](https://distribution.github.io/distribution/about/configuration/#tls).
