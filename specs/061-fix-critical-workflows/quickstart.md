# Quickstart: Restore Critical Learning Workflows

## Goal

Validate the three repaired workflows end-to-end on the `061-fix-critical-workflows` branch before task breakdown and implementation.

## Prerequisites

1. Apply the generated database migration `20260409141216_AddCommunityCommentModerationAndCriticalExamFixes`.
2. Start the backend API and frontend application with the project's normal local environment.
3. Ensure an admin user, a student user, one approved community post, one exam containing a find-the-mistake question, and one exam containing at least one essay question are available.

## Workflow 1: Community Comment Moderation

1. As a student, create a comment on an approved community post.
2. Confirm the create action succeeds but the comment does not appear in the student-facing comments list.
3. As an admin, request `GET /api/admin/community/comments/pending`.
4. Confirm the new comment appears with `Pending` status.
5. Approve the comment through `POST /api/admin/community/comments/{commentId}/approve`.
6. Refresh the student-facing comments list and confirm the comment is now visible.
7. Repeat with another new comment and reject it through `POST /api/admin/community/comments/{commentId}/reject` with a reason.
8. Confirm the rejected comment never appears in the student-facing comments list and the rejection reason is retained in the admin moderation response.

## Workflow 2: Find-The-Mistake Grading

1. Submit an exam attempt where the find-the-mistake answer matches the expected mistake.
2. Confirm the question is marked correct, earns its points, and the result review reflects the actual submitted mistake representation.
3. Submit another attempt where the selected mistake does not match.
4. Confirm the question is marked incorrect with zero awarded points.
5. Submit an attempt with missing find-the-mistake answer data.
6. Confirm the system treats the answer as unanswered or invalid rather than falling back to multiple-choice scoring.

## Workflow 3: Essay Grading Lifecycle

1. Submit an exam attempt containing at least one essay question.
2. Immediately request `GET /api/exams/attempts/{attemptId}/grading-status`.
3. Confirm each essay answer reports `WaitAI` and the attempt result state is `Pending` or `PartiallyGraded`.
4. Trigger the internal essay-grading callback with a valid internal token.
5. Confirm the callback response returns the same `essaySubmissionId` plus the persisted lifecycle `status`, then confirm the essay advances through the AI-complete handoff and becomes teacher-ready.
6. Attempt teacher grading before AI completion in a separate test and confirm the action is rejected.
7. Open the student result screen after submission and confirm it auto-refreshes grading progress while the result is not final.
8. Finalize teacher grading for all essay submissions tied to the attempt.
9. Confirm the attempt grading state becomes `Completed` and the final result is recalculated without manual repair.

## Recommended Verification Commands

```bash
dotnet test backend/tests/NaderGorge.Application.Tests
cd frontend && npm run lint
```

## Exit Criteria

- Pending community comments are hidden from students until approved.
- Rejected comments stay hidden and preserve moderation context.
- Find-the-mistake grading is deterministic and no longer depends on option-based scoring.
- Essay attempts expose partial grading progress until teacher grading completes.
- Final exam results update correctly once all essay answers are teacher graded.
