# Phase 8 review gates

## Clean-code guard

**Scope:** changed production code in `frontend/src`, `backend/src`,
`worker/src`, and `deploy/production/scripts`; test and documentation sources
were excluded from this guard pass.

**Disposition:** PASS after remediation.

| Severity | Finding | Disposition |
|---|---|---|
| P1 | An invalidated request could still commit its later error into the shared query cache because generation fencing covered successful responses only. | Fixed by applying the same generation fence to rejection state; targeted query contracts pass 5/5. |
| P1 | A worker cron heartbeat could still be in flight while the terminal outcome was written, allowing the late renewal to replace `completed`/`failed` with `running`. | Fixed by stopping and awaiting the active heartbeat before the fenced terminal update; focused cluster-cron tests pass 4/4. |
| P2 | Web Vitals normalization did not classify several known dynamic route parents, allowing identifiers below lesson/section/term/form and similar routes to create high-cardinality templates. | Fixed with matching finite dynamic-parent taxonomies in browser and API normalization. |
| P3 | Newly changed paths included generic local names and one broad JSON catch that obscured intent and recovery scope. | Renamed the reviewed locals and narrowed malformed permission handling to `JsonException`, preserving secure fail-closed behavior. |

Local evidence used only already-present dependencies: frontend query contract,
TypeScript, and ESLint passed; worker build, focused cluster-cron tests, and the
independent full rerun passed 75/75; API and Application builds passed with zero
warnings/errors using `--no-restore`; production diff whitespace validation
passed. The full solution build remains intentionally deferred because two new
utility projects do not have local assets and the no-download constraint
forbids restoring them.

## Test guard

**Scope:** 120 changed or new test sources across frontend, backend, worker,
production tooling, and the reviewed SSH skill. Artifact copies were excluded.

**Disposition:** PASS after remediation.

- Split the query-client regression coverage into five behavior-named
  `node:test` scenarios.
- Converted seven contrast inputs to data-driven cases and kept contrast
  correction as its own observable scenario.
- Preserved real PostgreSQL integration tests as fail-closed remote gates
  instead of replacing them with mocks or local skips.

Independent runs reported frontend query 5/5, accessible colors 8/8, route
budgets 4/4, Web Vitals 1/1, worker 75/75, Application 556 passed with one
pre-existing skip, non-PostgreSQL Integration 14/14, and production/SSH Python
426 passed with 6 environment-dependent skips. A release-bound
`verification.json` has not yet been assembled, so these counts are review
evidence rather than final feature acceptance evidence.

## Documentation guard

**Scope:** 66 changed documentation, specification, report, and skill files.

**Disposition:** PASS after remediation.

- Corrected local-check instructions so dependency restoration, browser
  execution, Docker builds, and image pulls are remote-only under the
  no-download constraint.
- Corrected the SSH database-schema reference and labeled the retained schema
  snapshot as historical rather than current production evidence.
- Distinguished pre-remediation review baselines from current fixed code and
  from browser/PostgreSQL gates that remain pending on the reviewed builder.
- Verified referenced local CLI aliases and paths; no relative Markdown link
  target is missing.

## Aggregate disposition

All actionable P0/P1/P2 architecture and UI findings are fixed in source and
have local regression coverage. Browser-computed accessibility, real
PostgreSQL, exact-image build, load, and cluster evidence remain mandatory
remote release gates; they are not represented as local passes.

The subsequent complete-inventory run also found three fail-closed tooling
edge cases. The release inventory now records clean Gitlinks by pinned commit
while rejecting dirty Gitlinks, permits only the public Google API-key shape
inside the exact Android Firebase client-config path while retaining all other
secret detectors, and hashes large generated artifacts without treating them
as release source. Source/config files above the scan bound still fail closed.
The focused source-manifest and release-image suite passes 20/20.

The first remote-builder attempt reported a streamed-source digest mismatch,
consistent with platform metadata being added by the macOS tar producer. The
strict transport now sets `COPYFILE_DISABLE=1` for that source-only producer.
A local pipe round-trip reproduced the actual transport shape and matched the
canonical digest exactly without creating a persistent archive. The transport
regression contract verifies the exact producer command; the failed immutable
remote workspace is not reused.

The next remote build was correctly invalidated when registration/error UX
files appeared during the build. Those changes are now included:
registration maps backend validation details to Arabic field errors, avoids
duplicate global toasts, lazy-loads deferred registration UI, and enforces
parent birth dates strictly before the current Cairo date. API-error behavior
tests pass 4/4, TypeScript and full ESLint pass, and no artifact from the
invalidated source is eligible for deployment.

The subsequent cached distribution exposed the production helper's legacy
1 MiB/schema-v1 manifest contract. Complete v2 provenance is about 1.8 MiB,
while the release bundle remains about 5 MiB. The immutable installer now
accepts schema v1/v2 and retains a bounded 4 MiB manifest ceiling; oversized
inputs still fail closed. Installer, release-contract, remote-builder, and
distribution regression tests pass 52/52. The reviewed client-sync operation
must install this narrow helper on all nodes before the next release install.
