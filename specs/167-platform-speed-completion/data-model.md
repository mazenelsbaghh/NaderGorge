# Data Model: Platform Speed Completion

This feature primarily changes execution and measurement behavior. It extends
existing records only where durable coordination or segmented evidence needs
authoritative state. All schema changes are additive and compatible with both
the current and candidate application during rolling deployment.

## 1. Performance Observation

Extends the existing `WebVitalsMetric` concept.

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Existing primary key |
| MetricName | bounded string | Allowed Web Vital/navigation metric names |
| Value | number | Finite, non-negative, unit determined by metric |
| Rating | enum/string | good, needs-improvement, poor |
| RouteTemplate | bounded string | Normalized route, no identifiers/query values |
| Surface | bounded enum | public, student, parent, teacher, assistant, employee, admin, support |
| DeviceClass | bounded enum | mobile, tablet, desktop, unknown |
| ConnectionClass | bounded enum | slow, moderate, fast, unknown |
| NavigationType | bounded enum | navigate, reload, back-forward, prerender, client |
| ReleaseId | bounded string | Immutable application release identity |
| NodeId | bounded string/null | Safe serving-node identity if available |
| CorrelationId | bounded string/null | Opaque safe trace key, never an access token |
| SampledAt | UTC timestamp | Browser observation time |
| CreatedAt | UTC timestamp | Server ingest time |

### Validation and privacy

- Raw URLs, query strings, request bodies, phone numbers, names, tokens, private
  messages, and content titles are forbidden.
- Route templates come from a server/client allowlist and collapse dynamic IDs.
- Unknown dimensions use `unknown`; they do not reject otherwise valid metrics.
- Ingest rate and payload size remain bounded.

## 2. Security Session State

Existing authoritative fields on `User`; represented as a cache value, not a
new source of truth.

| Field | Type | Rules |
|---|---|---|
| UserId | UUID | Cache key identity |
| IsActive | boolean | From authoritative user row |
| PasswordResetVersion | integer | Must equal JWT claim |
| SecurityStampVersion | integer | Must equal JWT claim |
| CachedAt | UTC timestamp | Diagnostic only |
| ExpiresAt | UTC timestamp | Short bounded TTL |

### State transitions

```text
Missing ──DB read──> Valid
Valid ──TTL──> Expired ──DB read──> Valid
Valid ──disable/password/permission/security change──> Invalidated
Invalidated ──next request DB read──> Valid or Rejected
Cache unavailable ──> authoritative DB fallback
```

- Cache entries never contain JWTs, password hashes, roles, or personal data.
- A state-changing transaction increments the appropriate version where
  required; cache invalidation completes before the command reports success.

## 3. Cached Query Record

Browser-memory state managed by the single query client.

| Field | Type | Rules |
|---|---|---|
| QueryKey | tuple | Includes surface, resource, normalized parameters and user boundary where needed |
| Data | typed DTO | Returned only through an existing service function |
| Status | enum | pending, success, error |
| UpdatedAt | monotonic timestamp | Freshness basis |
| StaleTime | duration | Chosen per volatility/security class |
| RequestIdentity | opaque | Prevents obsolete response replacement |
| AbortSignal | browser signal | Superseded requests cancel transport work |
| InvalidationScope | key prefix/exact key | Mapped from completed mutation or realtime event |

### State transitions

```text
idle → fetching → fresh → stale → refreshing → fresh
          │                    │
          └→ cancelled         └→ error-with-retained-data
fresh/stale ──logout or security boundary change──> removed
```

## 4. Paginated Collection

Shared response contract for large lists such as admin student search.

| Field | Type | Rules |
|---|---|---|
| Items | array | At most accepted page size |
| Page/PageSize or Cursor | number/string | Must match endpoint contract |
| TotalCount/HasMore | number/boolean | Server-derived |
| Sort | bounded enum | Deterministic tie-breaker required |
| Search | normalized string | Length bounded and debounced client-side |
| ResponseId | opaque/null | Optional debugging/ordering evidence |

- Default page size is 25–50; server enforces a safe maximum.
- The final page may be empty after concurrent deletion and must recover to a
  valid prior page.
- User/role scope is applied before count and pagination.

## 5. Background Event Claim

