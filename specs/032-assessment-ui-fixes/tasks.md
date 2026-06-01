# Implementation Tasks: Assessment UI Fixes

**Feature Branch**: `032-assessment-ui-fixes`
**Created**: 2026-03-31

## Phase 1: Setup & Foundational Fixes

*Goal: Ensure the core exam backend endpoint does not crash and interfaces are correct.*

- [x] T001 Backend Fix: Refactor `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamAttemptCommand.cs` with defensive null checks to resolve the `500 Internal Server Error` on submission.
- [x] T002 Backend DTO Update: Extend the `LessonDetailDto` in `backend/src/NaderGorge.Domain/DTOs` (or appropriate directory) to include `BlockingExamId` and `BlockingHomeworkLessonId`.
- [x] T003 [P] Backend Logic: Map the newly added blocking IDs correctly inside `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs`.

## Phase 2: Refined Exam Taking Experience (US1)

*Goal: Execute purely visual and state-related fixes to ensure student exam environment is fully distraction-free and accurate.*

- [x] T004 [US1] Frontend State Update: Integrate `useLessonFocusStore` within `frontend/src/components/exams/ExamViewer.tsx` to mount focus mode immediately upon exam load and detach it on unmount.
- [x] T005 [P] [US1] Frontend Layout Fixes: Strip out the animated grid CSS from `frontend/src/app/student/exams/[examId]/page.tsx` or its wrapper.
- [x] T006 [P] [US1] Frontend Duplication Fix: Remove the duplicate question number styling block wrapping `AnimatedStepper.tsx` or inside `ExamViewer.tsx` to clean up the UI.
- [x] T007 [P] [US1] Frontend Bug Fix: Resolve the React array unique `key` duplication warning in the `AnimatedStepper` question rendering map.

## Phase 3: Quick Navigation for Locked Lessons (US2)

*Goal: Make the lesson lock screen actionable based on backend DTO IDs.*

- [x] T008 [US2] Frontend Type Sync: Update TypeScript types in `frontend/src/types/content.ts` (or standard DTO file matching `LessonDetailDto`) to include `blockingExamId?: string` and `blockingHomeworkLessonId?: string`.
- [x] T009 [US2] Frontend UI Interactivity: Update `frontend/src/components/content/LessonViewer.tsx` to display contextual navigation action buttons when the lock screen active and blocking IDs are present.

## Phase 4: Unified Admin Question Table (US3)

*Goal: Ensure the same question display/addition UX applies universally for Admins building homework vs exams.*

- [x] T010 [US3] Frontend Admin Assessment Table: Update `frontend/src/components/admin/UnifiedAssessmentBuilder.tsx` to port the advanced grid/table row mapping for Questions over to Homework.
- [x] T011 [US3] Frontend Admin Assessment Actions: Ensure the identical "Add Question" modal configuration or sub-routine opens for both exams and homework paths within the Unified Assessment builder.

## Dependencies

- Phase 1 (Foundational Fixes) must be completed before UI dependencies relying on DTOs (US2) can be properly mapped.
- US1 UI cleanup is highly parallelizable and doesn't explicitly rely on Phase 1 completions (beyond the Submit bug).
- US3 Admin UI is distinct and isolated from US1 and US2, suitable for parallel development.

## Implementation Strategy

We begin immediately with fixing the critical 500 generic error blocking the `SubmitExamAttempt` before touching UI to guarantee an operational foundation. We'll proceed with extending the `GetLessonDetail` query since the file is currently active. Finally, all visually scoped frontend tasks in TSX files will be systematically knocked out (focus mode, grid removal, duplicate number cleanup, quick-nav button, unified table).
