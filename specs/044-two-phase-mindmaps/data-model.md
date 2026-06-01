# Data Model: Two-Phase AI Mindmap Generation

## Entity Updates

### `LessonVideo`

The `LessonVideo` entity will be updated to include an additional status flag to track the separate, manual generation process for mind maps.

**New Field:**
- `IsProcessingMindmaps` (Boolean): Flag representing whether the background image generation job is currently running.

### Application Layer Contracts

#### `GenerateChapterMindmapsCommand`
**Purpose**: Triggered by an Admin to explicitly generate mind maps for all existing chapters of a given video.
**Payload**:
- `LessonId` (Guid)
- `VideoId` (Guid)

## Queue Modifications

### Redis / BullMQ Job Queue: `generate-chapter-mindmaps`
**Purpose**: Dedicated queue for parallel processing of mind map generation using the Gemini AI API, separating it from the audio transcript analysis.
**Payload**:
```json
{
  "lessonId": "uuid",
  "videoId": "uuid",
  "teacherPhotoUrl": "https://..." 
}
```
