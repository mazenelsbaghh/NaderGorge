# Quickstart

## Local Development Execution Flow

1. **Backend API Stabilization:** Start by diagnosing `SubmitExamAttemptCommand.cs` to resolve the `500 Internal Server Error`. Update `GetLessonDetailQuery.cs` to append the navigation IDs (`BlockingExamId`, `BlockingHomeworkLessonId`).
2. **Frontend Locked UI Update:** In `LessonViewer.tsx`, map the new backend variables to render the "اذهب للامتحان" (Go to Exam) and "اذهب لحل الواجب" (Go to Homework) quick action buttons.
3. **Admin Unified Table:** Adjust `UnifiedAssessmentBuilder` to use the grid-based interactive table for Homework, mirroring Exams.
4. **Student Exam Tweaks:** Edit `ExamViewer.tsx` to mount `useLessonFocusStore`, remove `.map()` bugs producing duplicate numbers, and strip out background geometric animations.
