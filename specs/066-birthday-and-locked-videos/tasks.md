# Tasks: Student Birthday Greetings & Video Exam Progression

**Input**: Design documents from `/specs/066-birthday-and-locked-videos/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

---

## Phase 1: Setup

- [x] T001 Verify project directories and environment config files are present.

---

## Phase 2: User Story 1 - daily birthday greeting script

**Goal**: Implement a script that congratulates students celebrating their birthdays via in-app notification and WhatsApp.
**Independent Test**: Run the script with mock data and check tables `notification_events` and worker logs.

- [x] T002 [US1] Create the birthday congratulator script file in `worker/src/scripts/birthday-congratulator.ts`. The script connects to PostgreSQL via a connection pool (`pg`), queries for active students whose birth day and month matches today's date in `Africa/Cairo` timezone, handles leap year Feb 29/Mar 1 edge cases, inserts an in-app `NotificationEvent` (ChannelType = 0, Status = 1) for each student, and sends a WhatsApp message using a POST request to Evolution API if configured.
- [x] T003 [US1] Add the command task to `worker/package.json` scripts:
  ```json
  "congratulate-birthdays": "tsc -p tsconfig.json && node dist/scripts/birthday-congratulator.js"
  ```
- [x] T004 [US1] Add database connection helper or validation to check that the birthday script runs correctly and logs details.

---

## Phase 3: User Story 2 - Video Exam Progression Backend & Frontend

**Goal**: Sort videos and lock subsequent videos if preceding ones have unpassed exams.
**Independent Test**: Request lesson details with unpassed video exams, check `isExamLocked` in response, and verify step navigation is locked.

- [x] T005 [US2] Update the DTO `VideoDto` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs` to add:
  ```csharp
  Guid? ExamId = null,
  bool ExamPassed = false,
  bool IsExamLocked = false
  ```
- [x] T006 [US2] Update the handler `GetLessonDetailQueryHandler` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs` to retrieve whether the student has passed any exam linked to any video in the lesson. Enforce progression: traverse videos sorted by `Order` ascending; if a previous video had an exam and the student has not passed it, mark subsequent videos with `IsExamLocked = true`.
- [x] T007 [US2] Update frontend `VideoDto` type definition in `frontend/src/services/content-service.ts` to expose `examId`, `examPassed`, and `isExamLocked`.
- [x] T008 [US2] Update frontend `LessonCarousel.tsx` `Steps` component. If `video.isExamLocked` is true, render a lock icon instead of the step index number, disable clicking/selecting that step, and show the step as locked. If a video has `examId` but is not locked, render a small "Exam/Quiz" badge next to its title.

---

## Phase 4: User Story 3 - Video Player Lock Overlay

**Goal**: Show a beautiful locked screen in the player with direct quiz access.
**Independent Test**: Navigate to a locked video slide and verify the overlay details and quiz link.

- [x] T009 [US3] Update frontend `SecureVideoPlayer.tsx`. Inspect the video model: if `isLocked` (watch limit) OR `isExamLocked` is true, immediately transition status to `'locked'`.
- [x] T010 [US3] Update frontend `SecureVideoPlayer.tsx` lock screen render function. If the video is locked due to an unpassed previous video exam (`isExamLocked == true`):
  - Display the message: `"الفيديو مغلق. يرجى اجتياز امتحان الفيديو السابق أولاً."`
  - Display a gold-themed CTA button: `"اذهب للامتحان"`
  - On clicking the button, navigate the student to: `/student/exams/[examId]?packageId=[packageId]` where `[examId]` is the ID of the previous video's exam.
  - Disable and prevent requesting a playback session for this video from the backend.

---

## Phase 5: Polish & Verification

- [x] T011 Verify code builds successfully on backend and frontend.
- [x] T012 Run the birthday script and verify mock run behavior.
- [x] T013 Verify the video progression lock behavior in frontend pages.
