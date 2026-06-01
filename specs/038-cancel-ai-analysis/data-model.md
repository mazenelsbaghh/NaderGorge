# Cancel AI Analysis Data Model

No new database tables are needed. The cancel action operates directly on the existing `bull:ai-video-chapters` Redis collection and updates the `IsProcessingAI` flag on `LessonVideo` within the .NET backend via webhook or explicit update.

## State Transitions
- **Waiting -> Cancelled**: Job removed from Redis.
- **Active -> Cancelled**: Worker terminates `yt-dlp` or Gemini fetch, job marked failed with reason "Cancelled by Admin".
- **Failed -> Cancelled**: Job removed from failed queue so UI resumes to idle.
