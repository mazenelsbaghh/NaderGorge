# Combined workspace release — 2026-09-12

The owner requested committing and pushing all current changes to Git and deploying them to Production. Build scope: `all`, matching the explicit request and the detected backend, frontend, worker, database and infrastructure changes.

Scope includes the learning center, support blocking and multiple direct Baileys QR accounts, their migrations and verification artifacts. Three legacy assessment-revision URLs now use explicit route templates so the endpoint inventory resolves them without changing their runtime routes.

Verified before release: `ops-check` passed with validation-only local values for required Compose variables; 1330 Application tests passed (2 Redis tests skipped), 192 worker tests passed, frontend lint/typecheck passed, and EF reported no missing migration. The combined integration run passed 31 tests, including seven against isolated PostgreSQL for the learning center. Three secret-provisioning/topology unit tests and 23 direct Python repository contract assertions passed. Python does not have pytest installed, so these direct assertions are not represented as a pytest suite run. Fresh performance measurements and real WhatsApp mobile pairing are not claimed.

Production uses the same immutable worker image for a separate Baileys process on node-3. Its listener binds only the inventory-selected WireGuard address. Other backend replicas use that private address; callbacks use the existing HTTPS API. The process connects to node-3's local PostgreSQL HAProxy endpoint and retains the global PostgreSQL advisory lock. Rolling deployment and recovery include the bridge when the selected release defines it, and current-release inspection checks its worker-image digest and health on node-3.

`sync_baileys_env.py --secret-file <protected-file-outside-repo> --dry-run` previews configuration; `--yes` creates or reuses mode-0600 local keys and merges them into the established server environment without printing values or rotating existing keys. The local secret file must be retained securely. Frontend, gateway and ordinary worker processes receive empty overrides for the new Baileys secrets. Production configuration was synchronized after its preview; no application was restarted by that step.

The configuration helper also previews and installs one persistent/runtime firewall rule on node-3: only traffic arriving through `massar-app0` from `172.29.0.0/24` may reach TCP 3002. This is required for that node's own backend container to reach the host-network bridge. The existing WireGuard peer rule covers the other two nodes. No public listener or public ingress rule is added, and existing firewall contents are preserved.

Rollout evidence is written beneath `artifacts/production/` by the release helpers. No production database Down migration or restore is part of this release.
