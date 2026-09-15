# Massar automatic repair

The application and supervisor runtime are installed on production. The supervisor can collect logs while repair is paused. The database seed pauses repair and disables automatic deployment; use the Admin report for current control state. The Admin-only report is `/admin/auto-repair`.

## Components

- `runner.py`: one persistent supervisor on node-3, with a local process lock and backend lease. A lost lease pauses new work for reconciliation. Up to three attempts are scheduled for execution failures; failures during deployment pause the service. Inconclusive performance diagnoses use the bounded evidence workflow below.
- `policy.py`: application-only changes, critical-path approval, source/base-bound SHA-256. Deployment tools, package manifests and agent instructions cannot be changed by the agent.
- `release.py`: existing immutable remote build, backup/isolated restore, migrate, rolling deploy and health gates. Production source must match the configured live release. The completed candidate becomes the baseline for the next repair. An external release requires rebinding the baseline.
- Backend: PostgreSQL incidents, log receipts, control and append-only application event writes. Redis is read as a rolling input window, not authoritative incident storage.
- Frontend: status filtering, pagination, service controls, evidence, timeline, attempts, source hash approval and release ID. Each rollout's final three-node identity check is recorded in the timeline.

## Required deployment preparation

1. Build and review the backend migration and frontend; release them through the existing production workflow. Preserve unrelated working-tree changes.
2. Build a reviewed agent image from `Dockerfile`, supplying immutable Node 22 and .NET 9 image references and an explicit Codex version. The Dockerfile prepares dependency caches from the exact source baseline. The runtime refuses missing images and mutable tags. Set immutable, already-installed PostgreSQL 16 and Redis 7 image digests. `verification.py` provisions a private disposable test network and containers; no host ports are exposed. Missing prerequisites fail verification and prevent deployment.
3. Provision an **internal** Docker network named `massar-repair-internal`. Attach only agent containers, the currently active disposable diagnostic PostgreSQL container, and a separately managed HTTPS proxy. The diagnostic agent shares that disposable container’s network namespace; its Redis services remain on the fenced test network. The proxy also needs an outbound network. The installed `proxy.mjs` permits only the configured Codex HTTPS destinations and rejects private destinations. `prepare_runtime.py` installs an nftables host INPUT fence for the agent bridge; disposable verification bridges receive the same fence. Verify public API, private cluster and metadata addresses are unreachable from the agent. No Docker socket, deployment key or platform secret is mounted into the agent.
4. Authenticate Codex using a dedicated account/session on the server into `/home/massar-ops/.local/share/massar-auto-repair/codex-home`. Never copy the workstation SSH private key or its Codex session. Authentication is required before a real-agent acceptance test.
5. Generate a dedicated random runner token (at least 32 characters), store it at the configured `token_file` with mode 0600, and configure the same `AutoRepair__RunnerToken` for backend containers only. Keep it out of shared frontend/worker environments, arguments and logs.
6. Install the reviewed scripts at `/opt/massar-auto-repair`; give the supervisor access to Docker, a private state directory and a verified source repository. Populate `config.example.json` as `/etc/massar/auto-repair.json` with the exact deployed source and release identity. The existing strict SSH transport needs a separately provisioned node-side operations identity and host pins, specified by `MASSAR_KNOWN_HOSTS_FILE` and `MASSAR_SSH_IDENTITY_FILE` in `/etc/massar/auto-repair.env`. The workstation private key must not be copied.
7. Install `massar-auto-repair.service`. Run a real end-to-end acceptance case, including rejected critical approval, offline/missing dependency failure, lease loss, and failed rollout with the existing application rollback evidence. Collection may run with the Admin controls paused. Resume repair and automatic deployment only after the verification gates pass. The isolated smoke command alone does not establish full rollout acceptance.

The CLI installer supports `--dry-run` followed by `--yes`, targets only node-3, and verifies the official npm package SHA-512 for Codex 0.147.0 before installing native files in a dedicated user directory. It does not activate the supervisor.

The template intentionally has invalid placeholder identities so it cannot accidentally activate. There is no automatic package installation, credential fallback, schema reset or production SQL repair.

## Current limits and release blockers

