# Mobile player and assessment review — 2026-09-09

Owner-selected build scope: `all`. Baseline: `3f70949811bcbfadaed66b5d6231cec72d0e9c73`.

The owner reconfirmed `all` after the mobile discussion follow-up. Compare the release against this production baseline, not the older feature-branch merge-base; the actual release delta affects frontend, backend and worker, with no schema change.

## Changes

- Compact mobile playback controls; chapter information and zoomable mind maps below the video. Fullscreen uses the existing player root and a bounded native request, with a CSS/top-layer fallback that preserves the iframe and playback session.
- Lesson comments use a single mobile-safe feed. Replies load automatically near the viewport, show the parent author's name and retain moderation visibility. Long unbroken text wraps inside the bubble. Legacy fullscreen clears ancestor stacking contexts so lesson headings cannot cover controls after rotation.
- Homework and exam attempt review share one interface for answers, manual scores, feedback and confirmed attempt deletion. Teacher ownership and grading permissions are checked server-side. Deletion affects the selected attempt, records an audit entry, and allows resubmission; no production attempts are deleted by the release.
- Homework exports include entitled students who have not submitted, including started attempts. The paginated download is an Arabic UTF-8 CSV with spreadsheet-formula escaping and phone leading-zero preservation.
- Manual essay grading no longer waits for AI. Serializable retries prevent a late AI callback from overwriting teacher grades. Exam scaling uses assigned questions and excludes essay double-counting; unanswered homework questions remain in the total.
- Essay workers process three jobs concurrently per worker, use a 60-second inference deadline and retain successful inference across callback retries. This is not a measured production latency guarantee.

## Verification

- Application suite: 1,251 passed; two existing Redis integration-dependent tests skipped.
- Assessment tests against an isolated local PostgreSQL database: 12 passed, including concurrent manual/AI grading, assigned-question scaling, ownership, missing-student export and targeted deletion.
- Video protection/fullscreen tests: 104 passed. Worker suite: 187 passed.
- WebKit browser tests: eight passed across player widths 320/375/390/768 and discussion widths 320/390/768/1280. Player cases cover missing and non-resolving fullscreen APIs, absent popover support, portrait-to-landscape rotation, map zoom, iframe preservation, seek and exit. Discussion cases cover automatic replies, first replies to empty threads, failed-send draft retention, private pending replies, filtering and overflow.
- Browser assessment workflow passed: answer review, failed-save recovery, persisted manual grade, delete confirmation/cancellation and multi-page CSV download.
- Endpoint and Admin AI inventory tests: 15 passed. Generated inventories refreshed; AI activation remains blocked and no new execution adapters were granted.
- EF pending-model guard passed; no schema change or migration is required.

Physical iPhone/tablet hardware and live AI provider latency were not measured. Browser tests use the real frontend with mocked HTTP/media boundaries; grading tests use real PostgreSQL, not an in-memory substitute.

## Release safety

Build all four immutable images on node-3; require fresh encrypted backup, isolated restore and migration compatibility evidence before serialized migration and node-3 → node-2 → node-1 rollout. Record actual release outcomes in production evidence, not as a pre-build success claim here.

## Latest release attempt

On 2026-09-09, production status and scheduled-backup checks passed on all three nodes. The release-scoped `ops-check` and explicit EF guard passed. Node-3 was still the PostgreSQL primary, which the remote builder refuses. The operator's attempt to list Patroni members with `sudo /usr/bin/patronictl` was denied by the server's sudo policy. No leadership change or application deployment was performed. A server administrator must provide the approved Patroni operation or carry out the planned switchover before the build can proceed; do not bypass the builder's leader protection or broaden sudo permissions as part of this release.

After the owner requested inspection of the actual permissions, `sudo -n -l` confirmed that service management is permitted. The existing PostgreSQL-only failover drill was reviewed after a dry-run, with a healthy preflight, and executed without running the Redis drill or changing sudo policy. Leadership moved from node-3 to node-1; the drill reported 12 seconds to observe the new writer, preserved its acknowledged probe and returned node-3 to `running:replica`. This resolves the builder-placement blocker through the existing authorized service-management path; the earlier direct-Patroni denial does not mean that every supported leadership operation is unavailable. Application deployment still requires the release gates below to complete.
