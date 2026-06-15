# Research: Comprehensive Audit Remediation

## Decision 1: Teacher codes are read-only

**Decision**: Remove every teacher mutation path and enforce `codes.manage` in the command handler. Keep existing owner-scoped list/detail behavior.

**Rationale**: UI-only removal is bypassable. Handler-level permission protects controllers, tests, and indirect MediatR callers. Explicit permission handles multi-role users correctly.

**Alternatives considered**: Allow teachers to create non-balance codes; rejected by the user's explicit read-only rule. Reject only balance codes; rejected because other code types still create financial entitlements.

## Decision 2: Sensitive repository material is incident-contained, not automatically history-rewritten

**Decision**: Remove active tracked dumps/credentials, add preventive scanning/ignore rules, and publish an operator runbook for revocation and history cleanup.

**Rationale**: History rewriting and production credential rotation are destructive external operations requiring backups, ownership confirmation, and coordination with all clones and deployments.

**Alternatives considered**: Run history rewrite automatically; rejected as unsafe and outside an ordinary code change.

## Decision 3: Sensitive actions use explicit permission plus domain boundaries

**Decision**: Use existing permission claims/attributes for endpoint policy and repeat authorization in handlers/services where indirect invocation is possible. Manual unlock is administrative until a durable assignment model exists.

**Rationale**: Broad role lists created the current cross-tenant vulnerabilities. An assignment check cannot be invented when no authoritative assignment relationship exists.

**Alternatives considered**: Continue role-name authorization; rejected. Infer assistant assignment from unrelated task or teacher records; rejected as ambiguous.

## Decision 4: Financial updates use atomic database increments

**Decision**: Update balances through one conditional database statement inside a transaction, read the resulting row while the update lock is held, then append ledger/audit/outbox state before commit.

**Rationale**: Read-modify-write loses updates. Serializable snapshots can abort ordinary concurrent updates; atomic update plus row lock at read committed provides deterministic ordering.

**Alternatives considered**: Application locks; rejected across replicas. Optimistic retry token; valid but adds entity/version changes without improving the ledger transaction.

## Decision 5: Session authorization is single-use and atomic

**Decision**: Add password-reset versioning, conditionally consume refresh tokens, add server logout, and single-flight frontend refresh.

**Rationale**: All four defects are replay/race variants and need one authoritative server state transition with synchronized client state.

**Alternatives considered**: Shorter expiry alone; rejected because replay remains possible during the validity window.

## Decision 6: Redis Streams bridge accepted jobs to BullMQ

**Decision**: Replace LPUSH/BRPOP with Redis Streams consumer groups. A stream entry is acknowledged only after a stable BullMQ job exists; stale pending entries are reclaimed.

**Rationale**: Streams provide acknowledgement and pending-entry recovery using already deployed Redis and avoid implementing BullMQ internals in .NET.

**Alternatives considered**: Direct BullMQ protocol from .NET; rejected for compatibility risk. HTTP worker enqueue without an outbox; rejected because backend acceptance would not be durable. BRPOPLPUSH processing lists; rejected because recovery and ownership metadata are weaker.

## Decision 7: Retries require idempotent terminal updates

**Decision**: Configure bounded exponential retries and stable job IDs, retain failed jobs, and make callbacks tolerate duplicate progress/completion events.

**Rationale**: Retries without idempotency duplicate state; idempotency without retries leaves transient failures manual.

**Alternatives considered**: Infinite retries; rejected due cost and poison jobs. Manual retry only; rejected due operational load.

## Decision 8: Notification delivery must be truthful

**Decision**: Use a configured provider HTTP request and record/return its provider identifier. Missing configuration or provider rejection fails the job.

**Rationale**: A simulated success corrupts delivery status. Explicit failure is safer than false completion.

**Alternatives considered**: Keep the stub behind a development flag; rejected because production can still accidentally report delivery.

## Decision 9: Protected assets are physically separated from public assets

**Decision**: Split mounts and URL roots. nginx receives only public assets; protected assets remain backend-only and use authorized internal delivery.

**Rationale**: Deny lists on a shared root are fragile and new protected directories could be exposed accidentally.

**Alternatives considered**: Add nginx deny locations; rejected as an incomplete default-deny boundary.

## Decision 10: Release workflow is SHA-locked

**Decision**: Deploy only after required CI succeeds for the same SHA, build/tag images before migration, run that tag's migrator, verify all services, and retain the prior successful tag.

**Rationale**: Independent push workflows can race or deploy failing revisions; mutable local image names allow stale migrations.

**Alternatives considered**: Branch protection alone; useful but not sufficient for a self-hosted deployment script that fetches mutable `main`.

## Decision 11: Existing Massar design system overrides generic recommendations

**Decision**: Preserve current navy/teal/gold product register, Tajawal typography, moderate radii, and restrained state motion. Use an agenda fallback for mobile calendars and a shared accessible dialog.

**Rationale**: `PRODUCT.md` and `DESIGN.md` are project-specific sources of truth. The generated Cyberpunk result conflicts with brand, accessibility, and product trust.

**Alternatives considered**: Adopt generated palette/style; rejected.

## Decision 12: Real infrastructure is used where semantics matter

**Decision**: Keep fast unit tests, but use PostgreSQL/Redis integration tests for atomic updates, unique constraints, stream acknowledgement, and recovery.

**Rationale**: In-memory providers and mocks cannot validate database locking, unique conflicts, Redis pending entries, or BullMQ retry configuration.

**Alternatives considered**: Mock-only tests; rejected because they reproduce the current false confidence.
