# Phase 1: Data Model Updates

No schema changes are required to fix the concurrency bug. However, the interactions with existing tables will be updated:

### LessonVideo (Table)
- **Modifications**:
  - `IsProcessingAI` is set to `false` via `ExecuteUpdateAsync`.
  - `SubtitleUrl` is updated via `ExecuteUpdateAsync`.
  - `UpdatedAt` timestamp is automatically adjusted depending on global interceptors or manual assignment.

### VideoChapter (Table)
- **Modifications**:
  - Existing rows with `LessonVideoId` = `[request_video_id]` will be dropped using `ExecuteDeleteAsync`.
  - Newly generated chapters will be instanced as new records and saved via `AddRangeAsync(chapters)`.
