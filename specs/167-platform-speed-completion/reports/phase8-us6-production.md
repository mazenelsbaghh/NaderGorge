# Phase 8 / US6 — Production rollout

Date: 2026-07-29

## Outcome

The complete reviewed workspace was built remotely and progressively deployed
to Production in the required order: node-3, node-2, then node-1. Each node was
drained, checked for quorum, started from immutable images, verified through
backend/worker/shared-storage and seven-surface probes, switched atomically,
undrained, and checked again before the next node advanced. There was no
planned downtime.

The release identity is intentionally evidence-bound rather than hard-coded in
this source document: the final source digest changes when this report changes.
The immutable identity and per-operation timestamps are recorded under
`artifacts/production/build/<release-id>/manifest.json` and
`artifacts/releases/<release-id>/`.

## Database and rollback

- A fresh encrypted full backup and isolated restore passed.
- Target migrations and real-data validation passed.
- N-1 backend readiness against the retained forward schema passed.
- The production migration ran once under serialization.
- An initial rollout exposed a legacy 1 MiB current-pointer helper limit after
  the candidate services had passed health. The deployment tool automatically
  restored the previous application on node-3 in reverse recovery order.
- The database was not downgraded or restored. It remained on the compatible
  new schema, as required.
- The helper and collector now share a bounded 4 MiB manifest limit, accept
  release schema v1/v2 where appropriate, and retain symlink, size, digest,
  and exact-field protections. Boundary and rollout tests pass.

## Verification evidence

- Complete source manifest and secret audit: PASS.
- Remote four-image build and three-node digest parity: PASS.
- Real PostgreSQL migration compatibility and N-1 readiness: PASS.
- Rolling deployment and automatic app-only rollback behavior: PASS.
- Final cluster status, audit, Cloudflare status, backup schedules, and current
  manifest parity: PASS.
- Read-only browser smoke: landing, teacher carousel control/state, student
  login, and four-step registration PASS with no observed application console
  error.
- RUM: observational/pending; no fixed sample or time threshold blocks release.

## Decision

The zero-downtime production operation completed successfully, but the
feature-level release decision remains **NO-GO / incomplete**. The candidate
must not be described as fully accepted while its immutable manifest remains
`eligible=false` with `verification-pending`.

The six authenticated k6 journeys and the complete Chromium/WebKit
accessibility and four-role manual matrix were not executed. No disposable
load-test account or workflow/WebSocket token files were available, and no
result was fabricated or reused from another release. Closing these gates
requires a new exact-source candidate, release-bound verification evidence, and
another reviewed rollout.
