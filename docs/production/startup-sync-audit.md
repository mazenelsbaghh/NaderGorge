# Startup synchronization and release readiness

The mandatory `massar-startup` skill is referenced at the beginning of `AGENTS.md`. It checks the actual editing checkout, preserves local work through isolated integration, and requires ordinary edits to use `startup_edit.py`. Read-only investigation and maintenance of this synchronization/verification mechanism remain possible when readiness is blocked; this does not authorize a new rollout over a mixed cluster.

## Implemented protections

- `startup_check.py` fetches the canonical branch, checks local ancestry/conflicts, verifies application hashes against the three live manifests, and rechecks GitHub before returning readiness.
- `startup_edit.py` takes a cooperative local lock and the existing global rollout lock, checks readiness while holding them, and applies only a clean Git patch. The supported publisher uses the same global lock and expected-parent comparison. Traversal patches and mismatched sources are rejected.
- Public source export excludes top-level `output`, `outputs`, and `artifacts`, while local integration retains approved local files. Export compares the complete publication inventory again, including added/deleted/changed files and modes, and rechecks the shared parent.
- Dependency identity covers package manifests/locks, npm configuration, root and nested .NET SDK/NuGet configuration, MSBuild project/props/targets files, and repair image/font inputs. A mismatch blocks repair until the prepared image is verified again.
- The independent repair synchronization monitor reports readiness even while no incident is claimed or another repair is running. Reports are stored in the existing append-only `AutoRepairEvents` table. State transitions and at most one unchanged observation per minute are retained; no schema migration is required.
- The internal authenticated `POST /api/internal/auto-repair/synchronization` endpoint validates bounded state/commit/node metadata. Claims require a ready observation less than three minutes old. Failed checks do not consume repair attempts.
- The admin report shows the latest state, last received check, last successful synchronization, shared commit and all node release identities. Old or unavailable observations cannot display a fresh ready state.
- A private journal on node-1 records publication, deployment, completion, failure and rollback for the exact shared commit. Rollout failures, cluster build/migration failures, and backup/restore-gate failures attempt to record failed publication. Stale reports cannot replace the journal of a newer publication. A failed publication remains blocked until reconciled, including when application files happen to match.

## Failure recovery

For `pending-or-divergent-release`, inspect the existing release operation and its evidence. Wait for an active rollout. For `failed-release`, investigate the failure and use a reviewed forward fix or source revert with the normal verification, publication and rollout gates. Do not reset shared history, overwrite local changes, or automatically revert while another deployment may be active.

If a failure happened before publication recording was installed, or a journal update itself failed, verify that the matching operation is no longer active. Then use `source_sync.py mark-failed --expected <exact-shared-commit> --dry-run` followed by `--yes`. This records failure only; it neither deploys nor resumes repairs. SSH uses the inventory and pinned operator identity.

## Verification scope

Regression coverage includes concurrent publishers, source export races, private preview exclusion, root/imported dependency changes, patch traversal rejection, blocked edits, durable failure journal ordering, rollback/retry contracts, real PostgreSQL persistence and expired-readiness claim refusal, and browser transitions through pending/failed/ready/stale states. Final executed results belong in the release evidence, not permanent readiness claims in this document.

The full verifier also exposed a pre-existing support query-budget regression in the shared source. The admin conversation mapper fetched user and guest names in separate commands. It now loads both bounded name sets in one query while keeping their identity namespaces separate; historical owner lookup and block enforcement remain unchanged. The performance budget was not increased.

## Limits of enforcement

The guarded edit command is a technical gate for its own write path. A skill cannot intercept arbitrary desktop/CLI commands or another program's filesystem writes. The project instructions require the guarded path; independent enforcement across every tool would require a trusted execution layer outside this repository. No operating-system permissions were changed to disrupt other active work.

Network or journal failures produce unavailable/stale evidence and require diagnosis. Readiness is not a permanent lock: synchronization is rechecked under the edit lock and at publication/deployment. Review source content before public publication; path and credential checks are not a semantic privacy classifier. Cleanup or retry must preserve active rollout ownership and all local user changes.
