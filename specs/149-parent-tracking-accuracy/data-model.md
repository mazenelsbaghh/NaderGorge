# Data Model: Parent Tracking Accuracy

## Parent Tracking Teacher

- **Source**: `TeacherProfile` reached through `Lesson.ContentSection.Term.Package.Teacher`.
- **Fields returned**: `teacherId`, `teacherName`, `specialization`, `profileImageUrl`.
- **Visibility rule**: Included only if the student has at least one active purchased lesson owned by that teacher.

## Purchased Lesson

- **Source entities**: `StudentAccessGrant`, `Package`, `Term`, `ContentSection`, `Lesson`, `LessonVideo`.
- **Fields used**: lesson ID, lesson title, teacher ID/name/profile data.
- **Active entitlement rule**: grant `IsActive == true`, `CancelledAt == null`, and `ExpiresAt == null || ExpiresAt > now`.
- **Grant target expansion**:
  - Package grant includes all lessons under the package.
  - Term grant includes all lessons under the term.
  - Content section grant includes all lessons under the section.
  - Lesson grant includes that lesson.
  - Lesson video grant includes the video's lesson.
  - Exam grant may expose exam review only if the exam can be tied back to a purchased lesson/video; it must not create unrelated lesson visibility.

## Watch Log Lesson

- **Source entities**: purchased lessons, `LessonVideo`, `VideoWatchEvent`, `LessonProgress`.
- **Returned fields**: teacher ID/name, lesson ID/title, total active videos, watched distinct videos, watch count sum, watched seconds sum, completion state, latest watch timestamp.
- **Aggregation rule**: Aggregate all watch events for all videos in the lesson for the student.
- **Empty rule**: Purchased lessons with zero watch events still return with zero metrics.

## Exam Review

- **Source entities**: `Exam`, `Lesson.ExamId`, `LessonVideo.ExamId`, `Exam.LessonVideoId`, `StudentExamAttempt`, `StudentAnswer`, `ExamQuestion`, `QuestionBankItem`, `QuestionOption`.
- **Returned fields**: exam ID/attempt ID when present, teacher ID/name from purchased lesson, title, score, total score, percentage, status, submission timestamp when present, mistakes.
- **State rules**:
  - `NotStarted`: no attempt exists.
  - `Passed`/`Failed`: attempt exists and grading data is available.
  - Additional existing states may be mapped if present.
- **Mistake rule**: Only incorrect answers appear in mistakes; unattempted exams return an empty mistake list.

## Homework Review

- **Source entities**: `Homework`, `HomeworkSubmission`, `HomeworkAnswer`, `HomeworkQuestion`, purchased lesson map.
- **Returned fields**: homework ID, teacher ID/name from purchased lesson, title, submission state, grade/evaluation, submission timestamp, mistakes.
- **State rules**:
  - `NotSubmitted`: no submission.
  - Existing `SubmissionStatus` values (`InProgress`, `PendingReview`, `Graded`, `Missed`) pass through or map to Arabic labels in the UI.
- **Mistake rule**: Answers with missing score or score below points are review items; unsubmitted homework returns an empty mistake list.

## Balance Detail

- **Source entities**: `StudentBalance`, `BalanceTransaction`.
- **Returned fields**: current balance, transaction amount, balance after, transaction type, description, created date.
- **Ordering**: newest transactions first.
- **Missing row rule**: no balance row returns current balance `0` and an empty transaction list.

## Android Client Models

- `StudentDetailsResponse` owns teacher, watch, exam, homework, warning, and balance lists.
- New or deployment-sensitive fields are nullable or defaulted to prevent crashes against older backend payloads.
- UI filtering uses `teacherId` values returned by the backend; no client-side teacher derivation.
