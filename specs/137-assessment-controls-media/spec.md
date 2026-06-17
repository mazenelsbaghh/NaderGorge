# Feature Specification: Assessment Controls And Question Media

**Feature Branch**: `137-assessment-controls-media`  
**Created**: 2026-06-17  
**Status**: Draft  
**Input**: User description: "عايز احدد ان الواجب يبقي الزامي ولا لا اللي هو يحل الواجب لازم ولا عادي و كذلك الامتحان و امتحان البارت. وعايز ف الايدير ينفع ارفع صوره مع السوال. وعايز ف عرض السوال بحط <P> و كده عايز اشيلهم"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configure Mandatory Assessments (Priority: P1)

As an admin or teacher creating assessment content, I can explicitly choose whether each homework, lesson exam, or video part exam is mandatory so progression locks only apply to mandatory assessments.

**Why this priority**: This controls student access and directly affects whether students are blocked from the next lesson.

**Independent Test**: Create one optional homework, one optional lesson exam, and one optional video exam, then verify the next lesson is not blocked by any of them.

**Acceptance Scenarios**:

1. **Given** an admin creates a homework with mandatory disabled, **When** a student opens the next lesson, **Then** the homework does not block progression.
2. **Given** an admin creates a lesson exam with mandatory enabled, **When** a student has not passed it, **Then** the next lesson remains locked with an exam-specific reason.
3. **Given** an admin creates a video part exam with mandatory disabled, **When** the student finishes the video without passing that exam, **Then** later lessons are still accessible if other mandatory requirements are satisfied.

---

### User Story 2 - Attach Question Images (Priority: P2)

As an admin or teacher editing questions, I can upload an image for a question and students can see that image when answering or reviewing the question.

**Why this priority**: Some questions require diagrams, screenshots, maps, or visual references that cannot be expressed cleanly in text.

**Independent Test**: Add a question with an uploaded image to an exam and to a homework, then verify the image appears in the student attempt and result review.

**Acceptance Scenarios**:

1. **Given** the question editor is open, **When** the admin uploads a valid image, **Then** the editor stores a question image URL and shows a preview with a remove option.
2. **Given** a question has an image, **When** the student starts an exam or homework, **Then** the image appears below the question text with descriptive alt text.
3. **Given** a question image exists, **When** the student reviews results, **Then** the image is still visible with the related question.

---

### User Story 3 - Clean Question Text Display (Priority: P3)

As a student, I see question text without raw editor tags like `<p>` or `</p>` while preserving readable line breaks and safe formatting.

**Why this priority**: Raw tags make the learning interface look broken and confuse students.

**Independent Test**: Save a question through the rich text editor and verify all student/admin display surfaces show readable text, not literal HTML tags.

**Acceptance Scenarios**:

1. **Given** a question is saved from the rich text editor, **When** it is displayed in the student exam attempt, **Then** no raw `<p>` tag text is visible.
2. **Given** a question is shown in homework attempt and review screens, **When** the stored text contains HTML paragraph tags, **Then** the text is rendered or converted safely without literal tags.
3. **Given** option text contains accidental HTML tags, **When** options are displayed, **Then** students see clean answer text.

### Edge Cases

- Optional assessments must still be available for students to solve; they only stop blocking progression.
- Existing assessments without an explicit mandatory value keep their current default behavior, mandatory by default.
- Image upload must reject empty files and non-image files.
- If a question image URL is missing or fails to load, the question text and answer controls remain usable.
- Rich text cleanup must not execute scripts or unsafe HTML.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin, `/admin/content/lessons/{id}`, create homework with "إلزامي" disabled, add a question image, save, and verify the attached homework dashboard shows the question.
- **Manual QA Role/Flow 2**: Admin, same lesson page, create a lesson exam and a video part exam with mandatory on/off, then verify the selected state is clear before save.
- **Manual QA Role/Flow 3**: Student, start the created exam/homework and verify the question image appears and raw `<p>` tags do not appear.
- **Manual QA Negative Check**: Uploading a non-image file in the question editor must show an error and must not populate the image URL.
- **Docker Acceptance**: Backend and frontend containers build, API health endpoint responds, migrations apply, and the student/admin surfaces load without console errors.
- **External Dependencies**: Requires local authentication data and at least one lesson with a video for full video part exam validation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a mandatory/optional control when creating homework.
- **FR-002**: The system MUST expose a mandatory/optional control when creating lesson-level exams.
- **FR-003**: The system MUST expose a mandatory/optional control when creating video part exams.
- **FR-004**: The system MUST persist the mandatory setting for homework and exams.
- **FR-005**: The system MUST apply lesson progression locks only for mandatory homework and mandatory exams.
- **FR-006**: The system MUST allow admins and teachers to upload an image for each question in the shared question editor.
- **FR-007**: The system MUST persist an optional image URL for exam questions and homework questions.
- **FR-008**: The system MUST return question image URLs in student attempt and result review responses.
- **FR-009**: The system MUST display question images in student exam and homework attempt screens.
- **FR-010**: The system MUST display question images in student exam and homework result review screens.
- **FR-011**: The system MUST strip or safely render rich text so raw tags such as `<p>` are never visible to students.
- **FR-012**: The system MUST validate question image uploads as image files and store them under the existing assets-domain upload/static file mechanism.
- **FR-013**: The question editor MUST provide image preview and remove controls without disrupting audio upload.

### Key Entities *(include if feature involves data)*

- **Exam**: Assessment attached to a lesson or lesson video; includes mandatory setting that controls progression enforcement.
- **Homework**: Lesson homework; includes mandatory setting that controls progression enforcement.
- **QuestionBankItem**: Exam question source; gains optional question image URL.
- **HomeworkQuestion**: Homework question; gains optional question image URL.
- **Student Attempt/Review DTOs**: Student-facing question payloads; include sanitized display text and optional question image URL.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can create mandatory or optional homework, lesson exams, and video exams in under 2 minutes per assessment.
- **SC-002**: 100% of optional assessments do not block progression in the lesson list and start flows.
- **SC-003**: 100% of uploaded valid question images are visible in both attempt and review screens.
- **SC-004**: Raw `<p>` or `</p>` strings are absent from all student-facing question text in exam and homework attempt/review screens.
- **SC-005**: Invalid question image uploads are rejected before save with a clear error message.

## Assumptions

- Existing `IsMandatory` fields on homework and exams remain the source of truth.
- Question images are optional and stored as URL strings that resolve through the production assets domain.
- The shared admin `QuestionEditor` is the right place to add image upload because it is reused by exam and homework flows.
- Student text display should preserve safe formatting where appropriate, but visible raw tags are never acceptable.
