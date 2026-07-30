# Tasks: Three-Node Production Cluster

**Input**: Design documents from `/specs/166-three-node-production-cluster/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/`, `quickstart.md`

**Tests**: Mandatory. This feature changes production topology, data durability,
storage, queues, health contracts, deployment and security.

**Organization**: Setup and foundational safety first, then one phase per user
story. Every story ends with its independent acceptance checkpoint.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification
- [x] Phase 2: Arabic Clarification
- [x] Phase 3: Technical Planning
- [x] Phase 4: Detailed Task Breakdown

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel after its phase prerequisites are satisfied
- **[Story]**: User story from `spec.md`
- Every task names the concrete file or directory it changes

## Phase 1: Setup and Safe Inventory

**Purpose**: Create a secret-free, testable operations workspace without
changing any server state.

- [x] T001 Create the production operations directory layout and ownership README in `deploy/production/README.md`
- [x] T002 [P] Define the three approved nodes, role placement, overlay names, and SSH aliases without credentials in `deploy/production/inventory/production.yml`
- [x] T003 [P] Add inventory JSON Schema rejecting secret-like fields, duplicate node IDs, duplicate overlay addresses, and unknown roles in `deploy/production/inventory/schema.json`
- [x] T004 [P] Add placeholder-only application and cluster environment contract in `deploy/production/config/env.production.example`
- [x] T005 [P] Add root-only secret-reference manifest and permission rules without values in `deploy/production/config/secrets.manifest.example.yml`
- [x] T006 [P] Define release, node-health, failover, backup, and restore evidence JSON Schemas in `deploy/production/evidence/schemas/`
- [x] T007 Build the base `cluster` command parser, common flags, exit codes, inventory validation, dry-run behavior, and JSON evidence writer in `deploy/production/scripts/clusterctl.py`
- [x] T008 [P] Add inventory, secret-redaction, evidence-schema, and target-selection unit tests in `deploy/production/tests/test_clusterctl_contracts.py`
- [x] T009 Add a tracked-secret and unsafe-SSH regression scanner for passwords, `sshpass`, disabled host verification, raw approved IP targeting, and default credentials in `deploy/production/tests/test_no_tracked_secrets.py`

**Checkpoint**: Read-only local tests parse only the exact three approved nodes,
and no tracked artifact contains a production secret value.

---

## Phase 2: Foundational Security and Release Safety

**Purpose**: Blocking prerequisites shared by every story.

**⚠️ CRITICAL**: No remote bootstrap, data initialization, or story deployment
starts until this phase is green.

