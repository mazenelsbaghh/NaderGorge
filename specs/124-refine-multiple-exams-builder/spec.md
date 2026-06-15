# Feature Specification: Refine Multiple Exams Builder

**Feature Branch**: `124-refine-multiple-exams-builder`  
**Created**: 2026-06-14  
**Status**: Draft  
**Input**: User description: "عايز اضيف بقي الامتحان التاني مش باين /speckit-all اناي عادي اضيف امتحان لحصه و امتحان جوه لفيديو معين"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create Lesson Exam & Video Exam Concurrently (Priority: P1)

As an Admin, I want to clearly distinguish between creating a Lesson Exam (exam for the whole lesson) and a Video Exam (pop quiz for a specific video), so that I can add multiple exams (both a lesson exam and video exams) without confusion or overwriting existing exams.

**Why this priority**: It is the core requirement. Admins need to attach both types of exams without UI confusion or accidental overrides.

**Independent Test**:
Can be fully tested by opening the Lesson Cockpit admin panel, seeing two separate, clear builder cards (one for Lesson Exam, one for Video Pop Quiz) without overlapping settings, and successfully creating both types of exams.

**Acceptance Scenarios**:

1. **Given** a lesson has an existing main exam, **When** I click the "الامتحان المرفق" tab, **Then** I see the main exam details card at the top.
2. **Given** a lesson has videos, **When** I scroll down in the "الامتحان المرفق" tab, **Then** I see a separate "إضافة امتحان فيديو (Pop Quiz)" section with a builder locked to Video target type, allowing me to select a video and add an exam.
3. **Given** a video exam builder is displayed, **When** I fill in the details and click the save button, **Then** it says "إنشاء امتحان الفيديو (Pop Quiz)" and successfully saves the exam associated to the selected video.
4. **Given** I am in the "الفيديوهات" (Videos) tab, **When** I view the list of videos, **Then** any video with one or more exams attached displays a badge "امتحان مرفق" with the count of attached exams.

### Edge Cases

- **No Videos Added**: If the lesson has no videos, the "إضافة امتحان فيديو (Pop Quiz)" section should display a notice stating that no videos are available to attach exams to.
- **Multiple Video Exams**: If a video already has an exam, adding another exam to the same video should be supported, displaying all attached exams under that video in the lists.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin, `/admin/content/lessons/[id]`, navigate to "الامتحان المرفق" tab. Verify that the lesson exam builder and video exam builder are clearly separated, target selection buttons are hidden, and button text is contextual.
- **Manual QA Role/Flow 2**: Admin, `/admin/content/lessons/[id]`, navigate to "الفيديوهات" tab. Verify that videos with exams display the badge "امتحان مرفق".
- **Docker Acceptance**: Production containers `massar_admin` and `massar_platform-backend-1` rebuild and run healthily.
- **External Dependencies**: None.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `UnifiedAssessmentBuilder` MUST support locking the target type to a specific value (`Lesson` or `Video`) via a `forceTargetType` prop.
- **FR-002**: When the target type is locked via `forceTargetType`, the builder MUST hide the "ارتباط الامتحان (الهدف)" selection buttons.
- **FR-003**: The builder's submit button text MUST dynamically adapt to the target context:
  - For `type === 'exam'` and target type `Video`: "إنشاء امتحان الفيديو (Pop Quiz)"
  - For `type === 'exam'` and target type `Lesson`: "إنشاء امتحان الحصة ككل"
  - For `type === 'homework'`: "إنشاء الواجب"
- **FR-004**: The video-level exam builder MUST default to `Video` target type and show the video selection dropdown.
- **FR-005**: The `LessonVideoList` component MUST display the "امتحان مرفق" status badge for any video that has a direct `video.examId` OR has exams in its `video.exams` array.

### Key Entities

- **Lesson**: Has an optional `ExamId` (for the main lesson exam) and a collection of `Videos`.
- **LessonVideo**: Has an optional `ExamId` (legacy relation) and a list of associated `Exams` (the new one-to-many relationship).
- **Exam**: Associated with either a `Lesson` or a `LessonVideo` (via `LessonVideoId`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of admin exam creation flows under the "الامتحان المرفق" tab clearly indicate the specific target of the exam being created.
- **SC-002**: 100% of videos with attached exams display the attachment status badge in the Admin "الفيديوهات" list.

## Assumptions

- The frontend uses the cockpit API query results where `lesson.videos` contains `exams` for each video.
