# Feature Specification: Video Exam Locking & Lesson Cascade Lock

**Feature Branch**: `135-video-exam-locking`  
**Created**: 2026-06-16  
**Status**: Draft  
**Input**: User description: "The video exam should lock the specific video that it belongs to, and the video itself should not play/open until the student solves the exam. Also, subsequent lessons should be locked by this exam and state that there is an unpassed video exam in the preceding lesson."

## Clarifications
- **Locked Reason Language**: All locked reasons shown to the student must be in Arabic.
- **Cascading Blockage**: If any video in the preceding lesson has a mandatory exam that is not passed, the subsequent lesson is completely locked.
- **Top Exam Button**: In the lesson video carousel, the exam button for the current active video should remain active (enabled) even if that video is locked by its own exam, so that the student can click it to solve the exam. It should only be disabled if a preceding video's exam is not passed.

## User Scenarios & Testing

### User Story 1 - Video Exam Locks its Own Video (Priority: P1)
As a student, when I click on a video that has an associated mandatory exam that I haven't passed yet, the player should show a lock screen and prevent playback.

**Why this priority**: Core requirement of video exam locks.

**Independent Test**: Assert that the lesson detail returns `isExamLocked = true` for a video that has an unpassed mandatory exam, and the playback session endpoint returns `400 Bad Request` or `403 Forbidden` if they try to bypass.

---

### User Story 2 - Subsequent Lessons Locked by Preceding Lesson's Video Exam (Priority: P1)
As a student, I want subsequent lessons to be locked if any preceding lesson has a video with a mandatory exam that is not passed yet, so that I cannot skip progression requirements.

**Why this priority**: Ensures course path integrity.

**Independent Test**: Verify that `GetLessonsQuery` and `GetLessonDetailQuery` return `isLocked = true` and `blockingExamId = [VideoExamId]` for subsequent lessons.

---

## Requirements

### Functional Requirements
- **FR-001**: `GetLessonDetailQueryHandler` must mark a video as `isExamLocked = true` if that video itself has an unpassed mandatory exam or any preceding video has an unpassed mandatory exam.
- **FR-002**: `GetLessonDetailQueryHandler` and `GetLessonsQueryHandler` must lock a lesson if the preceding lesson contains any video with a mandatory exam that is not passed.
- **FR-003**: The frontend `LessonCarousel` must enable the exam button if the lock is only due to the current video's own exam (so they can take it), but disable it if there is a preceding video exam that is unpassed.

## Success Criteria

### Success Criteria
- **SC-001**: Playback sessions are blocked in 100% of cases for locked videos.
- **SC-002**: Lesson lock reasons are displayed correctly in 100% of cases when a cascading block is active.
- **SC-003**: The E2E pytest suite passes successfully in under 15 seconds.
- **SC-004**: Frontend linter passes with 0 warnings and 0 errors.