- [x] T010 Add pinned-host-key enrollment, strict SSH options, non-root `massar-ops`, least-privilege sudo, and rescue validation helpers in `deploy/production/scripts/ssh_transport.py`
- [x] T011 [P] Add idempotent host preflight for OS, CPU, RAM, disk, inodes, MTU, clock sync, public ports, Docker state, and existing cluster markers in `deploy/production/scripts/audit_hosts.py`
- [x] T012 [P] Add root/password SSH rotation and key-only transition tasks that never disable bootstrap access before rescue verification in `deploy/production/scripts/bootstrap_access.py`
- [x] T013 [P] Add full-mesh WireGuard configuration rendering with unique keys referenced externally and peer health validation in `deploy/production/config/wireguard/` and `deploy/production/scripts/configure_wireguard.py`
- [x] T014 [P] Add nftables/UFW rules binding data and management ports to WireGuard/loopback and retaining reviewed SSH access in `deploy/production/config/firewall/` and `deploy/production/scripts/configure_firewall.py`
- [x] T015 [P] Pin and verify Docker Engine, Compose, HAProxy, WireGuard, PostgreSQL client, Gluster client, pgBackRest, restic, chrony, and cloudflared packages/checksums in `deploy/production/config/packages.lock.yml`
- [x] T016 Add idempotent host bootstrap orchestration, cluster marker safety, and remote change evidence in `deploy/production/scripts/bootstrap_cluster.py`
- [x] T017 [P] Add production Compose base, external networks, shared labels, node/release identity, logging limits, resource reservations, and health defaults in `deploy/production/compose/compose.base.yml`
- [x] T018 [P] Add immutable release manifest generation, OCI digest verification, SBOM/scan placeholders, archive export/import, and parity checks in `deploy/production/scripts/release_images.py`
- [x] T019 Remove fixed credential/bootstrap insertion from `backend/src/NaderGorge.Infrastructure/Migrations/20260613154904_AddIbrahimAdmin.cs` for future clean databases without changing product schema
- [x] T020 Audit dependencies of the legacy teacher/subject bootstrap in `backend/src/NaderGorge.Infrastructure/Migrations/20260607200637_AddMultiTeacherSubjectArchitecture.cs` and record safe cleanup predicates in `specs/166-three-node-production-cluster/migration-audit.md`
- [x] T021 Add a forward EF migration that deletes only known legacy Admin/teacher/subject bootstrap rows when unreferenced and removes unsafe legacy defaults in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T022 Add migration regression tests asserting a clean PostgreSQL database contains roles but no fixed Admin, teacher, subject, demo catalog, or default users in `backend/tests/NaderGorge.IntegrationTests/ProductionMigrationCleanDatabaseTests.cs`
- [x] T023 Add advisory-lock migration runner behavior, concurrent-run refusal, target verification, and secret-safe logs in `backend/src/NaderGorge.Migrator/` and `backend/Dockerfile.migrator`
- [x] T024 [P] Replace the hard-coded schema checker with migration-history, model, table, column, type, nullability, default, PK/FK/check/unique/index, extension, ownership, orphan, duplicate-key, and forbidden-seed audit in `deploy/production/scripts/audit_database.py`
- [x] T025 [P] Add no-echo first-Admin bootstrap that BCrypt-hashes in memory and performs one parameterized User/UserRole/audit transaction in `deploy/production/scripts/bootstrap_admin.py`
- [x] T026 Add unit/integration tests for advisory migration locking, schema findings, atomic Admin creation, duplicate refusal, redaction, and rollback in `deploy/production/tests/test_database_tools.py` and `backend/tests/NaderGorge.Application.Tests/`

**Checkpoint**: A temporary empty PostgreSQL 16 database reaches the current EF
model with zero critical audit findings and zero unintended user/catalog rows;
the Admin helper creates one valid Admin without leaking its password.

---

## Phase 3: User Story 1 - All Three Nodes Serve Traffic (Priority: P1) 🎯 MVP

**Goal**: Run the complete application on all three nodes and balance any
ingress across every ready node.

**Independent Test**: 300 requests through one HAProxy show successful
`X-Massar-Node` evidence from node-1, node-2 and node-3; stopping one app removes
it within 30 seconds while the other two continue.

### Tests for User Story 1

- [x] T027 [P] [US1] Add OpenAPI/JSON contract tests for liveness, readiness, dependency health, node ID, and release ID in `backend/tests/NaderGorge.Application.Tests/ClusterHealthContractTests.cs`
- [x] T028 [P] [US1] Add HAProxy config tests for all five frontend pools, API, WebSocket, assets, readiness removal, forwarded headers, and default-host denial in `deploy/production/tests/test_app_topology.py` and `deploy/production/tests/test_foundation_rendering.py`
- [x] T029 [P] [US1] Add a 300-request distribution and one-app-loss test using Host headers and response evidence in `deploy/production/tests/test_app_distribution.py`

### Implementation for User Story 1

