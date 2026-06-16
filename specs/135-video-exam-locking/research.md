# Research & Design Decisions: Video Exam Locking & Lesson Cascade Lock

## Decision 1: Video Self-Locking Logic in Backend
- **Decision**: Update `GetLessonDetailQueryHandler` to evaluate:
  ```csharp
  bool isExamLocked = anyPrecedingExamNotPassed || examsForVideo.Any(e => e.IsMandatory && !e.Passed);
  ```
  instead of only using `anyPrecedingExamNotPassed`.
- **Rationale**: Setting `isExamLocked = true` when the video's own mandatory exam is unpassed correctly prevents playback of the video itself.
- **Alternatives Considered**: Frontend-only check. Rejected because the backend must securely reject playback sessions for locked videos and return accurate data in the DTO.

## Decision 2: Cascading Lesson Lock from Preceding Lesson's Video Exams
- **Decision**: Update `GetLessonDetailQueryHandler` and `GetLessonsQuery.cs` to check if the immediate `previousLesson` has any video associated with a mandatory exam that is not passed.
  We will query exams associated with `previousLesson`'s videos:
  ```csharp
  var prevVideoExams = await _db.Exams
      .Where(e => e.IsMandatory && (
          (e.LessonVideoId != null && e.LessonVideo.LessonId == previousLesson.Id) ||
          _db.LessonVideos.Any(lv => lv.LessonId == previousLesson.Id && lv.ExamId == e.Id)
      ))
      .ToListAsync(ct);
  ```
  If any such exam is unpassed, the current lesson is locked with a descriptive Arabic reason indicating which video exam in the previous lesson needs to be resolved.
- **Rationale**: Ensures students cannot skip preceding lesson video exams.
- **Alternatives Considered**: Cascading check across all preceding lessons. Rejected because progression is sequential; a lock on Lesson N propagates to Lesson N+1 automatically.

## Decision 3: Frontend Active Exam Button Availability
- **Decision**: In `LessonCarousel.tsx`, calculate if a *preceding* video's exam is unpassed:
  ```typescript
  const precedingVideoExamUnpassed = videos.slice(0, activeStep).some(v => v.examId && !v.examPassed);
  ```
  And change the exam button's disabled condition to:
  ```typescript
  disabled={precedingVideoExamUnpassed && !examPassed}
  ```
- **Rationale**: If the current video is locked by its own exam, the exam button must remain active so the student can click it to solve the exam and unlock the video. If the lock is due to a preceding video's exam, the button should be disabled.
- **Alternatives Considered**: Exposing another boolean flag like `isPrecedingExamLocked` in the DTO. Rejected because it can easily be calculated on the client side using existing video arrays without changing the DTO schema.
