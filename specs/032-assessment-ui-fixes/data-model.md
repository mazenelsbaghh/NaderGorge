# Data Model

### `LessonDetailDto` (Frontend Mapping)
When a lesson is locked, the backend currently provides `isLocked: boolean` and `lockedReason: string`. We will expand this structure to facilitate direct navigation.

- `blockingExamId`: `string | null` (The ID of the exam preventing lesson access)
- `blockingHomeworkLessonId`: `string | null` (The ID of the lesson whose homework is preventing access)

### `StudentExamAttempt` (Backend Updates)
Investigate and harden nullable types during the submission mapping process specifically around the attempt evaluation payload. No direct schema change, but mapping logic needs tightening.