- [x] T030 [P] [US1] Add backend `live`, `ready`, and authenticated dependency health responses with node/release identity in `backend/src/NaderGorge.API/Controllers/HealthController.cs`
- [x] T031 [P] [US1] Add trusted proxy/network configuration and `X-Massar-Node` plus `X-Massar-Release` response middleware in `backend/src/NaderGorge.API/Program.cs` and `backend/src/NaderGorge.API/Middleware/ClusterIdentityMiddleware.cs`
- [x] T032 [P] [US1] Parameterize frontend canonical origins, API, WebSocket, cookie domain, CORS inputs, surface identity, and immutable release args in `frontend/Dockerfile` and `docker-compose.yml`
- [x] T033 [US1] Add the full app stack for backend, worker, landing, student, admin, teacher, and staff on every node without fixed container names in `deploy/production/compose/compose.app.yml`
- [x] T034 [US1] Add per-node Nginx gateway routes for the eight approved hosts, WebSocket upgrades, protected assets, no fallback leak, and readiness endpoints in `deploy/production/config/nginx/massar-node.conf`
- [x] T035 [US1] Add per-node HAProxy host routing and health-checked round-robin pools spanning all three overlay nodes in `deploy/production/config/haproxy/haproxy.cfg`
- [x] T036 [US1] Add drain/undrain and backend convergence commands with bounded waits in `deploy/production/scripts/manage_traffic.py`
- [x] T037 [US1] Integrate app/gateway deployment and readiness evidence into `deploy/production/scripts/clusterctl.py`
- [x] T038 [US1] Run the US1 contract/config/distribution suite and record the MVP checkpoint format in `specs/166-three-node-production-cluster/reports/us1-app-ingress.md`

**Checkpoint**: All three app nodes run the same digest, each serves traffic,
and one app-node failure does not interrupt the pool.

---

## Phase 4: User Story 2 - One Authoritative HA Database (Priority: P1)

**Goal**: Initialize one empty logical PostgreSQL database with one writer, two
live replicas, automatic quorum election, audited schema and recoverable history.

**Independent Test**: Write through each app node, read the same row everywhere,
fail the current writer, observe exactly one replacement writer within 60
seconds and zero lost acknowledged transactions for one-node failure.

### Tests for User Story 2

- [x] T039 [P] [US2] Add Patroni/etcd rendered-config tests for three unique members, quorum, synchronous safety, checksums, rewind, TLS/auth references, and WireGuard binding in `deploy/production/tests/test_postgres_topology.py`
- [x] T040 [P] [US2] Add database writer, single-node failover, old-primary partition, no-split-brain, acknowledged-write, and concurrent-migrator acceptance tests in `deploy/production/tests/test_postgres_failover.py`
- [x] T041 [P] [US2] Add pgBackRest config tests for the encrypted internal three-node repository, continuous WAL, five-minute archive bound, daily differential, weekly full, 30-day retention, and isolated restore target in `deploy/production/tests/test_backup_and_cloudflare_contract.py`

### Implementation for User Story 2

- [x] T042 [P] [US2] Add three-member authenticated etcd configuration and health/election checks in `deploy/production/config/etcd/` and `deploy/production/compose/compose.data.yml`
- [x] T043 [P] [US2] Add PostgreSQL 16 and Patroni member templates with quorum synchronous mode, strict safety, data checksums, replication slots, rewind, and node-specific rendering in `deploy/production/config/patroni/`
- [x] T044 [US2] Add local HAProxy PostgreSQL writer endpoint selected by Patroni `/primary` and unavailable on loss of safe writer in `deploy/production/config/haproxy/postgres.cfg`
- [x] T045 [US2] Point backend, worker, and migrator to the stable writer endpoint and remove production dependencies on a local Compose `db` service in `deploy/production/compose/compose.app.yml`
- [x] T046 [US2] Add idempotent first-cluster initialization, replica join/rebuild, switchover, failover, and former-primary rejoin commands in `deploy/production/scripts/manage_postgres.py`
- [x] T047 [US2] Integrate clean audit database, advisory-locked production migration, and zero-finding gate into `deploy/production/scripts/migrate_release.py`
- [x] T048 [P] [US2] Add pgBackRest stanza, self-hosted Garage S3-compatible encrypted repository replicated on all three nodes, archive command, daily differential and weekly full timers, WAL-age alert, and 30-day retention in `deploy/production/config/garage/`, `deploy/production/config/pgbackrest/`, and `deploy/production/systemd/`
- [x] T049 [US2] Add isolated PITR restore orchestration and schema/integrity/login smoke evidence in `deploy/production/scripts/restore_database.py`
- [x] T050 [US2] Integrate PostgreSQL role/status/failover/backup/restore commands and evidence into `deploy/production/scripts/clusterctl.py`
- [x] T051 [US2] Run migration-from-zero, schema audit, write sharing, partition/failover, and PITR tests and record results in `specs/166-three-node-production-cluster/reports/us2-database.md`

