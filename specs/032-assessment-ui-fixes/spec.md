# Feature Specification: Assessment UI Fixes & Enhancements

**Feature Branch**: `032-assessment-ui-fixes`
**Created**: 2026-03-31
**Status**: Draft
**Input**: User description: "شاشه الامتحان مش مظبوطه و الرقم و عايزها برضو ف وضع الفومس و ان الخلفيه الانميشن ماتكنش موجوده فيه عايز جنب الدرس لو مامتحنش تبقي مقوله وزرار اذهب للامتتحان ولو الواجب بتاع اللي قبلها ماتعملش تبقي اذهب لحل الواجب وزرار ادوس عليه يوديني بقي علي اللي مفروض احلوا و المفروضي يبقي فيه في الادمن جدول شبه اللي ف الامتحان و الواجب و صفحه اضافه السئله بتاعت الامتحان تبقي ف الواجب برضو"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Refined Exam Taking Experience (Priority: P1)

Students taking an exam will see a clean, distraction-free environment without animated backgrounds or duplicate question number indicators. The screen automatically enters focus mode to maximize concentration. Also, passing the exam works reliably without server errors.

**Why this priority**: Core student experience. A distorted or distracting UI reduces exam performance, and backend submission errors directly block students from progressing.

**Independent Test**: Can be fully tested by opening a student exam. The background animation should be absent, focus mode enabled, only one clear step indicator is visible, and the final submission works.

**Acceptance Scenarios**:

1. **Given** a student opens an active exam, **When** the exam page loads, **Then** the animated grid background is completely hidden and focus mode is active.
2. **Given** a student is answering an exam question, **When** they look at the question indicator, **Then** they see only one stylized squircle indicator per question without duplicate legacy numbers.
3. **Given** a student completes all questions, **When** they click "Submit", **Then** the submission is processed successfully without a 500 generic error and they see their results.

---

### User Story 2 - Quick Navigation for Locked Lessons (Priority: P2)

When a student tries to access a locked lesson due to incomplete prerequisite assessments (exam or homework), they are presented with a clear action button that immediately redirects them to the specific pending assessment.

**Why this priority**: Enhances the UX by reducing friction. Currently, students have to manually navigate backward to find what they missed.

**Independent Test**: Can be independently tested by clicking on a locked lesson card. The locked reason screen should display "Go to solve Homework" or "Go to Exam" depending on the blocker, and clicking it navigates to the exact assessment.

**Acceptance Scenarios**:

1. **Given** a lesson is locked due to an unpassed previous exam, **When** the student views the locked lesson screen, **Then** a prominent button reads "اذهب للامتحان" (Go to Exam) that links directly to that exam.
2. **Given** a lesson is locked due to an unpassed previous homework, **When** the student views the locked lesson screen, **Then** a prominent button reads "اذهب لحل الواجب" (Go to solve Homework) that links directly to the homework view in the predecessor lesson.

---

### User Story 3 - Unified Admin Question Table (Priority: P3)

Administrators managing assessments will see a unified, consistent question table experience whether they are editing an Exam or a Homework. The sophisticated table UI used for Exams is fully replicated in the Homework builder.

**Why this priority**: Improves administrative efficiency and consistency across the platform.

**Independent Test**: Can be independently tested by opening the Admin Lesson Builder, selecting to add a "Homework", and observing that the added questions appear in the same styled data table used by the Exam builder, and clicking to add/edit questions opens the exact same form.

**Acceptance Scenarios**:

1. **Given** an admin is creating a homework assessment, **When** they view the list of questions added so far, **Then** they see a structured table identical to the Exam question table.
2. **Given** an admin wants to add a new question to a Homework, **When** they click "Add Question", **Then** they are presented with the same detailed question addition form used for exams.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST remove the animated grid background specifically from the `ExamViewer` pages or exam layout wrapping component.
- **FR-002**: System MUST automatically toggle `FocusMode` to `true` when entering an exam view.
- **FR-003**: System MUST remove the old floating extra "1" number indicator from above the question content in the Stepper/Exam UI to eliminate duplication.
- **FR-004**: System MUST fix the `500 Internal Server Error` occurring on the Exam Submission API endpoint (`POST /api/exams/{id}/submit/{attemptId}`). 
- **FR-005**: System MUST render contextual navigation buttons ("اذهب للامتحان" or "اذهب لحل الواجب") inside the `LessonViewer` lock screen, redirecting the user to `examId` or `lessonId` accordingly based on the lock evaluation payload.
- **FR-006**: System MUST pass the exact blocking `examId` or `lessonId` in the `LessonDetailDto.LockedReason` payload from the backend to support these navigation buttons.
- **FR-007**: System MUST replace the rudimentary homework questions list in the admin `UnifiedAssessmentBuilder` with the table layout used currently in the `InlineExamEditor`.
- **FR-008**: System MUST utilize the same question addition/editing form for homework and exams in the admin panel.

### Key Entities

- **LessonDetailDto**: Must include identifiers for the blocking assessment (e.g., `BlockingExamId`, `BlockingHomeworkLessonId`) alongside `LockedReason`.
- **StudentExamAttempt**: The backend entity managing the submission; the crash during submission processing needs resolving.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of exam submissions process correctly without 500 crashes.
- **SC-002**: Students can navigate to a missing assessment from a locked lesson in exactly 1 click.
- **SC-003**: Admin's question management UI operates identically between exams and homework assignments, eliminating UI discrepancies.
- **SC-004**: Visual duplication in the exam stepper UI is completely eliminated.
