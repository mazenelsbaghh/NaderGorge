# Implementation Plan: Three-Node Production Cluster

**Branch**: `160-employee-realtime-refresh` | **Date**: 2026-07-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/166-three-node-production-cluster/spec.md`

## Summary

تحويل السيرفرات الثلاثة النظيفة إلى Production Cluster متساوي التطبيق: كل عقدة
تشغّل الواجهات الخمس والـAPI والـworker ونقطة توزيع HAProxy، وكل نقطة توزيع
توجّه إلى العقد الثلاث الجاهزة عبر WireGuard. يُضاف replica من Cloudflare Tunnel
على كل عقدة بعد ربط النطاق، وبذلك يظل الدخول متاحًا دون Cloudflare Load Balancing
مدفوع، بينما يظل توزيع الحمل الحقيقي داخل السيرفرات.

الحالة المشتركة تتكون من PostgreSQL 16 واحد منطقيًا عبر Patroni + etcd بثلاثة
أعضاء، كاتب واحد ونسختين متزامنتين، وRedis 7 بثلاثة أعضاء مع Sentinel quorum.
الملفات تستخدم GlusterFS `replica 3 arbiter 1`: نسخة بيانات كاملة مفضلة على
العقدة الأولى، نسخة حية كاملة على الثانية، وmetadata arbiter على الثالثة لمنع
split-brain. تُركب نفس الـvolume على كل تطبيق؛ لا توجد volumes ملفات مستقلة.

النشر يبني images مرة واحدة بعلامة digest، يوزعها على العقد، يشغّل migration
runner واحدًا بقفل advisory، ثم ينشر rolling عقدة بعقدة. القبول يشمل migration
من الصفر، تدقيق schema، load/failover، SignalR/BullMQ، ملفات، backup/restore،
وحجب المنافذ، ولا يبدأ DNS cutover قبل نجاحها.

## Technical Context

**Language/Version**: C# 13 على .NET 9؛ TypeScript 5.9 على Node.js 20؛ Next.js 16.2.7 وReact 19.2.4؛ Bash وPython 3 لأدوات التشغيل  
**Primary Dependencies**: ASP.NET Core، EF Core 9/Npgsql، SignalR Redis backplane، BullMQ 5.71/ioredis، Docker Compose، HAProxy، WireGuard، Patroni 4، etcd 3، PostgreSQL 16، Redis 7/Sentinel، GlusterFS 11.2، Garage 2.3، pgBackRest، Restic، cloudflared  
**Storage**: PostgreSQL 16 واحد منطقيًا؛ Redis HA؛ GlusterFS data-primary/data-standby/arbiter؛ Garage S3-compatible bucket ذاتي الاستضافة بتكرار 3 ومشفّر من جهة pgBackRest/Restic  
**Testing**: `make verify`، `dotnet test`، worker Node tests، frontend lint/typecheck/build/Playwright، pytest/shell contract tests، Docker health gates، k6/vegeta load، chaos/failover scripts، pgBackRest وGluster restore drills  
**Target Platform**: ثلاث عقد Ubuntu 26.04 LTS amd64، كل منها 8 vCPU و31 GiB RAM وقرص 387 GiB، بواجهة عامة فقط وWireGuard overlay خاص  
**Project Type**: منصة ويب متعددة الأسطح مع API وworker وبنية تشغيل cluster  
**Performance Goals**: 300-request distribution sample على العقد الثلاث؛ أقل من 1% أخطاء؛ اختبار 30 دقيقة عند ضعفي baseline لعقدة واحدة؛ لا backlog متزايد؛ failover خلال 60 ثانية  
**Constraints**: صفر فقد للكتابات والملفات التي أُقر نجاحها عند فقد عقدة واحدة؛ كاتب DB واحد؛ refusal عند فقد quorum؛ RPO للـPITR لا يتجاوز 5 دقائق؛ لا Cloudflare LB مدفوع؛ لا secrets في Git؛ domain cutover مؤجل  
**Scale/Scope**: 3 عقد تطبيق كاملة، 8 hostnames، 5 Next surfaces، API، worker، 3 PostgreSQL/etcd members، 3 Redis/Sentinel members، مخزنا ملفات كاملان + arbiter

## Constitution Check

*GATE: Passed before research and re-checked after design. No unresolved
clarification remains. Any failed gate blocks the next implementation phase
unless the owner explicitly accepts a documented risk.*

| Gate | Plan evidence | Result |
|---|---|---|
| Layer impact documented | Backend gets readiness/storage/locking changes; frontend gets canonical origins and trace evidence only; worker gets Sentinel-aware Redis and distributed cron ownership; database keeps product schema but adds migration/schema audit; Docker gets production manifests and host automation | PASS |
| Clean architecture | Storage, cluster readiness, and distributed ownership enter through interfaces in Application/Infrastructure; controllers do not learn Gluster/Patroni details | PASS |
| Security by default | WireGuard-only service ports, pinned SSH keys, non-root operator, root/password disabled after bootstrap, external secret files, Cloudflare Tunnel outbound-only, direct-origin denial | PASS |
| Provider abstraction | Existing external providers remain behind current abstractions; cluster services are injected via configuration/interfaces | PASS |
| Automated critical-path tests | Migration-from-zero, singleton ownership, Sentinel config, storage atomicity, health contracts, routing/failover, backup/restore and regression suite are mandatory | PASS |
| Manual QA | Owner flows cover all 8 hostnames, auth roles, upload/download, realtime, node loss, and negative direct-origin/port checks | PASS |
| Docker gates | `docker compose config -q`, image digest parity, isolated migration, per-service health, rolling drain/restore, and `make verify` | PASS |
| Operational readiness | Metrics/evidence bundle, RTO/RPO checks, immutable release, backup alerts, monthly restore and runbooks | PASS |

### Layer Impact

- **Backend**: add `/api/health/live`, `/api/health/ready`, release/node evidence
  headers, stable connection handling, shared storage roots, and distributed
  singleton leases. Remove all assumptions that process-local disk is shared.
- **Frontend**: build once with the eight canonical production origins; verify
  proxy/cookie/CORS/WebSocket contracts. No product UI redesign.
- **Worker**: connect through Redis Sentinel-compatible configuration, mount the
  same file volume, and guard cron/singleton work with renewable distributed
  locks while BullMQ workers remain active on all nodes.
- **Database**: no new product-domain entity is required. Apply the complete EF
  migration chain to an empty database once; audit history, constraints,
  indexes, seeds, ownership, extensions, and schema drift.
- **Docker/Host**: add parameterized production Compose, WireGuard, HAProxy,
  Patroni/etcd, Redis/Sentinel, GlusterFS, pgBackRest, cloudflared, firewall,
  timers, evidence, rolling release and rollback automation.

## Project Structure

### Documentation (this feature)

```text
specs/166-three-node-production-cluster/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cluster-operations.md
│   ├── health-and-routing.yaml
│   └── production-environment.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.API/
├── src/NaderGorge.Application/
├── src/NaderGorge.Infrastructure/
└── tests/

