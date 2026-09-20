---
name: massar-startup
description: Synchronize Massar/NaderGorge with server repairs before editing project code, configuration, tests, or documentation. Use at task startup and whenever resuming changes in this project; preserve local work and distinguish shared source from deployed releases.
---

# Massar startup

Before the first project edit in each task, read this skill and run the preflight from the **actual editing checkout**. Repeat before a new edit batch after a pause, checkout switch, or notification of a server repair, and before committing or publishing. Read-only investigation does not require a successful gate. Fixing a broken synchronization gate itself is allowed after reporting its failure; it does not authorize unrelated application edits or deployment.

```sh
python3 deploy/production/scripts/startup_check.py --repo "$PWD"
```

The check fetches the canonical `codex/production` branch, checks local ancestry and unresolved merges, reads the release manifests from all three inventory nodes using strict SSH, compares their application source hashes with GitHub, and checks that GitHub did not advance during the read. Configure SSH using the existing `ssh-server` skill; never weaken host verification. Exit zero means synchronized at the time of inspection, not a permanent lock. Dirty local changes remain local and still require review/testing.

## Act on the result

- `ready`: Tell the user the baseline is current and briefly identify the working directory. Continue there, preserving existing changes.
- `needs-integration`: Summarize the incoming commit identities and affected paths from Git. Integrate **before** new feature edits. If clean and strictly behind the shared commit, fast-forward with `git merge --ff-only <sharedCommit>`. Otherwise use the existing isolated integration workflow below. Never force-reset, clean, auto-stash, or overwrite a dirty checkout.
- `conflict`: Resolve the synchronization conflict before feature work. Preserve both sides, inspect intent and tests, and ask only when the correct behavior cannot be determined. Do not choose all-ours/all-theirs.
- `failed-release`: A matching publication journal records a failed rollout or application rollback. Inspect its release evidence, then use a reviewed forward fix or source revert followed by the normal verified publication/release process. Do not reset shared history or retry application edits against a mismatched baseline.
- `pending-or-divergent-release`: GitHub's application source differs from a live manifest, or nodes disagree. Determine whether a rollout is active, failed, or rolled back using the `ssh-server` skill and the repair report. Wait for an active rollout; reconcile a failed release through the existing verified forward-release/revert process. Do not call shared source deployed, silently reset the shared branch, or start unrelated edits.
- `unavailable`: Missing SSH credentials, unreachable GitHub/node, invalid manifest, or Git failure is not proof of synchronization. Continue read-only diagnosis and explain the exact missing prerequisite. Do not claim readiness from old evidence.

For a dirty or diverged checkout, choose a **new** sibling directory and run:

```sh
python3 deploy/production/scripts/source_sync.py integrate --repo "$PWD" --destination /absolute/new/workspace --dry-run
python3 deploy/production/scripts/source_sync.py integrate --repo "$PWD" --destination /absolute/new/workspace --yes
```

Inspect the result; resolve conflicts there, then rerun startup against that directory. **All subsequent edits and tests must use the integrated directory**, with absolute tool paths/workdir. Tell the user its path. Creating a copy and continuing in the old directory does not satisfy synchronization. Never reuse `massar-local-source` or another old snapshot without comparing it to current original changes. Do not copy the integrated tree over an actively edited original checkout. Report that the original remains preserved when using isolation.

## Apply edits through the guarded path

For ordinary agent-authored project edits, create a standard Git patch outside the checkout (for example in a private temporary file), review it, then apply it through:

```sh
python3 deploy/production/scripts/startup_edit.py --repo "$PWD" --patch /absolute/change.patch --dry-run
python3 deploy/production/scripts/startup_edit.py --repo "$PWD" --patch /absolute/change.patch --yes
```

The apply command takes a cooperative local edit lock and the same shared rollout lock used by publishers, reruns readiness under that lock, and applies only a clean patch. Do not replace a rejected apply with a direct write or weaken the check. Builds/tests may create their normal ignored artifacts. Synchronization maintenance, resolving integration conflicts, and initial installation of this mechanism remain the existing narrow exception; record the reason and preserve original files. The repair model still uses its isolated workspace under its supervisor.

This protects the supported apply path against compliant concurrent publishers; it is not an OS sandbox for arbitrary commands or other programs. Never claim all desktop/CLI tools are intercepted. Do not alter operating-system permissions or interrupt another active task to simulate that guarantee.

## Before publishing

Follow `AGENTS.md`'s shared production source rules and the `ssh-server` release workflow. The original project's private history must not be pushed to the public repository. Export one approved source-only commit on the current shared parent; publish with the existing locked compare-and-swap helper and retain deployment/rollback gates. Skills and Git hooks are agent instructions or convenience controls, not a system-level prevention of every filesystem write.

For the repair worker's isolated model workspace (which intentionally has no Git or server credentials), the supervisor owns preflight through `source_baseline.py`; the model must not acquire Git/SSH credentials to run this operator check. Keep that existing separation.

When an operator build failed before rollout and its failure is not yet recorded, first verify that no matching build/deploy is still active. Record that exact shared commit with `source_sync.py mark-failed --expected <sharedCommit> --dry-run`, then `--yes`. This updates the failure observation only; it does not deploy, revert, or resume anything.

End reports should distinguish: baseline synchronized, local work retained, changes verified, source published, and actually deployed. Do not report any later state without its evidence. The admin report is `/admin/auto-repair`; see `docs/production/startup-sync-audit.md` for known remaining gaps.