- Collection covers retained backend/worker warning/error/critical entries from `system:logs:v1`, plus bounded frontend/gateway container logs on all nodes through `collector.py`. OS/kernel logs are outside this collector. Redis retains 2,000 entries; container polls overlap a two-minute window with 250 lines per service. Prolonged outages or high volume can lose input before persistence.
- Verification uses a fixed offline script plus a separate Codex review. Backend TRX counters and browser results must contain executed passing tests without skips. The complete backend, frontend, worker and browser gate must pass before repair is resumed. Actual Tajawal WOFF2 assets are cached during image build; verification serves them on loopback through the Next font response loader, with no external network needed.
- The existing deployment tool provides application rollback when rollout fails. A recurrence during the five-minute observation window invokes the existing evidence-bound application rollback, then pauses. Missing compatibility evidence or lease loss prevents an unsafe rollback and requires reconciliation. This wiring has not yet been exercised against production.
- The model cannot perform arbitrary operations outside application source. Infrastructure incidents are surfaced for intervention. Code affecting finance, access, schema and the repair system itself requires hash-bound approval.
- The report is paginated, with the latest 200 events for a selected incident; all stored events remain in PostgreSQL. Logs and reports are redacted. A bounded production sample confirmed IP redaction and preserved exception context; unknown sensitive formats remain a limitation of pattern-based redaction.
- Codex 0.147.0, the supervisor files, pinned agent image, restricted proxy/network, dedicated server-side SSH identity and backend-only machine token are installed. The model container uses Codex externally sandboxed mode: isolation is enforced by Docker mounts, read-only root, capability restrictions, default seccomp, resource limits and network fences. Agent-driven rollout/rollback acceptance remains separate from the completed operator rollout.

## Focused verification

```bash
tests/venv/bin/python -m pytest deploy/auto-repair/tests -q
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter FullyQualifiedName~AutoRepair
npm --prefix frontend run lint
npm --prefix frontend run typecheck
cd frontend && npx playwright test tests/e2e/auto-repair.spec.ts --project=chromium
```

The PostgreSQL scenario requires `AUTO_REPAIR_TEST_DB` pointing to a dedicated local database named `repair_test`, and `AUTO_REPAIR_TEST_REDIS` pointing to dedicated test Redis. It applies real migrations and must never target shared/production services. The browser scenario uses synthetic HTTP responses and proves UI behavior only.

## Provisioning and verification record (2026-09-14)

- Release `git-c66b7e7f1674c109758602a0b39c05a39b98d6d2` passed remote build, encrypted backup/isolated restore, migration and rolling deployment to all three nodes. Final status passed. Application source matches the preceding release; this rollout adds backend-only credential attachment and runtime tooling.
- Machine-token presence was checked in backend containers on all three nodes, and its absence checked in worker and all frontend containers. Dedicated node-3 SSH authentication succeeded against all three nodes.
- The font-cache agent image `sha256:cc2137142347335d938badf79bc715220fb8a7fb2c83c99271380e9b2a393154` passed the smoke check. Its real Codex smoke repaired a disposable function and passed independent execution. Probes verified blocked host SSH, blocked direct egress, rejected unrelated/private proxy destinations and allowed Codex connectivity.
- The production machine API accepted an empty log batch over verified HTTPS using the service's `Massar-AutoRepair/1.0` User-Agent. Cloudflare rejected Python's default User-Agent; no Cloudflare protection was changed.
- 16 local Python policy, patch-sealing and log collection tests passed, including authorization removal and rate-limit policy approval.
- The fresh-process PostgreSQL/Redis lifecycle fixture, remaining 1,433 application tests and all 321 integration tests passed without skips. Integration fixtures were corrected to read persisted timestamps at request boundaries, respect the role-name length limit, and compare persisted shared event cursors with their matching outbox payloads. Application behavior was unchanged by these fixture corrections.
- The live Admin report was reviewed using the owner’s existing Chrome session. At 16:20 UTC the supervisor was enabled for persistent collection, with repair paused and auto-deploy off; the report confirmed node-3 heartbeat and queued real log incidents. Further activation and verification evidence belongs under `artifacts/production/auto-repair/`.

Runtime preparation is explicit: `provision.py --dry-run` then `--yes` from the operator workspace, followed by `prepare_runtime.py --dry-run` then `--yes` on node-3. Neither command changes the Admin repair/deployment controls. `prepare_runtime.py --rebind-only` refuses changed application source or an active repair baseline; a changed dependency baseline requires rebuilding and verifying the image.

