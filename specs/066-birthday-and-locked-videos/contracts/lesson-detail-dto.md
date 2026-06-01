# Interface Contract: Updated VideoDto Response Schema

This document details the updated response payload contract for the lesson details API.

## API Endpoint
- **Endpoint**: `GET /api/content/lessons/{lessonId}`
- **Handler**: `GetLessonDetailQueryHandler`
- **Response Wrapper**: `ApiResponse<LessonDetailDto>`

## Schema Modifications

### VideoDto

The `videos` collection inside the `LessonDetailDto` response now includes additional properties to support video-specific exams and lock states.

```typescript
interface VideoDto {
  // Existing fields
  id: string;                      // Guid: Unique ID of the lesson video
  title: string;                   // string: Video title
  provider: string;                // string: Video host provider (youtube, telegram, vk, etc.)
  order: number;                   // int: Sort order index
  limit: number;                   // int: Maximum watch limit count
  watched: number;                 // int: Current watch count by student
  isLocked: boolean;               // bool: True if watch limit is exceeded
  watchedSeconds: number;          // int: Total seconds watched
  lastWatchedAt?: string;          // string (ISO datetime): Date of last watch
  subtitleUrl?: string;            // string: Optional subtitle URL
  isProcessingAI: boolean;         // bool: AI transcription job state
  isProcessingMindmaps: boolean;   // bool: Mindmap generation job state
  chapters: VideoChapterDto[];     // Array: Chapter list
  
  // NEW fields
  examId?: string;                 // Guid (nullable): Exam linked directly to this video
  examPassed: boolean;             // bool: True if the student has passed the linked exam
  isExamLocked: boolean;           // bool: True if a preceding video's exam has not been passed
}
```

## Progression Validation Rules (Backend Enforced)

- If `isExamLocked` is `true`, the frontend MUST NOT request a playback session (`POST /api/student/video-session`) for this video. The backend will reject playback session requests with a `400 Bad Request` or `403 Forbidden` if the student attempts to bypass the lock.
