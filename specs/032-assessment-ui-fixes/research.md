# Phase 0: Research & Technical Feasibility

## 1. Fixing Exam Submit 500 Error
- **Observation**: A 500 internal server error occurs on `POST /api/exams/{id}/submit/{attemptId}`.
- **Cause Hypothesis**: The `SubmitExamAttemptCommandHandler` is likely encountering an unhandled null referencing issue, specifically with mapping questions, score calculations, or `attempt` nullability when calculating metrics.
- **Resolution**: Refactor the handler to add defensive null checks, ensure proper question option ID matching, and log meaningful error messages.

## 2. Navigational Actions on Locked Lessons
- **Observation**: The `GetLessonDetailQueryHandler` in the backend returns `IsLocked` and `LockedReason`.
- **Requirement**: We need to inject actionable IDs into `LessonDetailDto` so the frontend knows *where* to direct the user.
- **Resolution**: Add `BlockingExamId` (Guid?) and `BlockingHomeworkLessonId` (Guid?) to the DTO payload returned from `GetLessonDetailQueryHandler`. Update the DTO typing in the frontend, and add "Go To Exam / Homework" buttons targeting these routes.

## 3. Unifying Admin UI Question Tables
- **Observation**: `InlineExamEditor` has robust question rendering with a stylized grid using grid-cols formatting. `UnifiedAssessmentBuilder` mapped the `AddHomeworkForm` basic layout.
- **Resolution**: Port the `.map` iterations over `shuffledQuestions` from the Exam visual logic into the Homework render logic within `UnifiedAssessmentBuilder`.

## 4. Cleaning Exam Viewer & Enabling Focus Mode
- **Observation**: ExamViewer renders a large secondary '1' digit from an older layout container.
- **Resolution**: Simply remove `w-full h-full` absolute positioning or wrapper elements around `AnimatedStepper` that have leftover text. 
- **Focus Mode**: Import `useLessonFocusStore`, and use `useEffect(() => { setFocusMode(true); return () => setFocusMode(false); }, [])` at the top level of `ExamViewer`.
- **Grid Background Removal**: In the `app/student/exams/[examId]/page.tsx` or layout, remove the CSS animated grid class, replacing it with a clean solid surface tone for maximum focus.
