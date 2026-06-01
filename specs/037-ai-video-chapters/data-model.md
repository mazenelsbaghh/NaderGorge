# Data Model

## 1. Entities

### VideoChapter
Represents a discrete sequence or topic within a `LessonVideo`, populated via AI analysis.

*   `Id` (Guid, PK)
*   `LessonVideoId` (Guid, FK)
*   `Title` (string, MaxLength 200, Required)
*   `StartTime` (int, Required) // Seconds from start
*   `EndTime` (int, Required) // Seconds from start
*   `SummaryText` (string, MaxLength 2000, Required)
*   `Order` (int, Required) // Sequential order of the chapter

### LessonVideo (Extension)
*   `SubtitleUrl` (string, Nullable) // Path or URL to the generated `.srt` file
*   `IsProcessingAI` (bool, Default: false) // Indicates if the video is currently in the BullMQ queue
*   `VideoChapters` (ICollection<VideoChapter>)

## 2. Validation Rules

*   `VideoChapter.StartTime` must be >= 0 and < `EndTime`.
*   `LessonVideo.SubtitleUrl` must be a valid absolute or relative URL path if present.

## 3. Relationships

*   **LessonVideo (1) -> (*) VideoChapter**: Cascade delete enabled. If the video is deleted, its chapters are also deleted.

## 4. State Transitions

*   **Idling/Ready**: `LessonVideo.IsProcessingAI = false`
*   **Processing**: When Admin clicks "Generate Chapters", `IsProcessingAI` becomes `true`. The job is queued in Redis.
*   **Completed**: The NodeWorker finishes via Gemini API, uploads the SRT, inserts the `VideoChapter` rows via an internal Callback API, and resets `LessonVideo.IsProcessingAI` to `false`.
