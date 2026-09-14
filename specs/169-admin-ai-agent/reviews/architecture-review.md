# Admin AI Agent — Architecture and Security Review

## Status

**Current decision: NO-GO / fail-closed.** The isolated persistence, access, worker protocol, private UI, confirmation, secure-input, audit, recovery, and content-free telemetry foundations are coherent. Production registration intentionally supplies an empty capability registry, so the model has no platform read or action authority. This is the correct state until the sealed capability baseline reaches zero current-business gaps.

Review source state: current worktree on 2026-08-12. The regenerated baseline contains 962 items (400 read/preview/export candidates and 562 blocked mutations), including 93 direct-controller blockers. Its digest is `f99ae1fd5abd0e87a4bec28c6ea29a06d80da2a322ace9163f19d3cee3e73da8`; activation remains `blocked`.

## Findings

### P0 — none in enabled production behavior

The feature defaults disabled and the runtime catalog is empty. No generic SQL, DbContext, web, MCP, code-execution, or automatic action tool is exposed to the model.

### P1 — release blockers

1. **Capability baseline is not closed.** The latest generated baseline remains `blocked`: 562 mutations lack reviewed authoritative adapters, durable idempotency, concurrency, and audit parity. Disposition: release-blocking; do not activate or convert blockers to exclusions. T176 cannot complete in this state.
2. **Production registry is empty.** `Program.cs` registers `AdminAICapabilityRegistry([])`. This safely prevents use but means whole-platform reads/actions are not delivered yet. Disposition: preserve until reviewed compiled definitions and registrations pass the bidirectional matrix.
3. **Direct-controller writes remain.** The baseline identifies 93 pending-essay, finance, shared-package, teacher-finance, and HR write entries that must be extracted to application commands/services before agent adaptation. Disposition: extract and independently parity-test; never call these controllers from the Worker.
4. **Real PostgreSQL concurrency/restart proof is incomplete.** Structural EF and in-memory tests do not prove serializable confirmation, lease loss, callback replay, or external recovery on PostgreSQL. Disposition: T016, T109, T135–T136, T181.
5. **Real-provider and owner acceptance are unavailable in automated local evidence.** No real Gemini secret or owner manual QA result may be fabricated. Disposition: T209 and T210 remain open until performed in an authorized environment.

6. **Frontend/backend endpoint parity was repaired.** Conversation archive/restore now resolve to their authoritative backend routes and the regenerated parity gate reports zero missing routes. Disposition: keep the drift gate mandatory so this does not regress; this does not reduce the separate capability-coverage blockers above.

### P2 — hardening blockers

1. Public and internal API contract coverage is partial for every documented status/error branch.
2. Read adapters currently cover only representative summaries, not every sealed domain family.
3. Ordinary and high-risk action adapter matrices are not registered; action execution therefore remains intentionally unavailable.
4. Full browser matrix definitions exist, but real backend/SignalR execution and WebKit evidence remain required.
5. Retention, append-only terminal completeness, and RecoveryRequired reconciliation need full restart integration evidence.

### P3 — follow-up quality work

1. Register and integrate the now-implemented `AdminAITelemetry` only when orchestration call sites are reviewed; the standalone service rejects unreviewed labels and records no IDs/content.
2. Run bundle/query-plan measurements against representative production-scale data.
3. Re-run documentation verification after endpoint and configuration closure.

## Verified invariants

- Admin role, active/deleted state, and security version are revalidated from PostgreSQL-facing services.
- AdminAI domain, queue, contracts, UI state, and worker logic are separate from human chat and live-support AI.
- Passwords, tokens, secrets, encryption keys, session data, and verification values are rejected/redacted and secure values stay outside transcript/model context.
- Model output is a schema-versioned closed union; unknown fields, branches, depth, size, and capability calls fail closed.
- Model `propose_actions` is advisory only; it never executes an action.
- Strong confirmation is proposal-specific and digest-backed; secure inputs are purpose-encrypted, actor-bound, expiring, and one-time.
- Realtime events are content-free and owner-targeted; REST snapshots remain authoritative.
- Feature configuration defaults disabled; rollback preserves PostgreSQL/Redis volumes and append-only evidence.
- Worker completion is persisted in BullMQ job data before callback delivery; callback retry replays it without a second Gemini inference.
- Worker and backend telemetry accept only bounded, reviewed labels; prompt, message, entity, identifier, argument, result, and secret content is not a metric/log dimension.

## Component evidence and disposition

| Component | Current evidence | Disposition |
|---|---|---|
| Persistence | Additive AdminAI migration and explicit restricted relationships; no destructive AdminAI migration operation | Keep additive; real PostgreSQL migration/restart proof remains required |
| Access | Active, non-deleted PostgreSQL Admin role plus security-version checks | Recheck at every claim/read/proposal/execution boundary |
| Catalog | Three representative read adapters exist, but `Program.cs` registers an empty registry | Correct fail-closed registration; populate only from reviewed sealed definitions |
| Worker | Manual Gemini function loop; read callbacks only; advisory action suggestions; durable callback replay | Keep isolated queue and no database/action authority |
| Actions | Proposal/confirmation/executor foundations exist; no complete registered action matrix | No-go until every mutation has authoritative parity/idempotency/recovery evidence |
| Realtime/UI | Private Admin workspace and content-free event contract; REST is authoritative | Complete browser/SignalR ownership and reconnect evidence |
| Telemetry | Worker telemetry integrated; backend `AdminAITelemetry` implemented standalone with label allowlists | Registration/call-site integration remains explicit follow-up; never add high-cardinality IDs |
| Baseline gates | Drift checks are wired; focused capability/security tests pass | Preserve blocked status and fix all gaps rather than weakening tests |

## Required go-live proof

Go-live requires zero baseline gaps, all generated capability/parity/no-effect/secret tests, clean and existing PostgreSQL migrations, concurrency/restart recovery, Chromium and WebKit accessibility matrices, Docker health, real-provider sentinel capture with zero destructive effect, and explicit owner manual acceptance.
