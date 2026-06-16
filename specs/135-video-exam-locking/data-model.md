# Data Model: Video Exam Locking & Lesson Cascade Lock

No database migrations or model schema changes are required for this feature, as we are leveraging existing relationships:

## Existing Entities and Relations
1. **LessonVideo**: Has an optional `ExamId` referencing the `Exam` entity.
2. **Exam**: Has an optional `LessonVideoId` referencing the `LessonVideo` entity, and an `IsMandatory` boolean flag.
3. **StudentExamAttempt**: Records if a student has passed an exam (`IsPassed`).
4. **Lesson**: Contains multiple `LessonVideos`.
