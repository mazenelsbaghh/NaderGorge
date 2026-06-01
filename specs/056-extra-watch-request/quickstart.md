# Quickstart: Extra Watch Request

This feature adds a manual approval workflow for students who exhausted their video view limits, allowing them to request an extra view from the admin.

## Key Changes
1. **Database Update**: New table `ExtraWatchRequests` added to `AppDbContext`.
2. **Student API**:
   - `POST /api/Student/video-session/{lessonVideoId}/request-extra`: Initiates request.
   - `GET /api/Student/video-session/{lessonVideoId}/request-status`: Used by the video player to see if there's a pending request.
3. **Admin API**:
   - `GET /api/Admin/watch-requests`: Fetch all.
   - `POST /api/Admin/watch-requests/{id}/approve`: Overrides the lock.
   - `POST /api/Admin/watch-requests/{id}/reject`: Denies it.
4. **Frontend Dashboard**: Added `/admin/watch-requests` page.
5. **Video Player**: Updated `SecureVideoPlayer.tsx` fallback view to check the request status.

## How to test
1. Find a student who reached watch limits on a video.
2. In the student view, the locked screen should now display "Request Extra View".
3. Click it and observe it change to "Pending".
4. Navigate to Admin > Watch Requests.
5. Approve the request.
6. The student refreshes the video player page and now has access!
