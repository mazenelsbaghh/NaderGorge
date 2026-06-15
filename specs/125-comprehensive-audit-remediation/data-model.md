# Data Model: Comprehensive Audit Remediation

## User

New field:

- `PasswordResetVersion: int`, required, default `0`.

Rules:

- A password-reset token carries the current version.
- Successful reset compares and increments the version in the same state transition.
- Any previously issued reset token becomes invalid after the increment.

## RefreshToken

Existing fields remain authoritative: token, user, expiry, revocation, device fingerprint.

Rules:

- Token value remains unique.
- Rotation conditionally changes exactly one active token from unrevoked to revoked.
- A successor is inserted only when the conditional revoke affected one row.
- Logout revokes the token represented by the refresh cookie.

State transitions:

```text
Active -> Rotated (revoked + one successor)
Active -> LoggedOut (revoked, no successor)
Active -> Expired (revoked, no successor)
```

## HomeworkSubmission

New constraint:

- Unique `(HomeworkId, StudentId)`.

Rules:

- Concurrent first submissions produce one row.
- A losing request reloads the existing row and returns the appropriate already-submitted/in-progress outcome.
- Reward publication occurs only after the winning submission reaches the submitted state.

## WarningEvent

New field:

- `OccurrenceKey: string`, required for commitment-engine warnings, bounded length, uniquely indexed.

Format:

```text
commitment:{studentId}:{reason}:{yyyy-MM-dd}
```

Rules:

- Repeated schedulers and worker replicas use the same key for one occurrence window.
- Conflict-safe insertion creates at most one warning.

## CodeGroup and AccessCode

No schema change required.

Rules:

- Creation requires explicit `codes.manage` permission.
- Teacher reads require `CodeGroup.TeacherId == caller.TeacherProfile.Id`.
- Teachers cannot mutate groups or codes.
- Balance code amount must be positive and within the server maximum.

## Durable Job Message

Redis Stream fields:

- `messageId`: stable producer identifier.
- `jobType`: video analysis, mind maps, essay, notification, or commitment sweep.
- `jobId`: stable BullMQ identifier.
- `payload`: serialized command data.
- `createdAt`: producer timestamp.

State transitions:

```text
StreamPending -> BullMQOwned -> Acknowledged
StreamPending -> Reclaimed -> BullMQOwned -> Acknowledged
BullMQWaiting -> Active -> Completed
BullMQWaiting/Active -> RetryDelayed -> Active
BullMQActive -> FailedRetained
```

## Asset Storage

Logical roots:

- `PublicAsset`: images explicitly intended for anonymous/static delivery.
- `ProtectedAsset`: subtitles, protected media, and generated content requiring authorization.

Rules:

- Public nginx mount contains only `PublicAsset` files.
- Protected paths are not mounted into the public static virtual host.
- Authorized backend responses may delegate file transfer through nginx internal locations.

## Constraints and Migration Order

1. Add `User.PasswordResetVersion` with default `0`.
2. Resolve any duplicate homework submissions before adding the unique index; retain the most advanced/latest valid submission and associated answers.
3. Add `WarningEvent.OccurrenceKey` nullable for historical rows, populate deterministic legacy keys where needed, then enforce uniqueness for non-null values.
4. Apply indexes before enabling concurrent/idempotent production paths.
