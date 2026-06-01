# API Contracts: Phase 3 Logic and Performance Fixes

## Purpose

Document the externally visible API and behavior changes required by this feature. These contracts focus on response semantics and request validation, not implementation details.

## 1. Watch Event Tracking

### `POST /api/tracking/video-event`

- **Request Body**:
  - `lessonVideoId`
  - `watchedSeconds`
  - `totalDurationSeconds`
- **Contract Changes**:
  - `totalDurationSeconds` is mandatory and must be greater than zero.
  - Requests with missing or zero duration return `400 Bad Request`.
  - Successful responses continue to return the tracking context payload.
- **Error Semantics**:
  - Invalid duration must include the existing `DURATION_REQUIRED` error token.
  - Reaching the watch limit continues to produce the existing forbidden behavior.

## 2. Session-Based Watch Progress

### `POST /api/student/video-session/{lessonVideoId}/track-progress`

- **Request Body**:
  - `secondsWatched`
  - `totalDurationSeconds`
  - `registerView`
- **Contract Changes**:
  - `totalDurationSeconds` is mandatory and must be greater than zero.
  - Returned threshold and watch-count behavior must be consistent with `POST /api/tracking/video-event`.
  - Repeated requests within the cache window must observe the same settings-derived threshold unless an admin settings update occurs.

## 3. Extra Watch Requests

### `POST /api/student/video-session/{lessonVideoId}/request-extra`

- **Contract Changes**:
  - The request is rejected once the student reaches the configured maximum number of requests for the same video.
  - Failure responses must contain a clear limit-reached message suitable for student display.

### `GET /api/student/video-session/{lessonVideoId}/request-status`

- **Contract Changes**:
  - Existing status response remains intact.
  - Status must reflect the capped-request behavior after additional requests are blocked.

## 4. Lesson Comment Moderation

### `GET /api/admin/lessons/{lessonId}/comments?status={status}`

- **Contract Changes**:
  - `status` is parsed as a supported moderation status value.
  - Invalid status values return `400 Bad Request` rather than an empty or misleading result set.
  - Valid status values return only matching comments.

### Student lesson comment self-view

- **Affected Consumer Contract**:
  - Student-facing lesson comment history must no longer present rejected comments as ordinary visible comments.
- **Allowed Outcomes**:
  - Rejected comments are omitted from the self-view, or
  - They are returned with explicit rejection state information the UI can distinguish.

## 5. Community Moderation Listing

### `GET /api/admin/community/posts?status={status}`

- **Contract Changes**:
  - Response shape remains unchanged.
  - `commentCount` and `likeCount` must still be present for every returned post.
  - The endpoint must obtain those counts through set-based retrieval rather than per-post follow-up lookups.

## 6. Exam Help-Tool Penalty

### `GET /api/exams/{examId}/attempts/{attemptId}/questions/{questionId}/fifty-fifty`

- **Contract Changes**:
  - Successful help-tool usage must persist penalty-triggering usage for the targeted question.
  - Existing removed-options response shape remains unchanged.

### Exam result payloads

- **Affected Responses**:
  - `POST /api/exams/{examId}/submit/{attemptId}`
  - Any later result rebuild path using the same attempt data
- **Contract Changes**:
  - Question scores reflect configured help-tool deductions.
  - Penalized scores never go below zero.

## 7. Per-Question Timing Enforcement

### `POST /api/exams/{examId}/submit/{attemptId}`

- **Contract Changes**:
  - Late answers to timed questions are accepted for audit but returned with zero awarded score.
  - Result payloads must reflect timeout state consistently for those questions.
  - Questions without a time allowance are not marked timed out solely due to missing timing metadata.

## 8. Platform Settings Administration

### Existing admin platform settings update flow

- **Affected Contract**:
  - Any existing admin command or endpoint that updates platform settings.
- **Contract Changes**:
  - Updating relevant settings must invalidate the application cache immediately.
  - New supported keys include `MaxExtraWatchRequestsPerVideo` and `HintPenaltyPercentage`.
