# Feature Specification: Student Birthday Greetings & Video Exam Progression

**Feature Branch**: `066-birthday-and-locked-videos`  
**Created**: 2026-06-01  
**Status**: Draft  
**Input**: User description: "إضافة ميزة التهنئة بعيد ميلاد الطالب: (سكربت منفصل تماماً بيشتغل بناءً على تاريخ اليوم). إظهار علامة قفل على الفيديو المرتبط بامتحان وترتيب الفيديوهات: (تحسين لعرض المناهج الحالية)."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Daily Student Birthday Greetings Script (Priority: P1)

As the system administrator, I want to run a standalone script periodically (daily) that scans the student database and automatically sends birthday congratulations to students whose birth day and month matches today's date in local Egypt time.

**Why this priority**: Enhances student relationship management, gamification, and platform engagement on their special day.

**Independent Test**: Can be tested by running the birthday greeting script locally with mock student records having today's birth day and month, and verifying that:
1. A database record is inserted in the `notification_events` table (type: `InApp`/`SMS`) for each birthday student.
2. A WhatsApp message request is sent to the Evolution API for each student with a valid phone number.

**Acceptance Scenarios**:

1. **Given** a student has their birthday today (month/day matching current date in Egypt timezone), **When** the script is executed, **Then** a birthday notification event is created for them in the database, and a personalized WhatsApp message is sent via Evolution API.
2. **Given** the script is run on a day when no student has a birthday, **When** the script executes, **Then** it completes successfully without creating any notifications or calling Evolution API.
3. **Given** a student has their birthday today but Evolution API fails or is unconfigured, **When** the script executes, **Then** it still creates the in-app notification successfully and logs the WhatsApp failure without crashing the script.

---

### User Story 2 - Video Exam Progression Lock & Ordering (Priority: P1)

As a student, I want lesson videos to be displayed in a strict chronological sequence (ordered by their admin-defined order), and I want subsequent videos to be locked with a "lock" icon if a preceding video is linked to an exam that I have not yet passed.

**Why this priority**: Enforces learning progression integrity. Students must prove they understood the material of Video N by passing its quiz before they can move to Video N+1.

**Independent Test**: Can be tested by visiting a lesson page with multiple videos where Video 1 has a mandatory exam. Verify Video 1 is unlocked, Video 2 is locked, and displays a lock icon. Upon passing Video 1's exam, Video 2 becomes unlocked.

**Acceptance Scenarios**:

1. **Given** a lesson has 3 videos sorted sequentially (Video 1, Video 2, Video 3), and Video 1 has an associated exam, **When** the student views the lesson details, **Then** Video 1 is unlocked, but Video 2 and Video 3 are locked and display a lock icon in the step navigation.
2. **Given** a student tries to select/click a locked video step, **When** they click it, **Then** it does not open, or it displays a premium locked screen in the player with a message explaining the requirement.
3. **Given** Video 2 is locked because Video 1's exam is not passed, **When** the student goes to the exam page, passes it, and returns, **Then** Video 2 is automatically unlocked and playable.

---

### User Story 3 - Video Lock Screen with Direct Quiz Access (Priority: P2)

As a student viewing a video that is locked because of an unpassed exam, I want to see a beautiful locked overlay in the player that lists the exact requirement and provides a direct "اذهب للامتحان" (Go to Exam) button to take the quiz.

**Why this priority**: Improves user experience and navigation efficiency, directing the student directly to the next actionable step.

**Independent Test**: View a locked video, verify the lock screen shows the text "هذا الفيديو مغلق. يجب اجتياز امتحان الفيديو السابق أولاً" and contains a working link to the exam.

**Acceptance Scenarios**:

1. **Given** a student is on a locked video slide in the Lesson Carousel, **When** the player displays, **Then** it shows a premium lock screen (matching the design system) with a description and a button redirecting to the exam.

---

## Edge Cases

- **Leap years**: How does the script handle students born on February 29th during non-leap years? (Assumption: Congratulate them on February 28th or March 1st).
- **Timezone shifts**: Running the script from a server in UTC vs. Egypt time (UTC+2 or UTC+3). (Script MUST explicitly convert current time to `Africa/Cairo` time zone to determine "today").
- **Multiple exams**: If multiple previous videos have unpassed exams, does it link to the first unpassed exam? (Yes, it should direct to the first unpassed exam in order).
- **Deleted exams**: What if the admin removes an exam from a video? (The video should immediately unlock).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a standalone script under the Node.js `worker` that executes independently of the main Express server to process birthdays.
- **FR-002**: The birthday script MUST query the database to find all active students whose `DateOfBirth` matches today's day and month (relative to `Africa/Cairo` timezone).
- **FR-003**: The script MUST insert a new `NotificationEvent` in the database with status `Pending` (or `Sent` for InApp) and type `InApp` containing a warm birthday message.
- **FR-004**: If Evolution API credentials are provided in the environment, the script MUST call the Evolution API `message/sendText/{instance}` endpoint to send a WhatsApp birthday greeting to the student's phone number.
- **FR-005**: The C# backend `GetLessonDetailQueryHandler` MUST return videos sorted by `Order` ascending.
- **FR-006**: The C# backend `GetLessonDetailQueryHandler` MUST include the `ExamId` of each video in the `VideoDto`.
- **FR-007**: The C# backend `GetLessonDetailQueryHandler` MUST determine if each video is locked:
  - A video is locked if its own `IsLocked` (watch limit reached) is true, OR if any previous video in the sorted list has an `ExamId` and the user has not yet passed that exam.
- **FR-008**: The frontend `LessonCarousel` steps navigation MUST show a lock icon for locked videos and disable selection.
- **FR-010**: The frontend `SecureVideoPlayer` MUST show a locked overlay for videos locked due to unpassed exams, with a direct link to the exam.

---

## Key Entities *(include if feature involves data)*

- **NotificationEvent**: Represents the in-app notification sent to the student.
- **StudentProfile**: Stores the student's `DateOfBirth` used for birthday checks.
- **LessonVideo**: Stores the video `Order` and `ExamId` (if any).
- **StudentExamAttempt**: Tracks whether the student passed (`IsPassed = true`) the exam associated with a video.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Birthday script runs to completion and successfully congratulates all birthday-matching students in less than 2 minutes.
- **SC-002**: 100% of students whose birthday is today receive both an in-app notification and a WhatsApp message (if valid number and API active).
- **SC-003**: Lesson videos are strictly sequential and locked/unlocked in accordance with video-level exam completions.
- **SC-004**: The lock overlay renders within the custom player without layout shifts or performance lag.

---

## Assumptions

- **Timezone**: Egypt timezone (`Africa/Cairo`) is the source of truth for "today's date".
- **Execution**: The birthday script is intended to be run daily (e.g. via a cron manager or docker command).
- **Access**: In-app notifications are visible on the student's dashboard.
