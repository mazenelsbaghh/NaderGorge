# Admin AI Agent implementation report

## Decision

**NO-GO for feature activation; safe to ship only as disabled, fail-closed foundations.**

The isolated Admin-only workspace, durable conversation/turn pipeline, bounded redacted reads, proposals, confirmations, audit/recovery, bulk and many authoritative action adapter classes are implemented locally. A Codex CLI provider boundary has been added and exercised with synthetic data through its local Unix socket. Secrets remain outside transcripts and source control. PostgreSQL is authoritative and the worker has no database or execution authority.

The current generated baseline has 1,054 items, of which 628 mutation/external-effect items are blocked; these represent 307 distinct authoritative-operation labels, including 94 direct-controller write extraction blockers. The frontend graph now counts only demonstrably invoked methods from student, content, shared-package, live-support, and video-learning services; uncertain object uses still retain every method. It links 401 frontend calls to an exact backend route and leaves 148 without a proven match. The production registry currently contains reads only. Five ordinary identity/content adapters are wired behind it but cannot be proposed or executed while absent from the catalog. PostgreSQL migration and a representative read query-plan test passed locally, but real-backend browser, production-equivalent provider, full restart recovery, zero-gap inventory, and owner manual acceptance gates remain open. Therefore `ADMIN_AI_ENABLED` must remain false. A source-only candidate is on GitHub's `codex/release/all-20260930-hls-finance` review branch; shared `codex/production` and the production server were not changed.

The action executor now commits its unique claim before an authoritative effect and leaves ambiguous outcomes in `RecoveryRequired`. PostgreSQL `char(64)` padding for shorter state fingerprints was also corrected in the comparison path. The complete AdminAI PostgreSQL integration group passed 15/15 after a temporary unrelated build interruption, and the focused application suite passed 220/220 on the final rerun. These checks do not change the NO-GO decision.

On 2026-09-30, activation validation was tightened to reject every unsupported manifest item, duplicate capability identity, and a missing ready marker. A real PostgreSQL two-tab test exposed a serializable claim conflict; the executor now retries only before the authoritative effect. The concurrency group passed 7/7, and the two-tab case passed three additional independent runs. The focused AdminAI application suite passed 225/225, the generated inventory check passed 18/18 after frontend/backend route linking, and production-tooling tests passed 575 with 8 skipped. The repository-wide performance gate still lacks authentic, comparable baseline and candidate artifacts. These results do not complete the 41 open AdminAI tasks or authorize activation.

Proposal input validation now enforces closed nested schemas before preview and persistence, including typed identifiers, bounds, allowed values, and duplicate-field rejection. Create/get/cancel proposal responses now consistently return redacted preview objects; the raw-preview leak regression passed. The expanded AdminAI application group passed 244/244. The production action catalog and remaining release gates are still incomplete; the NO-GO decision remains.

Two PostgreSQL restart-context recovery tests passed, and the complete AdminAI PostgreSQL integration group passed 23/23. Actual worker/Redis restart delivery and callback acceptance remain unverified, so T181 remains open.

The action bridges now consume exact camelCase JSON from the worker and reject casing drift before dispatch. The AdminAI application group passed 246/246 after ordinary and secure-action wire tests. The missing production action catalog remains a release blocker.

The latest `make verify` run passed the backend, frontend, worker, Compose, and performance contract stages but stopped at the performance budget gate because authentic baseline and candidate evidence files are absent. It cannot be treated as a full verification pass.

Startup no longer auto-approves a read-only AdminAI baseline when the feature flag is enabled. It now requires a manually approved active manifest matching the running action catalog and rejecting unsupported inventory items. The activation guard passed 9/9 tests and the focused application group passed 255/255; the current catalog is still read-only, so the gate correctly prevents activation.

Five ordinary identity/content adapters now have read-only authoritative previews for student notes, subjects, and video types. A removed or duplicate target invalidates a pending confirmation before any execution claim. The focused AdminAI application suite passed 258/258; the removed-target path also passed against a real PostgreSQL database with a fresh verification context. These adapters remain hidden behind the read-only production catalog until the complete action matrix and activation gates are ready.

The five identity/content actions now have closed candidate input definitions. One migrated PostgreSQL integration test covered their complete preview, proposal, confirmation, original command, persisted result, and fresh-context replay path, using a real Admin role and the real access gate. It passed 1/1; the focused AdminAI application group remained 258/258. The subject-update result was corrected from a misleading `subjectId=true` field to an accurate `updated=true` field. This is candidate coverage only: the production registry still exposes reads, and the generated mutation inventory remains blocked.

## Disable and rollback

Keep or restore `ADMIN_AI_ENABLED=false`; this prevents admission and worker readiness from exposing the feature. Use the normal immutable production rollback lane for the deployed release. Database changes are additive and evidence records must not be deleted during rollback.