**Checkpoint**: Every application uses one logical writer; exactly one writer
exists; one-node loss preserves acknowledged commits; quorum loss refuses writes;
fresh migration and isolated PITR restore pass.

---

## Phase 5: User Story 3 - Shared Realtime, Queues, and Singleton Work (Priority: P1)

**Goal**: Share Redis/SignalR/BullMQ across all nodes and guarantee one durable
effect for critical retries and scheduled work.

**Independent Test**: Cross-node SignalR succeeds; Sentinel promotes once during
master loss; retried jobs and three scheduler replicas produce one durable
effect.

### Tests for User Story 3

- [x] T052 [P] [US3] Add Redis/Sentinel rendered-config tests for one master, two replicas, quorum two, AOF, replica write safety, auth references, and WireGuard-only binding in `deploy/production/tests/test_redis_topology.py`
- [x] T053 [P] [US3] Add backend Sentinel discovery, SignalR cross-node, outbox replay, lease fencing, and duplicate-effect integration tests in `backend/tests/NaderGorge.Integration.Tests/ClusterCoordinationTests.cs`
- [x] T054 [P] [US3] Add worker Sentinel, BullMQ retry/duplicate, stream recovery, cron lease, and failover tests in `worker/src/clusterCoordination.test.ts`, `worker/src/scheduling/clusterCron.test.ts`, `worker/src/queues/jobIngestion.test.ts`, and `deploy/production/tests/test_redis_failover.py`

### Implementation for User Story 3

- [x] T055 [P] [US3] Add Redis primary/replica and three-Sentinel templates, ACL/auth references, AOF, replica-lag write limits, and health checks in `deploy/production/config/redis/`
- [x] T056 [US3] Add Redis bootstrap, role discovery, safe Sentinel failover, replica rejoin, and evidence commands in `deploy/production/scripts/manage_redis.py`
- [x] T057 [P] [US3] Add Sentinel-aware StackExchange.Redis configuration for cache, SignalR, pub/sub, and non-authoritative idempotency in `backend/src/NaderGorge.Infrastructure/Cache/RedisConnectionFactory.cs` and `backend/src/NaderGorge.API/Program.cs`
- [x] T058 [P] [US3] Add Sentinel-aware ioredis/BullMQ connection factory and remove direct single `REDIS_URL` assumptions in `worker/src/config/redis.ts`, `worker/src/index.ts`, and `worker/src/queues/`
- [x] T059 [US3] Add PostgreSQL-backed renewable cluster lease with owner token, fencing generation, expiry, and audit outcome in `backend/src/NaderGorge.Domain/Entities/ClusterLease.cs`, `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`, and a new EF migration
- [x] T060 [US3] Add cluster lease interface/implementation and atomic claim/release behavior in `backend/src/NaderGorge.Application/Interfaces/IClusterLeaseService.cs` and `backend/src/NaderGorge.Infrastructure/Services/PostgresClusterLeaseService.cs`
- [x] T061 [US3] Guard recharge expiry, HR escalation, live-support recovery, and AI recovery with DB leases/atomic claims in `backend/src/NaderGorge.API/BackgroundServices/` and `backend/src/NaderGorge.API/Services/HrApprovalEscalationService.cs`
- [x] T062 [US3] Guard worker cron with PostgreSQL lease ownership while retaining BullMQ workers on all nodes in `worker/src/scheduling/clusterCron.ts` and `worker/src/index.ts`
- [x] T063 [US3] Harden outbox/external effects with stable idempotency keys and replay-safe completion in `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs` and affected dispatch services
- [x] T064 [US3] Integrate Redis/Sentinel status/failover and lease evidence into `deploy/production/scripts/clusterctl.py`
- [x] T065 [US3] Run SignalR, Sentinel loss, queue retry/replay, and triple-scheduler tests and record results in `specs/166-three-node-production-cluster/reports/us3-coordination.md`

