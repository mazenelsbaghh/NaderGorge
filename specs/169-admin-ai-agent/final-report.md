# Admin AI Agent implementation report

## Decision

**NO-GO for feature activation; safe to ship only as disabled, fail-closed foundations.**

The isolated Admin-only workspace, durable conversation/turn pipeline, bounded redacted reads, proposals, confirmations, audit/recovery, bulk and many authoritative action adapter classes are implemented locally. A Codex CLI provider boundary has been added and exercised with synthetic data through its local Unix socket. Secrets remain outside transcripts and source control. PostgreSQL is authoritative and the worker has no database or execution authority.

The current generated baseline has 1,054 items, of which 628 mutation/external-effect items are blocked; these represent 307 distinct authoritative-operation labels, including 94 direct-controller write extraction blockers. The frontend graph now counts only demonstrably invoked methods from student, content, shared-package, live-support, and video-learning services; uncertain object uses still retain every method. It links 401 frontend calls to an exact backend route and leaves 148 without a proven match. The production registry currently contains reads only; action adapter classes are not registered for execution. PostgreSQL migration and a representative read query-plan test passed locally, but real-backend browser, production-equivalent provider, full restart recovery, zero-gap inventory, and owner manual acceptance gates remain open. Therefore `ADMIN_AI_ENABLED` must remain false. A source-only candidate is on GitHub's `codex/release/all-20260930-hls-finance` review branch; shared `codex/production` and the production server were not changed.

The action executor now commits its unique claim before an authoritative effect and leaves ambiguous outcomes in `RecoveryRequired`. PostgreSQL `char(64)` padding for shorter state fingerprints was also corrected in the comparison path. The complete AdminAI PostgreSQL integration group passed 15/15 after a temporary unrelated build interruption, and the focused application suite passed 220/220 on the final rerun. These checks do not change the NO-GO decision.

On 2026-09-30, activation validation was tightened to reject every unsupported manifest item, duplicate capability identity, and a missing ready marker. A real PostgreSQL two-tab test exposed a serializable claim conflict; the executor now retries only before the authoritative effect. The concurrency group passed 7/7, and the two-tab case passed three additional independent runs. The focused AdminAI application suite passed 225/225, the generated inventory check passed 18/18 after frontend/backend route linking, and production-tooling tests passed 575 with 8 skipped. The repository-wide performance gate still lacks authentic, comparable baseline and candidate artifacts. These results do not complete the 41 open AdminAI tasks or authorize activation.

Proposal input validation now enforces closed nested schemas before preview and persistence, including typed identifiers, bounds, allowed values, and duplicate-field rejection. The expanded AdminAI application group passed 242/242. The production action catalog and remaining release gates are still incomplete; the NO-GO decision remains.

## Disable and rollback

Keep or restore `ADMIN_AI_ENABLED=false`; this prevents admission and worker readiness from exposing the feature. Use the normal immutable production rollback lane for the deployed release. Database changes are additive and evidence records must not be deleted during rollback.
