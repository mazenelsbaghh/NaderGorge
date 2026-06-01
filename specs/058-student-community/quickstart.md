# Quickstart: Student Community

This feature adds a moderated student community feed where students can submit posts, wait for admin approval, and then receive likes and comments from other students once approved.

## Key Changes

1. **Database Update**: Add new persistence for `CommunityPost`, `CommunityPostComment`, and `CommunityPostLike`, including moderation metadata for posts.
2. **Student APIs**:
   - `GET /api/community/posts`: Fetch approved community posts for the main feed.
   - `POST /api/community/posts`: Submit a new post in pending state.
   - `GET /api/community/posts/mine`: Show the current student's submitted posts and statuses.
   - `GET /api/community/posts/{postId}/comments`: Fetch comments for an approved post.
   - `POST /api/community/posts/{postId}/comments`: Add a comment to an approved post.
   - `POST /api/community/posts/{postId}/likes/toggle`: Toggle the current student's like on an approved post.
3. **Admin APIs**:
   - `GET /api/admin/community/posts`: Load the moderation queue, optionally filtered by status.
   - `POST /api/admin/community/posts/{postId}/approve`: Approve a pending post.
   - `POST /api/admin/community/posts/{postId}/reject`: Reject a pending post.
4. **Student UI**: Add a student community page under `frontend/src/app/student/community/` with feed rendering, post composer, pending-state feedback, likes, and comment interactions.
5. **Admin UI**: Add an admin moderation surface under `frontend/src/app/admin/community/` or an equivalent routed admin screen that uses shared admin shell/table primitives.
6. **Authorization and Audit**: Student actions require authenticated student access, while moderation actions require admin privileges and must be logged as state-changing content review operations.

## How to Test

1. Open the student community page as an authenticated student.
2. Verify the page renders an empty state if no approved posts exist.
3. Submit a valid post and confirm the UI reports that it is waiting for review.
4. Verify the submitted post appears in the student's own submissions view as `Pending` and does not appear in the public feed yet.
5. Open the admin community moderation page as an admin.
6. Confirm the pending post appears with student, content, and status details.
7. Approve the post and verify it appears in the public student feed.
8. Like the approved post from another student account and confirm the count updates without allowing duplicate active likes.
9. Add a comment to the approved post and confirm it appears in the post discussion list.
10. Submit another post, reject it from the admin view, and verify it never appears in the public feed.
