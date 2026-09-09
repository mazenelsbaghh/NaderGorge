# Mobile player and assessment review — 2026-09-09

Owner-selected build scope: `all`. Baseline: `3f70949811bcbfadaed66b5d6231cec72d0e9c73`.

## Changes

- Compact mobile playback controls; chapter information and zoomable mind maps below the video. Fullscreen uses the existing player root and a bounded native request, with a CSS/top-layer fallback that preserves the iframe and playback session.
- Homework and exam attempt review share one interface for answers, manual scores, feedback and confirmed attempt deletion. Teacher ownership and grading permissions are checked server-side. Deletion affects the selected attempt, records an audit entry, and allows resubmission; no production attempts are deleted by the release.
- Homework exports include entitled students who have not submitted, including started attempts. The paginated download is an Arabic UTF-8 CSV with spreadsheet-formula escaping and phone leading-zero preservation.
- Manual essay grading no longer waits for AI. Serializable retries prevent a late AI callback from overwriting teacher grades. Exam scaling uses assigned questions and excludes essay double-counting; unanswered homework questions remain in the total.
- Essay workers process three jobs concurrently per worker, use a 60-second inference deadline and retain successful inference across callback retries. This is not a measured production latency guarantee.

## Verification

- Application suite: 1,251 passed; two existing Redis integration-dependent tests skipped.
- Assessment tests against an isolated local PostgreSQL database: 12 passed, including concurrent manual/AI grading, assigned-question scaling, ownership, missing-student export and targeted deletion.
- Video protection/fullscreen tests: 104 passed. Worker suite: 187 passed.
- WebKit browser tests: mobile widths 320/390 and tablet width 768 passed, including missing and non-resolving fullscreen APIs, map zoom, iframe preservation, seek and exit.
- Browser assessment workflow passed: answer review, failed-save recovery, persisted manual grade, delete confirmation/cancellation and multi-page CSV download.
- Endpoint and Admin AI inventory tests: 15 passed. Generated inventories refreshed; AI activation remains blocked and no new execution adapters were granted.
- EF pending-model guard passed; no schema change or migration is required.

Physical iPhone/tablet hardware and live AI provider latency were not measured. Browser tests use the real frontend with mocked HTTP/media boundaries; grading tests use real PostgreSQL, not an in-memory substitute.

## Release safety

Build all four immutable images on node-3; require fresh encrypted backup, isolated restore and migration compatibility evidence before serialized migration and node-3 → node-2 → node-1 rollout. Record actual release outcomes in production evidence, not as a pre-build success claim here.