Evidence is stored under `artifacts/production/auto-repair/` and the isolated activation worktree's `artifacts/production/`. Do not commit credentials or Codex login material with evidence.

The activation correction groups compact correlation IDs and gateway timestamps. Pending, unattempted legacy duplicates are consolidated in bounded batches under the existing advisory transaction lock; their original rows and events remain available as “تكرار مجمّع”. No application schema change is required. Release scope is `all`, continuing the owner-authorized three-node deployment. Browser fixture updates follow remembered-session hydration, streamed access-denial pages, current report errors, academic eligibility and the parent-code dialog.

Final activation verification also reproduced concurrent cookie rotation between bootstrap and API requests. Both paths now use the existing shared refresh operation; the real browser gate passed all 9 scenarios with zero failed, skipped or flaky tests. Isolated failure acceptance confirmed pre-start lease rejection, cancellation of a running verification container, rejection of a missing offline package and interruption after a failed release command. These failure probes did not deploy or roll back production.

## Shared source synchronization

`codex/production` in `mazenelsbaghh/NaderGorge` is the shared source history. Bootstrap publishes the reviewed release source only; private local history and release artifacts are not imported. The default GitHub branch is not rewritten.

The installed supervisor fetches the shared branch before preparing a case, checks its complete application file hashes against the live manifest, and checks dependency inputs against the verified image's source. It preserves verified repair commits on `codex/repair/<incident-id>`, then advances the shared branch with an ancestry-checked compare-and-swap while holding the existing rollout lock. Deployment checks the exact shared tip again under that same lock. If another release wins, a candidate stops for reintegration rather than overwriting it. The report records the GitHub commit identity in the deployment timeline.

For local work, follow the `Shared production source` instructions in `AGENTS.md`. Integration creates a separate worktree and preserves the original working files/index. A conflicting merge remains in that worktree for review. Export creates a source-only, single-parent commit; run the usual checks there before publication and release. Fetch failure, uncommitted changes, stale parents and unpublished release manifests fail closed.

A dedicated repository-scoped GitHub write key remains on node-3 outside the agent container. `/etc/massar/source-sync.json` points to its identity, verified host pins and shared source repository. `verified_dependencies_source` in the runner configuration retains the source used to verify its installed image. Neither key material nor local operating credentials travel with source exports.

After a failed rollout or an intentional application rollback, the shared source may still describe the rejected candidate. Preserve its history and use a reviewed forward revert/release to reconcile it; do not reset the shared branch. The paused repair control and application/source comparison prevent further repairs against that mismatch.

Before claiming any new incident, the supervisor refreshes shared source and compares it with the live application. While an operator's published release is still pending, it keeps collecting logs and retries without claiming more cases. Once source and live application match again, repair resumes automatically; changed image dependencies still require runtime preparation.

## Automatic evidence and diagnostic prerequisites

`RepairEvidenceCollector` processes `needs_evidence` incidents while repair is enabled. A recognized route template and HTTP method open a `collecting_evidence` window for at most 20 minutes. Up to three fresh matching slow-request measurements from the bounded Redis log window are saved as append-only evidence and requeue diagnosis. Old samples and other routes do not reopen an incident. There are at most two automatic collection rounds per incident; an empty expired window, unknown route or exhausted budget ends collection with a concrete owner action in the report. Absence of logs never means the incident was fixed. Manual additional evidence still uses the existing Admin decision.

Performance evidence contains command operation/duration/success (first 24), connection-open duration, transaction-commit duration when present, in-flight request count, and existing route/node/release fields. It records no request body, SQL text/parameters, student identity or production credentials. Advisory-lock command duration includes execution; connection-open duration does not isolate pool waits. Samples are from fresh same-route requests, not a proven reproduction of the original incident. Unmeasured causes must remain explicit in the diagnosis.

Before diagnosis, the supervisor restores .NET dependencies from the installed offline package cache. Each diagnosis/review gets fresh disposable PostgreSQL and Redis services and their test connection variables; they contain no production data and are removed on exit. The model can apply repository migrations and seed synthetic test cases. It cannot automatically replay real authenticated production requests. The fixed full verification gate remains separate and mandatory before release.