**Checkpoint**: Realtime crosses nodes, Redis fails over once, loss/retry does not
duplicate approved durable effects, and every singleton schedule has one owner.

---

## Phase 6: User Story 4 - Shared Files with Live Copy (Priority: P1)

**Goal**: Present one logical filesystem to all app/worker instances, store full
bytes on the preferred and live-copy nodes, and use the third as split-brain
arbiter.

**Independent Test**: Upload public/private files through each app node, read
byte-identical content through all nodes, stop the preferred data brick, retain
all acknowledged files, then lose quorum and observe explicit write refusal.

### Tests for User Story 4

- [x] T066 [P] [US4] Add Gluster arbiter topology, quorum, preferred-read, mount, heal, and split-brain config tests in `deploy/production/tests/test_gluster_topology.py`
- [x] T067 [P] [US4] Add backend storage tests for path normalization, classification, temp write, fsync, checksum, atomic rename, protected reads, deletes, and failure cleanup in `backend/tests/NaderGorge.Integration.Tests/SharedFileStorageTests.cs`
- [x] T068 [P] [US4] Add cross-node upload/read, preferred-brick loss, quorum refusal, heal/failback checksum, and hourly restore acceptance tests in `deploy/production/tests/test_file_failover.py`

### Implementation for User Story 4

- [x] T069 [P] [US4] Add GlusterFS trusted-pool and `replica 3 arbiter 1` rendering with data bricks on node-1/node-2, arbiter on node-3, client quorum, and WireGuard transport in `deploy/production/config/gluster/`
- [x] T070 [US4] Add idempotent Gluster bootstrap, same logical mount on all nodes, heal/status, safe maintenance, and split-brain refusal commands in `deploy/production/scripts/manage_files.py`
- [x] T071 [US4] Add shared storage interface and POSIX implementation with normalized roots, temp/fsync/checksum/atomic rename, classification, and safe delete in `backend/src/NaderGorge.Application/Interfaces/ISharedFileStorage.cs` and `backend/src/NaderGorge.Infrastructure/Services/SharedFileStorage.cs`
- [x] T072 [US4] Refactor content image and live-support attachment storage to `ISharedFileStorage` in `backend/src/NaderGorge.API/Services/ContentImageStorage.cs` and `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAttachmentStorage.cs`
- [x] T073 [US4] Refactor controller and command file writers for resources, question audio/images, sales/admin uploads, and student assets to `ISharedFileStorage` in `backend/src/NaderGorge.API/Controllers/` and `backend/src/NaderGorge.Application/Features/Admin/Commands/`
- [x] T074 [P] [US4] Point worker subtitles, mind maps, and generated assets to the shared mount with atomic writes in `worker/src/services/geminiService.ts`, `worker/src/jobs/generateChapterMindmaps.ts`, and `worker/src/config/storage.ts`
- [x] T075 [US4] Bind one shared Gluster mount into backend/worker public, protected, private, subtitle, mind-map, and live-support paths in `deploy/production/compose/compose.app.yml`
- [x] T076 [US4] Harden `assets` routing for public versus authenticated/protected content and disable temporary/private path exposure in `deploy/production/config/nginx/massar-node.conf`
- [x] T077 [P] [US4] Add hourly encrypted incremental restic backup, 30-day retention, checksum manifest, age alert, and isolated sample restore in `deploy/production/config/backup/files/`, `deploy/production/systemd/`, and `deploy/production/scripts/restore_files_sample.sh`
- [x] T078 [US4] Integrate file quorum/heal/failover/backup/restore evidence into `deploy/production/scripts/clusterctl.py`
- [x] T079 [US4] Run storage abstraction, cross-node checksum, brick-loss, quorum-refusal, heal, and restore tests and record results in `specs/166-three-node-production-cluster/reports/us4-files.md`

