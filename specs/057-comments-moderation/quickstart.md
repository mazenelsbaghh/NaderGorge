# Quickstart: Lesson Comments Moderation

This feature adds a moderated comments section beneath the lesson video area, allowing students to submit lesson-related questions while teachers or admins approve or reject comments from the existing lesson management page.

## Key Changes

1. **Database Update**: Add a new `LessonComment` entity with lesson linkage, author linkage, moderation status, and review metadata.
2. **Student APIs**:
   - `GET /api/content/lessons/{lessonId}/comments`: Fetch approved comments for display under the lesson video area.
   - `POST /api/content/lessons/{lessonId}/comments`: Submit a new comment in pending state.
   - `GET /api/content/lessons/{lessonId}/comments/mine`: Show the student's own comment statuses.
3. **Teacher/Admin APIs**:
   - `GET /api/admin/lessons/{lessonId}/comments`: Load lesson comments for moderation in the lesson cockpit.
   - `POST /api/admin/comments/{commentId}/approve`: Approve a pending comment.
   - `POST /api/admin/comments/{commentId}/reject`: Reject a pending comment.
4. **Student UI**: Extend `LessonViewer.tsx` to render a comments block below the lesson video experience, including approved comments, submission form, and pending-state feedback for the current student.
5. **Teacher UI**: Extend `frontend/src/app/admin/content/lessons/[id]/page.tsx` with a comments moderation surface, ideally as a dedicated tab that uses shared admin components.
6. **Audit and Authorization**: Moderation actions must be restricted to staff roles and logged as state-changing academic content actions.

## How to Test

1. Open a lesson as a student with access to that lesson.
2. Verify that a comments section appears beneath the lesson video area, even when no approved comments exist.
3. Submit a valid comment and confirm the UI reports that it is waiting for review.
4. Refresh the lesson page and verify the student's own submitted comment still shows `Pending` while the public list remains unchanged.
5. Open the same lesson in the admin/teacher lesson cockpit.
6. Navigate to the comments moderation surface and verify the pending comment appears with the student name, text, and status.
7. Approve the comment and confirm it becomes visible in the public lesson page.
8. Submit another comment, reject it from the moderation surface, and confirm it never appears in the public lesson page.