Extends the existing `OutboxEvent` with additive delivery coordination.

| Field | Type | Rules |
|---|---|---|
| Id | UUID | Stable event identity |
| Type/PayloadJson/Target* | existing | Existing event envelope |
| ClaimOwner | bounded string/null | Node/process identity |
| ClaimedAt | UTC timestamp/null | Set during claim |
| ClaimExpiresAt | UTC timestamp/null | Recoverable lease deadline |
| AttemptId | UUID/null | Unique delivery attempt |
| RetryCount | integer | Monotonic, bounded before dead letter |
| NextAttemptAt | UTC timestamp/null | Persisted backoff eligibility |
| ProcessedAt | UTC timestamp/null | Successful acknowledgement |
| LastError | bounded string/null | Sanitized; no payload/secret content |
| IsDeadLetter | boolean | Terminal automatic retry state |

### State transitions

```text
Pending → Claimed → Dispatched → Acknowledged
             │            │
             │            └→ Retryable → Pending
             ├→ LeaseExpired → Pending
             └→ RetryLimitReached → DeadLetter
```

- Claim transaction locks only while selecting/updating the batch.
- Acknowledgement succeeds only for the matching `AttemptId`/owner.
- Expired claims are safe to redeliver; consumers/client events use the stable
  event identity for idempotency.

## 6. Performance Budget

Version-controlled test configuration.

| Field | Type | Rules |
|---|---|---|
| JourneyOrRoute | string | Normalized route or named authenticated workflow |
| ResourceClass | enum/null | initial-js, shared-js, deferred-js, css, image, font, requests |
| Metric | enum | bytes, duration, percentile, command-count, error-rate |
| Limit | number | Non-negative blocking threshold |
| MeasurementMode | enum | build, browser, API, database, load, production-rum |
| Blocking | boolean | RUM is informative until sufficient; immediate gates block |
| BaselineId | string | Sealed pre-change evidence identity |

## 7. Release Candidate Manifest

Existing production manifest extended for complete workspace provenance.

| Field | Type | Rules |
|---|---|---|
| ReleaseId | string | `git-…` or `src-…` exact source identity |
| SourceDigest | SHA-256 | Hash of complete releasable snapshot |
| HeadCommit | git OID | Recorded even when workspace is dirty |
| Paths | array | Every tracked/untracked included path with digest/classification |
| ImageDigests | map | Exactly backend, frontend, worker, migrator |
| MigrationSet | array | Ordered IDs and compatibility evidence |
| VerificationEvidence | array/map | Commands, results, timestamps, artifact hashes |
| SealedAt | UTC timestamp | Candidate creation |
| Eligible | boolean | False after any workspace delta or failed gate |
| InvalidationReason | string/null | Sanitized path/gate reason |

### State transitions

```text
Inventory → Sealed → Built → Verified → Distributed → Deploying → Deployed
             │        │        │             │             │
             └────────┴────────┴─────────────┴─workspace delta/failure→Invalid
Invalid → new complete Inventory (never mutate old immutable artifacts)
```

## 8. Deployment Gate Result

| Field | Type | Rules |
|---|---|---|
| ReleaseId | string | Candidate identity |
| NodeId | enum/null | node-3, node-2, node-1, cluster/global |
| Gate | enum | preflight, backup, migrate, drain, deploy, health, smoke, traffic, realtime, queue, file, failover, rollback |
| StartedAt/CompletedAt | UTC timestamp | Monotonic ordering |
| Outcome | enum | pass, fail, blocked |
| EvidenceDigest | SHA-256/path | Immutable local evidence reference |
| FailureCode | bounded string/null | Machine/actionable classification |
| ApplicationAction | enum | none, advance, stop, rollback |
| DatabaseAction | enum | none, migrate-forward, keep-schema |

## Relationships

- Performance observations and gate results reference one `ReleaseId`.
- A release manifest contains many source paths, artifacts, migrations, and gate
  results.
- Browser query records are scoped to one authenticated identity boundary and
  are cleared on logout/security change; they are never persisted as server
  authority.
- Security cache entries project one authoritative `User` and are invalidated
  by user/role/permission commands.
- Each outbox event has at most one live claim lease but may have multiple
  historical delivery attempts in logs/metrics.
