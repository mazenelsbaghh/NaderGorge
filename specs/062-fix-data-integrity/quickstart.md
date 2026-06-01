# Quickstart: Phase 2 Data Integrity Fixes

## Goal

Validate the implemented Phase 2 data-integrity repairs end to end on the `062-fix-data-integrity` branch before merge.

## Prerequisites

1. Apply the generated database migration for this feature branch.
2. Start the backend API and frontend application with the project's normal local environment.
3. Ensure a student user, an admin user, one lesson video with a known duration and finite watch limit, one exam containing at least one essay question with written correction text, and one extra watch request are available.

## Workflow 1: Watch Threshold Consistency

1. Create or reuse a lesson video with a known duration, for example 600 seconds, and a known watch-threshold percentage in platform settings.
2. Call `POST /api/Tracking/video-event` with valid duration data and enough watched seconds to stay below the first threshold.
3. Confirm the response shows `watchCount = 0` for a first partial watch.
4. Repeat using additional watched seconds until the threshold is crossed.
5. Confirm the response increments the count exactly once.
6. Repeat the equivalent scenario through `POST /api/student/video-session/{lessonVideoId}/track-progress`.
7. Confirm both flows return the same threshold seconds and the same counted-view outcome.
8. Repeat either request with `totalDurationSeconds = 0`.
9. Confirm the request is rejected instead of using a fallback duration.
10. In the student player UI, confirm the failure is surfaced as a user-facing error instead of silently retrying.

## Workflow 2: Theme Preference Persistence

1. As a student with an existing profile, call `PUT /api/Student/theme-preferences` with light palette, dark palette, and `currentMode`.
2. Confirm the update succeeds.
3. Call `GET /api/Student/theme-preferences`.
4. Confirm the same `currentMode`, light palette, and dark palette are returned.
5. Repeat the update flow for a student who does not yet have a profile row.
6. Confirm the update succeeds and a subsequent read returns the newly saved preferences.

## Workflow 3: Essay Audio and Written Correction

1. Start and submit an exam attempt that includes an essay answer with both written text and an audio reference.
2. Confirm the submission succeeds without requiring audio for other essay answers.
3. Retrieve the persisted attempt details or result data after completion.
4. Confirm the essay answer still contains the same audio reference.
5. Request the exam result while the attempt is still in progress in a separate test.
6. Confirm `writtenCorrection` is not returned yet.
7. Request the exam result after the exam is complete.
8. Confirm `writtenCorrection` is included for applicable questions.
9. Confirm retained essay audio can still be rendered from the returned result payload when present.

## Workflow 4: Extra Watch Rejection Reason

1. As a student, create or reuse an extra watch request for a lesson video.
2. As an admin, reject that request through `POST /api/admin/watch-requests/{id}/reject` with a non-empty reason.
3. Call `GET /api/student/video-session/{lessonVideoId}/request-status` as the student.
4. Confirm the response reports `Rejected` and returns the same rejection reason.
5. Repeat the status check for a pending or approved request.
6. Confirm no rejection reason is returned.

## Recommended Verification Commands

```bash
dotnet test backend/tests/NaderGorge.Application.Tests
dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj
cd frontend && npm run lint
```

## Exit Criteria

- Equivalent watch behavior through both tracking flows yields identical threshold and watch-count outcomes.
- Missing-duration tracking requests fail validation instead of inventing duration values.
- Theme preference reads return the same current mode that the student last saved, including for newly created profiles.
- Essay audio references survive submission and later retrieval.
- Written corrections appear only after the exam result is eligible for final review.
- Rejected extra watch requests return a student-visible rejection reason, while non-rejected requests do not.
