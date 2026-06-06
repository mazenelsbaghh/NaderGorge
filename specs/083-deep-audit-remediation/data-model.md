# Data Model: Deep Technical Audit Remediation

## AccessCode

- **Purpose**: Represents one redeemable code.
- **Key state**: `IsConsumed`, `ConsumedByUserId`, `ConsumedAt`, expiration fields, code group target.
- **State transition**: `Unused -> Consumed` exactly once through a conditional update.
- **Validation**: Cannot be consumed if expired, group expired, or already consumed.

## StudentBalance

- **Purpose**: Stores spendable student credit.
- **Key state**: `CurrentBalance`, transaction history.
- **State transition**: `Balance N -> N + credit` or `N -> N - debit`.
- **Validation**: Debit requires `CurrentBalance >= amount`; concurrent debits must not overspend.

## StudentAccessGrant

- **Purpose**: Records active access to package/term/month/lesson/video/exam content.
- **Key state**: user id, grant type, target id, active flag, expiry, source access code.
- **Validation**: Purchase grants should not duplicate an already active grant for the same target.

## VideoWatchEvent

- **Purpose**: Tracks student watch count, cumulative seconds, lock state, and last update timestamp.
- **Key state**: `TimeWatchedInSeconds`, `WatchCount`, `IsLocked`, `UpdatedAt`.
- **State transition**: Adds plausible seconds, increments watch count when threshold is crossed, locks when max count is reached.
- **Validation**: Reject or cap impossible client deltas; require positive video duration.

## VideoPlaybackSession

- **Purpose**: Short-lived server-side playback authorization for one user/video.
- **Key state**: session id, encrypted playback material, expiration, consumed flag, IP address.
- **State transition**: Created/reused while active, consumed after iframe material loads or progress begins.
- **Validation**: Expired or already consumed sessions cannot serve embed material.

## WorkerJob

- **Purpose**: Operational status for AI/video/mindmap/background jobs.
- **Key state**: job id, status, progress, failure reason, retry/cancel eligibility.
- **Validation**: Only verified staff users can view or control jobs through the proxy.

## EndpointInventory

- **Purpose**: Review artifact generated from controllers.
- **Key state**: method, path, action, auth classification, source file/line.
- **Validation**: Generated JSON must match current controllers and custom internal/E2E auth must not appear anonymous.
