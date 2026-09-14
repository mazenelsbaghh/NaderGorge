# Quickstart: Platform Speed Completion

This is an execution checklist, not permission to skip `tasks.md`, tests, or the
reviewed production operating contract.

## 1. Confirm feature context

```bash
export SPECIFY_FEATURE=167-platform-speed-completion
.specify/scripts/bash/check-prerequisites.sh --json --paths-only
git branch --show-current
git status --short
```

Expected Git branch: `codex/167-platform-speed-completion`. Spec Kit resolves
the logical feature through `SPECIFY_FEATURE`.

Read:

- `specs/167-platform-speed-completion/spec.md`
- `specs/167-platform-speed-completion/plan.md`
- `specs/167-platform-speed-completion/contracts/`
- `docs/platform-speed-navigation-ui-audit-2026-07-29.md`
- `specs/166-three-node-production-cluster/plan.md`

## 2. Capture baseline and complete workspace inventory

Before performance implementation:

- enumerate actual tracked and untracked files, not directory summaries;
- hash and classify the complete current workspace state;
- scan for secrets and stop on a finding;
- build the current frontend in production mode and record route-specific
  initial/shared/deferred compressed transfer;
- record critical navigation timings, duplicate GET counts, API percentiles,
  live-support datastore command count, and existing Web Vitals sample details.

Never reset or omit an existing change. Fix any failure in its owning surface.

## 3. Implement in dependency order

1. Baseline/evidence and candidate-delta gate.
2. Root provider split and persistent shells, surface by surface.
3. Selective prefetch and canonical permission/navigation policy.
4. One query client, service cancellation, targeted realtime invalidation.
5. Admin student bounded pagination and export separation.
6. Entry WebGL/motion, bundle split, image/logo/font/static-cache changes.
7. Accessible drawers/carousels/loading/errors/focus and design-token gate.
8. Live-support projections, auth security cache/invalidation, outbox lease.
9. Segmented privacy-safe RUM, correlation, budgets, authenticated load.
10. Whole-workspace repair, complete verification, final reseal.

After each slice, run focused tests before moving to the next dependency.

## 4. Required local gates

Do not download anything onto the user's device. Do not run `npm install`,
`npm ci`, `dotnet restore`, Playwright browser installers, Docker pulls, or SDK
installers. Run only commands whose dependencies are already present. Record an
absent dependency as a local blocker and execute that gate later on the reviewed
remote builder against the sealed source.

`make verify` is the repository-wide contract, but its backend target performs
`dotnet restore`. Therefore it is reserved for the reviewed remote builder in
this run and must not be invoked on the user's device.

Focused frontend:

```bash
cd frontend
npm run lint
npm run typecheck
npm run check:platform-events
npm run check:live-support-contracts
npm run check:design-tokens
npm run check:accessibility
```

The production frontend build is part of the remote-builder gate. The root
layout uses `next/font/google`, so do not invoke a cold local build that may
fetch font assets.

Backend and worker:

```bash
dotnet build backend/NaderGorge.sln --no-restore
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-restore
cd worker
npm run build
npm test
```

Production tooling:

```bash
.venv/bin/python -m pytest -q deploy/production/tests
docker compose config -q
```

Run focused Playwright journeys after starting the backend in E2E mode according
to `docs/verification-contract.md`. If the required browser runtime is absent
locally, do not run an installer; execute `make verify-e2e` on the reviewed
remote builder.

## 5. Remote-builder Docker and migration gates

The commands in this section run only in the reviewed remote-builder/isolated
environment. Do not run them on the user's device: `make up` may pull or create
local Docker state, and migration verification requires disposable databases.

```bash
docker compose config -q
make up
make migrate
make ps
```

Verify backend, frontend surfaces, worker, PostgreSQL, Redis, SignalR, queues,
shared assets, static cache headers, and release/node identity. Repeat
migrations against empty and production-like isolated schemas and test both
current and candidate applications against the expanded schema.

## 6. Seal the final complete candidate

Immediately before build:

1. Re-enumerate all tracked/untracked paths and hashes.
2. Resolve any secret finding.
3. Produce a complete workspace/source digest and manifest.
4. Build all four application images once.
5. Compare workspace digest again.
6. Run the complete gates against those exact artifacts.

If any file changes at any point, mark the candidate ineligible, reseal, rebuild,
and restart the complete qualification. Do not reuse artifacts from the prior
source digest.

## 7. Production rollout

Before production operations, read and use the `ssh-server` skill and reviewed
feature-166 commands. Do not improvise raw cluster mutation commands.

Sequence:

1. Read-only preflight all nodes and shared dependencies.
2. Verify fresh backup and restore readiness.
3. Distribute identical artifact digests to all nodes.
4. Apply forward-compatible migrations once under serialization.
5. Drain/deploy/verify/undrain node-3.
6. Drain/deploy/verify/undrain node-2.
7. Drain/deploy/verify/undrain node-1.
8. Run cluster-wide traffic, workflows, realtime, queue, file, and failure
   acceptance.

On a critical failure, stop and roll back the application on the failed and all
already advanced nodes in reverse order. Keep the new compatible database
schema; never run an automatic down migration or restore.

## 8. Final evidence

The completion report includes:

- final complete path manifest and source digest;
- all image/migration/evidence digests;
- before/after immediate performance results;
- RUM sample counts labeled observational when insufficient;
- test/build/Docker/load results;
- per-node drain/deploy/health/smoke/rejoin timestamps;
- final release identity and traffic distribution;
- rollback evidence or confirmed rollback readiness;
- known risks and manual QA results.
