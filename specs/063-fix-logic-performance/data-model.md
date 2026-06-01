# Data Model: Phase 3 Logic and Performance Fixes

## Overview

This feature extends existing tracking, moderation, and exam entities. No new top-level domain area is introduced. Most changes tighten behavior around existing records and add a small amount of persisted metadata needed for deterministic scoring and timing.

## Entities

### Platform Setting

- **Purpose**: Stores configurable operational rules used by watch tracking and exam scoring.
- **Current Shape**: Key/value records.
- **New/Confirmed Keys**:
  - `VideoWatchThresholdPercentage`
  - `MaxExtraWatchRequestsPerVideo`
  - `HintPenaltyPercentage`
- **Validation Rules**:
  - Watch threshold percentage must be a positive percentage suitable for turning a duration into a threshold.
  - Maximum extra watch requests per video must be a positive whole number.
  - Hint penalty percentage must be a non-negative percentage and must not allow deductions below zero.
- **Relationships**: Referenced by watch tracking flows, extra watch request enforcement, and exam result calculation.
- **State Notes**: Cached for 10 minutes in the application layer and invalidated after admin updates.

### Video Watch Event

- **Purpose**: Tracks accumulated watched seconds, derived watch counts, and lock state for a student and lesson video.
- **Relevant Fields**:
  - `UserId`
  - `LessonVideoId`
  - `TimeWatchedInSeconds`
  - `WatchCount`
  - `IsLocked`
- **Behavior Changes**:
  - Duration is mandatory for threshold evaluation.
  - Threshold seconds are derived consistently from the same shared calculation path.
  - First watch creation and later updates must not increment more than once per request.
- **Relationships**: Belongs to one student and one lesson video.

### Extra Watch Request

- **Purpose**: Represents a student's request for additional access to a specific lesson video.
- **Relevant Fields**:
  - `UserId`
  - `LessonVideoId`
  - `Status`
  - `ResolvedAt`
  - `RejectionReason`
- **Behavior Changes**:
  - All historical requests for the same student and lesson video count toward the configured cap.
  - Creation flow must fail fast when the configured maximum has been reached.
- **Relationships**: Belongs to one student and one lesson video.

### Lesson Comment

- **Purpose**: Stores student-authored comments on lessons and their moderation state.
- **Relevant Fields**:
  - `LessonId`
  - `AuthorUserId`
  - `Body`
  - `Status`
  - `ReviewedAt`
  - `ReviewedByUserId`
- **Behavior Changes**:
  - Moderation filters must use parsed status values rather than string comparison.
  - Student self-view must not surface rejected comments as if they were normal visible comments.
- **Relationships**: Belongs to one lesson and one author; may reference one reviewer.

### Community Post Moderation Summary

- **Purpose**: Admin-facing representation of community posts with moderation metadata and engagement counts.
- **Relevant Fields**:
  - Post identifiers and author metadata
  - Moderation status and review timestamps
  - `CommentCount`
  - `LikeCount`
- **Behavior Changes**:
  - Counts are derived through set-based aggregation for the returned result set, not per-row follow-up queries.
- **Relationships**: Derived from `CommunityPost`, `CommunityPostComment`, and `CommunityPostLike`.

### Student Answer

- **Purpose**: Stores the persisted answer outcome for a student's exam question submission.
- **Current Shape**:
  - `StudentExamAttemptId`
  - `ExamQuestionId`
  - `SelectedOptionId`
  - `SubmittedText`
  - `IsCorrect`
  - `PointsAwarded`
- **Planned Additions**:
  - `HintUsed`
  - `QuestionStartedAt`
  - `TimedOut`
- **Validation Rules**:
  - `QuestionStartedAt` is required when per-question timing applies and an answer record is created for that flow.
  - `PointsAwarded` must never become negative after penalty application.
  - `TimedOut` answers always award zero points.
- **Relationships**: Belongs to one exam attempt and one exam question.
- **State Transitions**:
  - `QuestionStartedAt` recorded when the question becomes active for answer tracking.
  - `HintUsed` flips to true if any penalty-triggering help tool is used.
  - `TimedOut` becomes true when server-side elapsed time exceeds the allowed duration.

## Cross-Entity Rules

- One student can have at most one watch event record per lesson video.
- Extra watch cap evaluation uses `PlatformSetting` plus the student's historical `ExtraWatchRequest` count for the target video.
- Per-question score calculation uses `StudentAnswer`, `ExamQuestion`, and `PlatformSetting` together.
- Moderation query outputs are derived views and do not introduce new persisted entities.

## Migration Impact

- A migration is expected for new persisted answer metadata if `StudentAnswer` is extended.
- A migration is not required for new `PlatformSetting` keys if the table remains key/value based, but seed/update logic should ensure default values exist.