**Checkpoint**: There is one mounted logical file source; every acknowledged
file has two full copies; one data-node loss continues safely; no-quorum writes
fail and no partial/private file leaks.

---

## Phase 7: User Story 5 - Safe Immutable Deployment and Rollback (Priority: P2)

**Goal**: Build once, migrate once, deploy one drained node at a time, stop on
failure, and roll application images back by digest.

**Independent Test**: A canary release preserves two serving nodes; injected
readiness/migration failure stops rollout; prior image digest returns within 10
minutes without a destructive down-migration.

### Tests for User Story 5

- [x] T080 [P] [US5] Add release-manifest, OCI digest parity, drain ordering, stop-on-failure, and rollback contract tests in `deploy/production/tests/test_release_workflow.py`
- [x] T081 [P] [US5] Add SSH target safety, pinned-host, dry-run, confirmation, redaction, and per-command evidence tests in `.agents/skills/ssh-server/tests/test_skill_contract.py`

### Implementation for User Story 5

- [x] T082 [US5] Implement preflight, build/import parity, backup gate, migration gate, node-3/node-2/node-1 rolling flow, and failure stop in `deploy/production/scripts/deploy_release.py`
- [x] T083 [US5] Implement application-only immutable digest rollback with schema compatibility gate and no automatic down-migration in `deploy/production/scripts/rollback_release.py`
- [x] T084 [P] [US5] Add systemd units/timers for cluster health evidence, backup age, disk/inodes, time drift, certificate/tunnel, Patroni, Sentinel, Gluster heal, and queue backlog in `deploy/production/systemd/`
- [x] T085 [US5] Replace the test-server `ssh-server` skill with secret-free three-node audit/bootstrap/status/deploy/migrate/drain/failover/backup/restore-test/rollback workflows in `.agents/skills/ssh-server/SKILL.md`
- [x] T086 [US5] Replace hard-coded `sshpass` deployment and schema scripts with wrappers around reviewed cluster commands in `.agents/skills/ssh-server/scripts/deploy.sh` and `.agents/skills/ssh-server/scripts/check_db_schema.py`
- [x] T087 [P] [US5] Update operational role, topology, backup, schema, failover, rollback, and incident documentation in `.agents/skills/ssh-server/docs/`
- [x] T088 [US5] Wire build/deploy/drain/rollback/status commands into `deploy/production/scripts/clusterctl.py`
- [x] T089 [US5] Run immutable rolling deploy with injected readiness failure and rollback rehearsal and record results in `specs/166-three-node-production-cluster/reports/us5-release.md`

**Checkpoint**: All nodes run verified identical digests, one-at-a-time rollout
works, failure stops progression, and application rollback is proven.

---

## Phase 8: User Story 6 - Production Acceptance and Cloudflare Cutover (Priority: P2)

**Goal**: Prove the cluster before DNS, then expose the exact eight hostnames
through standard Cloudflare protection with three tunnel replicas and no direct
origin access.

**Independent Test**: The acceptance runner produces `GO` only after all code,
Docker, load, failure, security, backup and restore gates pass; an
Access-protected rehearsal and final hostnames pass HTTP/WebSocket/cookie/upload
tests while direct origin is denied.

### Tests for User Story 6

- [x] T090 [P] [US6] Add cloudflared three-replica and eight-hostname rendered-config tests, proving connectors target local HAProxy and no paid LB/raw-origin records are required in `deploy/production/tests/test_backup_and_cloudflare_contract.py`
- [x] T091 [P] [US6] Add browser/API/WebSocket/cookie/CORS/upload/protected-asset domain rehearsal tests in `frontend/tests/e2e/production-domain.spec.ts` and `deploy/production/tests/test_domain_contract.py`
- [x] T092 [P] [US6] Add outside-in port scan and direct-origin wrong-Host denial tests for all internal/public services in `deploy/production/tests/test_origin_exposure.py`
- [x] T093 [P] [US6] Add 30-minute 2× baseline load scenario collecting p95/p99/errors/resources/replication/queue/backlog evidence in `deploy/production/tests/load/cluster-load.js`
- [x] T094 [P] [US6] Add bounded one-node-at-a-time chaos scenarios for ingress, app, PostgreSQL, Redis, files, worker, and tunnel connector in `deploy/production/tests/chaos/`

