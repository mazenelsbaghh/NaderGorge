# Massar automatic repair

Implementation is not activated on production. The database seed pauses execution and disables automatic deployment. The Admin-only report is `/admin/auto-repair`.

## Components

- `runner.py`: one persistent supervisor on node-3, with a local process lock and backend lease. A lost lease pauses new work for reconciliation. Up to three diagnosis attempts are scheduled; failures during deployment pause the service.
- `policy.py`: application-only changes, critical-path approval, source/base-bound SHA-256. Deployment tools, package manifests and agent instructions cannot be changed by the agent.
- `release.py`: existing immutable remote build, backup/isolated restore, migrate, rolling deploy and health gates. Production source must match the configured live release. The completed candidate becomes the baseline for the next repair. An external release requires rebinding the baseline.
- Backend: PostgreSQL incidents, log receipts, control and append-only application event writes. Redis is read as a rolling input window, not authoritative incident storage.
- Frontend: status filtering, pagination, service controls, evidence, timeline, attempts, source hash approval and release ID. Each rollout's final three-node identity check is recorded in the timeline.

## Required deployment preparation

1. Build and review the backend migration and frontend; release them through the existing production workflow. Preserve unrelated working-tree changes.
2. Build a reviewed agent image from `Dockerfile`, supplying immutable Node 22 and .NET 9 image references and an explicit Codex version. The Dockerfile prepares dependency caches from the exact source baseline. The runtime refuses missing images and mutable tags. Set immutable, already-installed PostgreSQL 16 and Redis 7 image digests. `verification.py` provisions a private disposable test network and containers; no host ports are exposed. Missing prerequisites fail verification and prevent deployment.
3. Provision an **internal** Docker network named `massar-repair-internal`. Attach only agent containers and a separately managed HTTPS proxy. The proxy also needs an outbound network. Use `squid.conf` to permit only Codex HTTPS destinations and deny private destinations. Verify public API, private cluster and metadata addresses are unreachable from the agent. No Docker socket, deployment key or platform secret is mounted into the agent.
4. Authenticate Codex using a dedicated account/session on the server into `/home/massar-ops/.local/share/massar-auto-repair/codex-home`. Never copy the workstation SSH private key or its Codex session. Authentication is required before a real-agent acceptance test.
5. Generate a dedicated random runner token (at least 32 characters), store it at the configured `token_file` with mode 0600, and configure the same `AutoRepair__RunnerToken` for backend containers only. Keep it out of shared frontend/worker environments, arguments and logs.
6. Install the reviewed scripts at `/opt/massar-auto-repair`; give the supervisor access to Docker, a private state directory and a verified source repository. Populate `config.example.json` as `/etc/massar/auto-repair.json` with the exact deployed source and release identity. The existing strict SSH transport needs a separately provisioned node-side operations identity and host pins, specified by `MASSAR_KNOWN_HOSTS_FILE` and `MASSAR_SSH_IDENTITY_FILE` in `/etc/massar/auto-repair.env`. The workstation private key must not be copied.
7. Install `massar-auto-repair.service`. Run a real end-to-end acceptance case, including rejected critical approval, offline/missing dependency failure, lease loss, and failed rollout with the existing application rollback evidence. Only then enable the service and resume it from the report.

The CLI installer supports `--dry-run` followed by `--yes`, targets only node-3, and verifies the official npm package SHA-512 for Codex 0.147.0 before installing native files in a dedicated user directory. It does not activate the supervisor.

The template intentionally has invalid placeholder identities so it cannot accidentally activate. There is no automatic package installation, credential fallback, schema reset or production SQL repair.

## Current limits and release blockers

- Collection covers retained backend/worker warning/error/critical entries from `system:logs:v1`, plus bounded frontend/gateway container logs on all nodes through `collector.py`. OS/kernel logs are outside this collector. Redis retains 2,000 entries; container polls overlap a two-minute window with 250 lines per service. Prolonged outages or high volume can lose input before persistence.
- Verification uses a fixed offline script plus a separate Codex review. Real browser/business workflow verification and a real-agent run through the isolated integration services remain activation gates.
- The existing deployment tool provides application rollback when rollout fails. A recurrence during the five-minute observation window invokes the existing evidence-bound application rollback, then pauses. Missing compatibility evidence or lease loss prevents an unsafe rollback and requires reconciliation. This wiring has not yet been exercised against production.
- The model cannot perform arbitrary operations outside application source. Infrastructure incidents are surfaced for intervention. Code affecting finance, access, schema and the repair system itself requires hash-bound approval.
- The report is paginated, with the latest 200 events for a selected incident; all stored events remain in PostgreSQL. Logs and reports are redacted, but the new redactor still requires a production-data sentinel review before activation.
- The native Codex 0.147.0 CLI is installed on node-3 with verified npm package integrity. Supervisor installation, the agent image/network, deployment credentials, production migration, real repair execution and rollout acceptance are not completed by the local implementation tests.

## Focused verification

```bash
tests/venv/bin/python -m pytest deploy/auto-repair/tests -q
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter FullyQualifiedName~AutoRepair
npm --prefix frontend run lint
npm --prefix frontend run typecheck
cd frontend && npx playwright test tests/e2e/auto-repair.spec.ts --project=chromium
```

The PostgreSQL scenario requires `AUTO_REPAIR_TEST_DB` pointing to a dedicated local database named `repair_test`, and `AUTO_REPAIR_TEST_REDIS` pointing to dedicated test Redis. It applies real migrations and must never target shared/production services. The browser scenario uses synthetic HTTP responses and proves UI behavior only.

## Verification record (2026-09-14)

- 14 Python policy/source-isolation/container-log tests passed.
- 12 C# transition/redaction/runner-auth tests passed.
- The real PostgreSQL/Redis incident lifecycle and expanded concurrent-claim test passed after local Docker recovered (13 AutoRepair C# tests passed, none skipped). The rerun used disposable PostgreSQL/Redis containers bound only to loopback, with a dedicated `repair_test` database.
- The phone report/approval browser contract passed with synthetic HTTP responses; a screenshot was inspected. This is not a live backend/browser acceptance result.
- Frontend production build, focused ESLint, TypeScript and route-permission checks passed.
- EF pending-model check passed. The new migration has not been applied to production by this task.
- Initial production status passed on node-1, node-2 and node-3.
- Native Codex CLI installation on node-3 succeeded; the dedicated ChatGPT device login completed successfully, confirmed by the server CLI. Do not store its one-time code in this repository.
- No supervisor activation or production application rollout was performed by this task.
- Activation preflight at 13:34 UTC passed on all three nodes. `make ops-check` passed with 1,422 backend tests and 195 worker tests; three integration tests were skipped in that general run, and the dedicated AutoRepair integration rerun above passed separately. Frontend lint reported one existing chat-hook warning and no errors. The node-3 supervisor and internal repair network are still absent; activation remains pending provisioning and acceptance.
