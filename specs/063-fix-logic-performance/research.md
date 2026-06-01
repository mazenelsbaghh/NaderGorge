# Phase 0 Research: Phase 3 Logic and Performance Fixes

## Decision 1: Cache platform settings in the backend process for 10 minutes

- **Decision**: Use the existing ASP.NET Core in-process memory cache to store parsed platform settings needed by watch tracking and exam penalty logic for 10 minutes, and explicitly evict the cached entry after settings updates.
- **Rationale**: The hottest read paths in this feature are `RecordVideoEventCommand` and `TrackWatchProgressCommand`. Both currently query `PlatformSettings` directly. A short-lived in-process cache removes repetitive database lookups while keeping invalidation simple and synchronous when the admin updates settings.
- **Alternatives considered**:
  - Use the existing Redis cache layer. Rejected because this feature only needs local low-latency reuse and explicit single-key invalidation, not cross-service sharing.
  - Leave settings uncached and optimize queries only. Rejected because it does not address repeated hot-path round-trips.

## Decision 2: Parse moderation status values into enums before querying

- **Decision**: Normalize incoming moderation status parameters once, parse them to the corresponding enum type, and reject invalid values instead of comparing string representations inside database filters.
- **Rationale**: Enum parsing keeps filters deterministic, avoids case/formatting mismatches, and produces clear request validation behavior. It also aligns with the requirement that moderation views must not silently return misleading results.
- **Alternatives considered**:
  - Continue using `ToString()` comparisons. Rejected because it is brittle and can produce false negatives or provider-specific translation issues.
  - Ignore invalid filters and return all records. Rejected because it hides request errors and can mislead moderators.

## Decision 3: Remove community moderation N+1 using set-based aggregation

- **Decision**: Replace per-post comment and like counts with one set-based aggregation strategy in the moderation query, using either grouped projections or a single query shape that computes counts per returned post.
- **Rationale**: The current projection issues separate count lookups per post, which scales poorly. Set-based aggregation is the simplest way to preserve the existing response shape while improving list performance and avoiding loading full child collections into memory.
- **Alternatives considered**:
  - Use `Include()` for comments and likes and count in memory. Rejected because it can over-fetch large related collections for moderation lists.
  - Keep per-row correlated subqueries. Rejected because this is the current performance problem.

## Decision 4: Enforce per-question timing with persisted server timestamps

- **Decision**: Persist a per-question start timestamp on the answer record lifecycle and determine timeout by comparing server submission time to the question's allowed duration during grading/submission.
- **Rationale**: The constitution requires server-side truth for assessment timing. Persisted start times let the system survive refreshes and make late-answer decisions auditable.
- **Alternatives considered**:
  - Trust client countdown expiration. Rejected because it is tamperable and violates constitution rules.
  - Reject late answers without saving them. Rejected because the feature benefits from preserving an audit trail for support and teacher review.

## Decision 5: Track help-tool usage on persisted answers, not in transient DTO state

- **Decision**: Record penalty-triggering help usage on the stored answer record and apply deduction when building or finalizing results.
- **Rationale**: Penalty behavior must survive retries, later result rebuilding, and essay grading state changes. Persisting help usage beside the answer record provides a stable source of truth.
- **Alternatives considered**:
  - Infer penalty usage from request flow only. Rejected because result rebuilding would lose the original help usage context.
  - Store help usage only at attempt level. Rejected because penalties are question-specific.

## Decision 6: Count all historical extra watch requests for the same student and video

- **Decision**: Enforce the maximum extra-watch limit based on all prior requests for the same student and lesson video, regardless of request status.
- **Rationale**: The fix plan explicitly says "بأي حالة", so the policy should prevent churn through repeated rejected or pending requests.
- **Alternatives considered**:
  - Count only approved requests. Rejected because it allows spam through repeated pending/rejected submissions.
  - Count only currently open requests. Rejected because it weakens the cap semantics.
