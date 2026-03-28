# Feature Specification: Inline Lesson Exams

**Feature Branch**: `021-inline-lesson-exams`  
**Created**: 2026-03-28
**Status**: Draft  
**Input**: User description: "عايز اضيف الامتحان من هنا مش من حته تانيه شيلوا لو من حته تانيه واني احدد هو مقالي و لا mcq و احد الاجابه الصح و كده و احدد الامتحان في فيديو ولا علي حصه"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Centralized Inline Exam Creation (Priority: P1)

As an administrator, I want to create and manage exams directly from the Lesson Cockpit (and remove other isolated exam creation pages), so that all content for a lesson is managed in one central place without losing context.

**Why this priority**: Centralizing content management is critical for the instructor's UX, preventing them from jumping between multiple pages to assemble a single lesson.

**Independent Test**: Can be fully tested by navigating to the Lesson Cockpit's Exam tab, creating a new exam entity inline, and verifying that old, isolated exam creation links are either removed or redirect back to the central cockpit approach.

**Acceptance Scenarios**:

1. **Given** the Admin is in the Lesson Cockpit on the Exams tab, **When** they click "Create Exam", **Then** an inline interface opens to define the exam properties.
2. **Given** the old standalone exam creation pages, **When** the Admin tries to access them, **Then** they are instructed or redirected to manage exams within the specific lesson cockpit.

---

### User Story 2 - Defing Exam Questions & Types (Priority: P1)

As an administrator, I want to add multiple questions to the exam, specifying whether each question is an Essay or Multiple Choice (MCQ). For MCQs, I need to define the options and indicate which answer is correct.

**Why this priority**: Without question definition (MCQ vs Essay), the exam is just an empty container. This is the core functionality of assessments.

**Independent Test**: Can be fully tested by adding an MCQ to an exam, adding 4 choices, selecting the correct one, and then adding an Essay question, saving successfully.

**Acceptance Scenarios**:

1. **Given** the inline exam editor, **When** adding a new question, **Then** the Admin can select the question type (MCQ or Essay).
2. **Given** an MCQ question, **When** defining answers, **Then** the Admin can add multiple choices and mark exactly one (or more) as the correct answer.
3. **Given** an Essay question, **When** defining it, **Then** the Admin only provides the question text and maximum points without predefined options.

---

### User Story 3 - Target Attachment Level (Lesson vs. Video) (Priority: P2)

As an administrator, I want to specify whether the exam belongs to the entire lesson or is a "pop-quiz" tied specifically to a single video within that lesson.

**Why this priority**: It enables granular assessments (e.g., testing understanding immediately after a video segment) vs. comprehensive lesson assessments.

**Independent Test**: Can be fully tested by creating an exam and selecting "Attach to Video" and picking a video from the list, or "Attach to Lesson", and seeing the exam appear under the correct target in the UI.

**Acceptance Scenarios**:

1. **Given** the exam creation flow, **When** choosing the exam target, **Then** the Admin can select "Whole Lesson" or "Specific Video".
2. **Given** the selection of "Specific Video", **When** defining the target, **Then** a dropdown of videos currently attached to this lesson is shown to pick from.

---

### Edge Cases

- What happens if the Admin attaches an exam to a video, but later deletes that video? (e.g., Should the exam revert to lesson-level or be deleted?)
- How does the system handle an MCQ question where the Admin forgets to mark any option as 'correct'?
- What happens to students who already took an exam if the Admin changes the correct answer for an MCQ later?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow admins to create and edit exams completely inline within the Lesson Cockpit interface.
- **FR-002**: System MUST remove or deprecate standalone exam creation interfaces from other parts of the admin dashboard to enforce the centralized workflow.
- **FR-003**: System MUST support adding multiple questions to a single exam.
- **FR-004**: System MUST allow each question to be typed as either "MCQ" (Multiple Choice) or "Essay".
- **FR-005**: For MCQ questions, System MUST allow admins to add text options and boolean flags to designate the correct answer(s).
- **FR-006**: System MUST allow the admin to attach the exam either to the global Lesson entity or to a specific Video entity associated with the lesson.
- **FR-007**: System MUST validate that at least one correct option is selected before saving an MCQ question.

### Key Entities *(include if feature involves data)*

- **Exam**: The aggregate root representing the assessment, containing questions.
- **Question**: Represents a single prompt within the exam. Attributes include `Type` (MCQ, Essay), `Text`, `Order`, `Points`.
- **Option**: Represents an answer choice for MCQ questions. Attributes include `Text`, `IsCorrect`.
- **ExamTarget**: A relation linking an Exam either to a `LessonId` or a `LessonVideoId`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Exam creation time is reduced by 50% since admins no longer navigate away from the Lesson Cockpit to build assessments.
- **SC-002**: Admins can successfully build a 5-question exam (mixed formatting) within 3 minutes using the inline cockpit tools.
- **SC-003**: 100% of newly created exams have a clearly enforced target scope (Lesson-level vs. Video-level).
- **SC-004**: Zero orphaned exams are created (all exams are strictly bound to a lesson or video at the point of creation).
