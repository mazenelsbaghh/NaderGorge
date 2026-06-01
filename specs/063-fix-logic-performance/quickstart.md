# Quickstart: Phase 3 Logic and Performance Fixes

## Goal

Verify the seven scoped fixes end-to-end with the smallest practical manual and automated pass.

## Prerequisites

- Backend API configured against a local PostgreSQL database
- Frontend configured to call the local API
- A database state containing:
  - at least one student
  - one lesson with one video
  - lesson comments in mixed statuses
  - community posts with comments and likes
  - one exam containing at least one timed question and one MCQ eligible for fifty-fifty

## 1. Apply schema and settings changes

1. Run backend migrations against a clean or disposable database.
2. Ensure platform settings include:
   - `VideoWatchThresholdPercentage`
   - `MaxExtraWatchRequestsPerVideo`
   - `HintPenaltyPercentage`
3. Confirm default values exist before manual verification begins.

## 2. Validate watch tracking behavior

1. Call `POST /api/tracking/video-event` with `totalDurationSeconds = 0`.
2. Confirm the API returns `400` with `DURATION_REQUIRED`.
3. Call `POST /api/student/video-session/{lessonVideoId}/track-progress` with a valid duration twice inside the cache window.
4. Confirm the returned threshold is stable across requests.
5. Update the relevant platform setting from the admin flow.
6. Repeat the tracking call and confirm the new threshold takes effect immediately after the update.

## 3. Validate extra watch request limits

1. Submit repeated `POST /api/student/video-session/{lessonVideoId}/request-extra` calls for the same student and video.
2. Confirm requests succeed only up to the configured maximum.
3. Confirm the next request returns a clear limit-reached failure message.

## 4. Validate moderation filtering and aggregation

1. Call `GET /api/admin/lessons/{lessonId}/comments?status=Pending`.
2. Confirm only pending comments are returned.
3. Call the same endpoint with an invalid status value.
4. Confirm the API returns `400`.
5. Load `GET /api/admin/community/posts` against a dataset with many posts, comments, and likes.
6. Confirm the response still contains `commentCount` and `likeCount` for every post.
7. Compare query count or timing against the pre-fix baseline to confirm the N+1 reduction.

## 5. Validate exam penalties and timing

1. Start an exam attempt through `POST /api/exams/{examId}/start`.
2. Use `GET /api/exams/{examId}/attempts/{attemptId}/questions/{questionId}/fifty-fifty` on an MCQ.
3. Submit the exam with that question answered correctly.
4. Confirm the final awarded score reflects the configured penalty and does not fall below zero.
5. Start a new attempt with a timed question, wait until the allowed duration has passed, then submit.
6. Confirm the returned question result is timed out and awards zero points.

## 6. Regression checks

1. Run backend automated tests covering tracking, moderation, and exam scoring flows.
2. Run API-level checks for the affected endpoints.
3. Run frontend lint/tests if student or admin UI handling changes were required for the updated response semantics.
