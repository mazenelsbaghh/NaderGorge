# Tasks: Video Exam Locking & Lesson Cascade Lock

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Implementation Tasks

### T001: Modify GetLessonDetailQuery.cs to Lock Video & Cascade Lessons
- **Target File**: [GetLessonDetailQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs)
- **Task Description**: 
  - Change video self-lock: `isExamLocked = anyPrecedingExamNotPassed || examsForVideo.Any(e => e.IsMandatory && !e.Passed);`
  - Query mandatory exams associated with `previousLesson`'s videos and check if any are unpassed. If so, set `isLocked = true` and populate `lockedReason` and `blockingExamId`.
- **Verification**: Backend builds successfully.

### T002: Modify GetLessonsQuery.cs to Cascade Locks
- **Target File**: [GetLessonsQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonsQuery.cs)
- **Task Description**: Query mandatory exams associated with `previousLesson`'s videos in `GetBlockingStateAsync`. If any are unpassed, return `IsLocked: true` with the Arabic locked reason and blocking exam ID.
- **Verification**: Backend builds successfully.

### T003: Modify LessonCarousel.tsx to Manage Exam Button State
- **Target File**: [LessonCarousel.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/lessons/%5BlessonId%5D/components/LessonCarousel.tsx)
- **Task Description**: Calculate `precedingVideoExamUnpassed` and disable the exam button only if a preceding video has an unpassed mandatory exam.
- **Verification**: Frontend compiles cleanly.

### T004: Add E2E tests for Video Exam Locking & Cascading
- **Target File**: [test_e2e_content_flow.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_e2e_content_flow.py)
- **Task Description**: Add a test scenario checking that a video with a mandatory exam is self-locked, subsequent lessons are locked due to this video exam, and they unlock once passed.
- **Verification**: Run `BASE_URL=http://localhost:5246 .venv/bin/pytest tests/test_e2e_content_flow.py -v`.

### T005: Deep Critique and Quality Checks
- [ ] T005.1: Execute Phase 6 Deep Review and fix any architectural issues
- [ ] T005.2: Execute `clean-code-guard` against changed C# backend and frontend files
- [ ] T005.3: Execute `test-guard` against changed test file
- [ ] T005.4: Execute E2E integration test suite to run the feature tests and verify everything passes successfully
- [ ] T005.5: Run `npm run lint` and verify frontend builds cleanly