### Implementation for User Story 6

- [x] T095 [P] [US6] Add one Cloudflare Tunnel template with replicas on all nodes and exact root/app/admin/teacher/staff/api/ws/assets mappings in `deploy/production/config/cloudflared/`
- [x] T096 [P] [US6] Add Cloudflare Full-strict TLS, HTTPS, WAF/rate-limit, cookie/CORS, WebSocket, cache, protected-assets, and origin-lockdown runbook in `docs/production/cloudflare-cutover.md`
- [x] T097 [US6] Add protected rehearsal-hostname setup, connector status, replica failure, and final cutover evidence commands in `deploy/production/scripts/manage_cloudflare.py`
- [x] T098 [US6] Add the pre-DNS acceptance orchestrator that validates release-bound evidence, blocks `GO` on any critical finding, stale backup, failed restore, failed quorum/load/security test, or digest mismatch, and signs the exact evidence-file digests in `deploy/production/scripts/accept_production.py`
- [x] T099 [US6] Integrate `accept` and `cloudflare-status` commands into `deploy/production/scripts/clusterctl.py`
- [x] T100 [US6] Execute the full pre-DNS cluster suite (excluding the not-yet-installed Tunnel connector), complete the pre-DNS manual-QA subset, and write signed GO/NO-GO evidence in `specs/166-three-node-production-cluster/reports/production-acceptance.md`
- [x] T101 [US6] After signed pre-DNS GO and owner-supplied Cloudflare access, rehearse through an Access-protected hostname, install all three tunnel replicas, and record connector-failure evidence in `specs/166-three-node-production-cluster/reports/cloudflare-rehearsal.md`
- [x] T102 [US6] After GO, apply and verify the eight final hostnames, origin lockdown, HTTP/WebSocket/cookie/upload/protected-asset flows, and record cutover result in `specs/166-three-node-production-cluster/reports/cloudflare-cutover.md`

**Checkpoint**: Production remains `NO-GO` until all evidence is green. After
approved cutover, all eight hostnames work through Cloudflare and the origins
and data ports are not directly reachable.

---

## Phase 9: Cross-Cutting Review, Guards, and Final Verification

**Purpose**: Deep review and all mandatory Speckit-All close-out gates.

- [x] T103 [P] Update production topology, backup/restore, incident, Admin bootstrap, rotation, and domain documentation in `docs/production/`
- [x] T104 [P] Add architecture diagrams and exact role/failure matrix to `specs/166-three-node-production-cluster/architecture.md`
- [x] T105 Reconcile all local direct-file writes, direct Redis URLs, fixed DB hosts, singleton schedulers, unsafe SSH patterns, and fixed secrets through repository-wide checks in `deploy/production/tests/test_repository_cluster_readiness.py`
- [x] T106 Run deep architecture/security/operations critique and record P0-P3 findings and fixes in `specs/166-three-node-production-cluster/reports/phase6-architecture-review.md`
- [x] T107 Run `clean-code-guard` against all changed production code and scripts, fix every blocking finding, and record evidence in `specs/166-three-node-production-cluster/reports/phase7-clean-code.md`
- [x] T108 Run `test-guard` against all changed tests, fix every blocking finding, and record evidence in `specs/166-three-node-production-cluster/reports/phase8-test-guard.md`
- [x] T109 Run all feature tests plus backend restore/build/test, frontend lint/typecheck/build/E2E, worker build/test, Python operations tests, and `make verify` and record exact results in `specs/166-three-node-production-cluster/reports/final-verification.md`
- [x] T110 Run local and production `docker compose config -q`, image digest parity, migration, service health, surface verification, and rolling-node Docker gates and append results to `specs/166-three-node-production-cluster/reports/final-verification.md`
- [x] T111 Complete manual QA for public/student/Admin/teacher/staff roles, cross-node API/SignalR, public/private files, one-node loss, wrong permissions, direct origin, and all eight domains in `specs/166-three-node-production-cluster/reports/manual-qa.md`
- [x] T112 Validate `quickstart.md`, backup schedules, monthly restore automation, operational evidence retention, and owner handoff in `specs/166-three-node-production-cluster/reports/operations-handoff.md`
- [x] T113 Run `validate_run.py`, close all nine achievement phases only when their evidence is complete, and publish the final GO/NO-GO summary in `achievements.md` (completed as `GO WITH OWNER WAIVER` for the recorded VPS CPU-steal metric; the original signed automated evidence remains unchanged)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependency.
- **Foundational (Phase 2)**: depends on Setup; blocks every story and every
  remote state change.