frontend/
├── src/
├── tests/
└── Dockerfile

worker/
├── src/
├── Dockerfile
└── package.json

deploy/production/
├── compose/
├── config/
│   ├── haproxy/
│   ├── patroni/
│   ├── redis/
│   ├── gluster/
│   ├── pgbackrest/
│   └── cloudflared/
├── inventory/
├── scripts/
├── systemd/
└── tests/

docker/
├── nginx/
└── scripts/

.agents/skills/ssh-server/
├── SKILL.md
├── scripts/
└── docs/
```

**Structure Decision**: المنتج يظل في مشروعات backend/frontend/worker الحالية.
كل معرفة البنية الجديدة تعيش تحت `deploy/production`; مهارة `ssh-server` تصبح
واجهة تشغيل آمنة فوق نفس الأدوات بدل تكرار منطق أو أسرار. لا تُخلط ملفات
Production مع `docker-compose.yml` المحلي حتى يظل التطوير قابلًا للتشغيل.

## Architecture and Role Placement

| Capability | node-1 | node-2 | node-3 |
|---|---|---|---|
| Full app stack + HAProxy + cloudflared | Active | Active | Active |
| etcd + Patroni/PostgreSQL | Member; initial primary candidate | Member; sync replica | Member; sync replica |
| Redis + Sentinel | Initial primary candidate + Sentinel | Replica + Sentinel | Replica + Sentinel |
| GlusterFS | Preferred full-data brick | Live full-data brick | Arbiter metadata brick |
| Backup execution | pgBackRest command owner when DB leader | Eligible after DB promotion | Restore-test target/eligible |

العناوين المنطقية داخل WireGuard ثابتة (`node-1/2/3.cluster.internal`).
HAProxy على كل عقدة:

- routes `massar-academy.net`, `app`, `admin`, `teacher`, `staff` إلى أسطح
  Next الجاهزة على العقد الثلاث؛
- routes `api` و`ws` إلى backend pools مع WebSocket upgrade؛
- routes `assets` إلى static/protected asset path المسموح؛
- exposes local DB writer endpoint selected by Patroni `/primary`;
- health-checks every origin and emits `X-Massar-Node`/`X-Massar-Release`.

Cloudflare Tunnel uses one tunnel with three replicas. A replica landing on any
node enters that node's HAProxy, which still balances across all three app
nodes. Tunnel replicas provide ingress continuity; HAProxy provides actual
origin balancing.

## Phase 0 Research Decisions

The decisions, primary-source rationale, rejected alternatives and execution
blockers for ingress, WireGuard, PostgreSQL, Redis, files, backup, releases,
migrations, Admin bootstrap and SSH are recorded in [research.md](research.md).
There is no unresolved technical clarification.

## Phase 1 Design Outputs

- Operational entities, invariants and state transitions:
  [data-model.md](data-model.md)
- Health, environment and operator contracts: [contracts/](contracts/)
- Ordered bootstrap, acceptance and cutover flow:
  [quickstart.md](quickstart.md)

## Data, Consistency, and Failover

### PostgreSQL

- Patroni uses the three-member etcd quorum and PostgreSQL 16 data checksums.
- `synchronous_mode: quorum`, `synchronous_node_count: 1`, and strict refusal
  when no synchronous standby is available.
- Applications connect only to local HAProxy's writer port; replicas are never
  application writers.
- `pg_rewind`, replication slots, timeline validation, and watchdog/fencing
  checks are acceptance gates.
- EF migrations run once under PostgreSQL advisory lock from an immutable
  migrator image. The clean migration database is compared against the EF
  snapshot and production result before application rollout.
- The migration hardening work removes embedded identity creation from
  `20260613154904_AddIbrahimAdmin` for future clean databases and adds a forward
  cleanup migration for already-applied environments. It likewise removes or
  cleans the default teacher/subject/profile rows introduced by
  `20260607200637_AddMultiTeacherSubjectArchitecture` only after dependency
  analysis proves no approved data references them.

### Redis

- Redis primary with two replicas, AOF `everysec`, `min-replicas-to-write 1`,
  `min-replicas-max-lag 5`, and Sentinel quorum 2 on the three nodes.
- Backend and worker configs resolve the current primary through Sentinel; no
  service writes to a fixed node.
- BullMQ consumers run on all nodes. Queue semantics remain at-least-once;
  critical side effects require durable idempotency keys/DB constraints.

### Files

- `replica 3 arbiter 1` stores bytes on node-1 and node-2 and only metadata on
  node-3. Client quorum is retained; no unsafe replica-2 override.
- The preferred data brick is node-1 during normal operation; node-2 is the
  live copy and becomes the readable/writeable source automatically after
  quorum confirms node-1 loss.
- All backend/worker containers bind the same host Gluster mount. Public,
  protected, live-support, subtitles and mind-map paths are consolidated under
  this mount.
- Writes use temp-file + fsync + atomic rename; API success is returned only
  after the Gluster operation succeeds under quorum. When quorum is absent,
  upload/write fails visibly instead of acknowledging an unprotected file.

## Backup and Restore

- pgBackRest pushes client-side encrypted backups/WAL to the internal
  S3-compatible Garage bucket, with one object replica on every cluster node.
  `archive_timeout=300s` bounds idle-period PITR exposure; WAL is
  uploaded immediately when completed.
- Differential DB backup daily and full backup weekly in measured low-traffic windows; rolling retention
  30 days. Backup success, latest WAL age, and repository encryption are gates.
- Hourly incremental/versioned file backup uses checksums and Restic encryption
  in a separate prefix of the same internal three-replica bucket, retained 30
  days.
- Monthly automation restores both DB and a representative public/private file
  set into an isolated namespace, runs integrity/login/smoke checks, records
  evidence, then destroys only the isolated restore environment.
- Hostinger snapshots remain optional disaster aids. The owner explicitly
  accepts that an internal three-node repository survives one-node loss but
  does not protect against simultaneous destruction or compromise of all three
  production servers.

## Release Strategy

1. Validate inventory, SSH host keys, WireGuard, quorum, free space, time sync,
   backup freshness and secret presence without printing values.
2. Build backend/frontend/worker/migrator images once on the designated build
   node; record SHA-256 digests and distribute the exact OCI archives to all
   nodes.
3. Restore-test the latest backup if this is not first launch; take pre-release
   backup for existing production.
4. Run clean migration/schema audit, then the serialized production migration.
5. Drain node-3, deploy by digest, verify all health/smoke tests, undrain; repeat
   node-2 then node-1.
6. Run distribution, dependency, realtime, job, file, failure and load gates.
7. Roll back application images on any failure. Database down-migrations are
   never automatic; incompatible migrations require forward-fix or restore.

Fresh-database acceptance explicitly asserts that the legacy fixed identities,
password hashes, teacher profile and subject rows are absent. The supplied
production Admin is created only through the protected bootstrap transaction.

## Domain and Cloudflare Contract

| Hostname | Origin pool |
|---|---|
| `massar-academy.net` | landing |
| `app.massar-academy.net` | student |
| `admin.massar-academy.net` | admin |
| `teacher.massar-academy.net` | teacher |
| `staff.massar-academy.net` | staff |
| `api.massar-academy.net` | backend HTTP/API |
| `ws.massar-academy.net` | backend SignalR/WebSocket |
| `assets.massar-academy.net` | shared public/protected asset gateway |

All are Cloudflare proxied/tunnel public hostnames with Full (strict) origin
validation, WAF/rate controls, and no public origin ports. Cookie domain is
`.massar-academy.net`; secure/SameSite behavior and CORS allow only the approved
origins. Cloudflare credentials are an external cutover prerequisite, not
stored in the repository.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `make verify`
- focused backend cluster/readiness/lease/storage tests via `dotnet test`
- `cd frontend && npm run lint && npm run typecheck && npm run build`
- `cd worker && npm test`
- production compose/config/schema contract tests under
  `deploy/production/tests`
- empty-database migration and schema audit
- 300-request routing sample, 30-minute 2× baseline load, and per-node chaos
- PostgreSQL, Redis, Gluster and ingress failover drills
- isolated DB PITR and file restore drill

**Docker Gate Required**:

- local and production `docker compose config -q`
- one immutable digest per image across all nodes
- `make up`, `make migrate` for local compatibility
- liveness/readiness/dependency health for every surface
- `docker compose ps` has no unhealthy/restarting services
- rolling drain/deploy/undrain preserves at least two serving nodes

**Manual QA Required**:

- public landing, student login/navigation, admin login, teacher and staff
  authorized flows through their exact hostnames
- API authenticated write/read across different response-node headers
- two SignalR clients pinned to different nodes receive the same event
- public/private upload through each node and byte-identical read through all
  nodes
- one node lost at a time; then controlled DB/Redis/storage leader loss
- negative access: DB/Redis/Gluster/management ports, direct origin, wrong Host,
  unauthorized protected asset, stale admin bootstrap materials

**End-of-Phase Report Format**: scope delivered; immutable release/digests;
commands and timestamps; automated/Docker/manual results; node/DB/Redis/storage
roles; backup age and restore evidence; security scan; unresolved risks; explicit
GO/NO-GO. No later phase or DNS cutover begins on NO-GO.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Three-member DB/DCS/Redis and two-data-plus-arbiter filesystem | The feature explicitly requires automatic single-node continuity, one writer, live file copy, and split-brain prevention | One DB/Redis/NFS instance is a SPOF; async rsync can lose acknowledged files; replica-2 filesystem cannot safely preserve quorum and HA |
| Cloudflare Tunnel replicas plus per-node HAProxy | Standard DNS round-robin has no health-aware failover and paid Cloudflare LB is excluded | Multiple proxied A records can still route to a dead origin; one HAProxy/tunnel connector is a SPOF |
