# Research: Phase 2 Data Integrity Fixes

## Decision 1: Use one shared threshold calculation rule across both watch-tracking flows

- **Decision**: Introduce a shared watch-threshold calculator and require both watch-tracking entry points to use the same rounding rule, the same percentage source, and the same duration validation behavior.
- **Rationale**: The current codebase calculates thresholds differently in `RecordVideoEventCommand` and `TrackWatchProgressCommand`, which makes identical watch behavior produce different counted-view outcomes. A shared rule removes drift and keeps lesson-locking decisions trustworthy.
- **Alternatives considered**:
  - Keep separate calculations and only align the percentage source. Rejected because the rounding mismatch would still produce inconsistent data.
  - Standardize on a hidden fallback duration. Rejected because substituting invented duration values silently corrupts watch data.

## Decision 2: Reject missing-duration watch updates instead of guessing

- **Decision**: Treat zero or missing video duration as a validation failure for watch-counting operations rather than using fallback values such as 30 minutes or 60 seconds.
- **Rationale**: A fabricated duration makes counted watches non-deterministic and creates data that cannot be trusted later for unlocks, extra-watch decisions, or reporting.
- **Alternatives considered**:
  - Retain the existing large fallback duration in one endpoint and small fallback duration in the other. Rejected because it preserves current corruption.
  - Accept progress time but skip count registration when duration is missing. Rejected because it creates partial records whose counted-view semantics vary by code path.

## Decision 3: Auto-create missing student profiles during theme updates

- **Decision**: Theme preference updates will upsert `StudentProfile` records so a student can save preferences even if no profile row exists yet.
- **Rationale**: Theme preference storage is student-scoped, and requiring a separate preconditioned profile-creation flow would make preference persistence unreliable for valid users whose profiles are missing because of earlier workflows.
- **Alternatives considered**:
  - Reject updates until another workflow creates the profile. Rejected because it pushes user-visible failures into a non-essential dependency.
  - Add profile creation only during registration. Rejected because it would not repair already-existing users with missing profile rows.

## Decision 4: Keep essay audio as optional submission evidence, not a new grading workflow

- **Decision**: Extend essay-answer persistence to retain an optional audio reference alongside existing essay text, while leaving the grading lifecycle itself unchanged for this feature.
- **Rationale**: The user problem is loss of submitted data, not the need for new grading states. Treating the audio reference as optional evidence preserves backward compatibility for typed-only essay answers.
- **Alternatives considered**:
  - Make audio mandatory for essay submissions. Rejected because it changes product behavior and breaks existing typed essay flows.
  - Store audio only outside the essay answer record. Rejected because retrieval integrity becomes indirect and harder to guarantee.

## Decision 5: Gate written corrections by exam completion state

- **Decision**: Include written corrections in exam result payloads only when the attempt is no longer in progress or otherwise hidden by result-visibility rules.
- **Rationale**: Written corrections are academic feedback and should be aligned with final result visibility. Returning them too early would leak answer guidance while the student is still actively taking the exam.
- **Alternatives considered**:
  - Always return written corrections if they exist. Rejected because it undermines assessment integrity.
  - Never return written corrections to students. Rejected because the feature scope explicitly requires them after completion.

## Decision 6: Persist rejection reasons on extra watch requests and expose them only for rejected states

- **Decision**: Add an optional rejection reason to extra watch requests, store it at the time of rejection, and return it only from student status lookups when the latest request is rejected.
- **Rationale**: Students need actionable outcomes, but non-rejected requests should not expose empty or irrelevant explanation fields.
- **Alternatives considered**:
  - Store rejection reasons only in admin audit logs. Rejected because it does not solve the student-facing clarity problem.
  - Always return the field even when null. Rejected because it adds noise and weakens the meaning of the status response.