- **US1**: depends on Foundation; proves three-node stateless app/ingress.
- **US2**: depends on Foundation and uses US1 health/evidence endpoints.
- **US3**: depends on Foundation and US2's authoritative database for leases and
  replay; app distribution from US1 is required for cross-node tests.
- **US4**: depends on Foundation and US1 container/mount layout.
- **US5**: depends on US1-US4 because it rolls their complete topology.
- **US6**: depends on US1-US5 and verified internal three-node backup; final cutover also
  depends on owner-provided Cloudflare access.
- **Cross-cutting close-out**: depends on all in-scope stories; blocks final GO.

### User Story Completion Order

```text
Setup -> Foundation -> US1
                      ├-> US2 -> US3
                      └-> US4
US1 + US2 + US3 + US4 -> US5 -> US6 -> Final Review/Guards
```

### Parallel Opportunities

- T002-T006 and T011-T015 can run in parallel within their phases.
- US2 config tests, failover tests and backup tests can run in parallel before
  their implementations.
- US3 backend, worker and Redis config work can run in parallel after US2.
- US4 backend storage abstraction, worker path conversion and backup config can
  run in parallel after the Gluster contract is fixed.
- US6 Cloudflare, browser/domain, exposure, load and chaos tests can be authored
  in parallel; destructive runtime drills remain serialized one node at a time.

## Parallel Examples

### User Story 1

```text
T027 backend health contract tests
T028 HAProxy config contract tests
T029 distribution/failure tests
```

### User Story 3

```text
T057 .NET Sentinel client
T058 Node/BullMQ Sentinel client
T055 Redis/Sentinel templates
```

### User Story 4

```text
T071 shared backend storage abstraction
T074 worker shared paths
T077 hourly internal three-node file backup
```

## Implementation Strategy

### MVP First

1. Finish Setup and Foundational gates.
2. Implement US1 locally and on isolated/pre-domain endpoints.
3. Prove all three app nodes receive traffic and one app failure is tolerated.
4. Do not call this Production-ready yet: data, files, deployment and restore
   stories remain required.

### Incremental Delivery

1. Secure inventory/network/release foundation.
2. Three-node stateless app and ingress.
3. One authoritative PostgreSQL and Redis/coordination.
4. One shared file source with live copy and arbiter.
5. Immutable rolling release and rollback.
6. Full acceptance, protected tunnel rehearsal, then final domain cutover.

### Safety Rules

- Never change two quorum members simultaneously.
- Never run a destructive action against an unresolved variable, glob, home
  directory, filesystem root, or production restore target.
- Never print, commit, or pass a password/token as a command argument.
- Never mark backup complete without checksum and never mark recovery complete
  without isolated restore evidence.
- Never route the final domain on a failed or missing gate.
- Preserve unrelated user changes, including existing
  `frontend/src/components/landing/HeroSection.tsx` and
  `.agents/skills/ssh-server/docs/database_schema.md` edits.
