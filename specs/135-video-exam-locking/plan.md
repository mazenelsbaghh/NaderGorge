# Implementation Plan: Video Exam Locking & Lesson Cascade Lock

**Branch**: `135-video-exam-locking` | **Date**: 2026-06-16 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/135-video-exam-locking/spec.md)
**Input**: Feature specification from `/specs/135-video-exam-locking/spec.md`

## Technical Context
We will modify the backend's lesson details and lesson list queries, as well as the frontend carousel component, to enforce video-level exam locking and cascade it to subsequent lessons.

## Constitution Check
We confirm that this plan adheres to the codebase's access control boundaries and sequential progression locking conventions.

## Phase 0: Outline & Research
Research findings are stored in `research.md`. The design leverages existing relationships to check if preceding video exams are passed.

## Phase 1: Design & Contracts
- The updated schema entities and access check flow are detailed in `data-model.md`.
- No new external API endpoints or contract changes are introduced.
- Build and verification commands are listed in `quickstart.md`.

## Proposed Changes

### Backend Component

#### [MODIFY] [GetLessonDetailQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs)
- Update `GetLessonDetailQueryHandler` video loop:
  - Set `isExamLocked = anyPrecedingExamNotPassed || examsForVideo.Any(e => e.IsMandatory && !e.Passed);`
- Update previous lesson locking checks:
  - Query exams linked to the previous lesson's videos.
  - If any video exam in the previous lesson is mandatory and unpassed, set `isLocked = true` and populate `lockedReason` with descriptive text.

#### [MODIFY] [GetLessonsQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonsQuery.cs)
- Update `GetBlockingStateAsync` method:
  - Query exams linked to the previous lesson's videos.
  - If any video exam in the previous lesson is mandatory and unpassed, return `IsLocked: true` and the Arabic locked reason.

### Frontend Component

#### [MODIFY] [LessonCarousel.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/lessons/%5BlessonId%5D/components/LessonCarousel.tsx)
- Calculate `precedingVideoExamUnpassed`:
  ```typescript
  const precedingVideoExamUnpassed = videos.slice(0, activeStep).some(v => v.examId && !v.examPassed);
  ```
- Change the `disabled` property of the Exam button:
  ```typescript
  disabled={precedingVideoExamUnpassed && !examPassed}
  ```

## Verification Plan

### Automated Tests
- Run e2e Pytest content flow test validating the video self-lock and subsequent lesson lock.
  ```bash
  BASE_URL=http://localhost:5246 .venv/bin/pytest tests/test_e2e_content_flow.py -v
  ```

### Manual Verification
- Check the lesson player view for the locked screen when a video has an unpassed exam.
- Check that the exam button remains active/clickable when the video is self-locked.
