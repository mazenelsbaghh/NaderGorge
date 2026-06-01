# Feature Specification: Exam and Question UI Enhancements

**Feature Branch**: `055-exam-qs-enhancements`  
**Created**: 2026-04-04  
**Status**: Draft  
**Input**: User description: "تظبيط باقي شكل الامتحان والأسئلة و الواجب (إضافة فويس كولكشن و تصحيح كتابي لكل سوال بتضيفهم وان بضيف لكل سوال، تصحيح الأسئلة المقالية، إضافة وسائل مساعدة، نوع سوال اسمو اكتشف الغلطه يكون فيه غلطه ف حدث وهو يكتشفوا)."

## Clarifications
### Session 2026-04-04
- Q: هل سيتم تصحيح الأسئلة المقالية يدوياً أم باستخدام الذكاء الاصطناعي؟ → A: تصحيح آلي (AI) كمرحلة أولية مع إمكانية تعديل واعتماد المعلم.
- Q: ما هي طريقة تفاعل الطالب مع سؤال "اكتشف الغلطة"؟ → A: التحديد/الضغط المباشر على الكلمة (Option A).
- Q: هل يترتب على استخدام المشهد المساعد (Hint) أي خصم في الدرجات؟ → A: وسائل المساعدة مجانية للاستخدام دائماً (Option A).
- Q: هل يوجد حد أقصى للتسجيل الصوتي للمدرس؟ → A: لا يوجد حد أقصى أو قيود برمجية محددة (Option B).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Voice & Written Explanations for Questions (Priority: P1)

Teachers must be able to attach audio explanations (voice collections) and written text explanations to any question so that students can understand the correct answers better after submission.

**Why this priority**: Essential learning feedback mechanism that significantly improves the educational value of exams and homework.
**Independent Test**: Can be fully tested by creating a question, attaching an audio file and text, taking the exam as a student, and reviewing the results to see the provided explanations.

**Acceptance Scenarios**:

1. **Given** a teacher is creating/editing an exam or homework question, **When** they choose to add an explanation, **Then** they can upload an audio file and input text.
2. **Given** a student is reviewing their submitted exam/homework, **When** they look at a completed question, **Then** they can read the written explanation and play the attached audio file.

---

### User Story 2 - Essay Question Correction (Priority: P1)

Teachers must be able to review, correct, and grade essay questions submitted by students.

**Why this priority**: Without this, essay questions cannot be properly graded, rendering them useless in assessments.
**Independent Test**: Can be fully tested by submitting an essay answer as a student and having a teacher grade it and provide feedback.

**Acceptance Scenarios**:

1. **Given** a student has submitted an exam with an essay question, **When** the teacher accesses the submissions dashboard, **Then** they can view the student's text answer.
2. **Given** a teacher is reviewing an essay question, **When** they add a grade and written feedback, **Then** the student's final score is updated and they can view the teacher's feedback.

---

### User Story 3 - Add Question Hints/Aids (Priority: P2)

Teachers must be able to attach hints/aids to questions that students can use while taking the exam/homework.

**Why this priority**: Helps students who are stuck during practice or homework without immediately giving away the answer.
**Independent Test**: Can be fully tested by adding a hint to a question and verifying a student can reveal it during the test.

**Acceptance Scenarios**:

1. **Given** a question with an attached hint, **When** a student views the question during an assessment, **Then** they see an option/button to reveal the hint.
2. **Given** a student clicks to reveal a hint, **When** the action is processed, **Then** the hint text/media is displayed to the student.

---

### User Story 4 - "Find the Mistake" Question Type (Priority: P2)

Teachers must be able to create a new type of question where an event or text contains a deliberate error that the student must identify.

**Why this priority**: Diversifies the assessment types, promoting critical thinking and closer reading of the provided scenarios.
**Independent Test**: Can be fully tested by creating this specific question type and having a student locate and identify the error to earn a point.

**Acceptance Scenarios**:

1. **Given** a teacher is selecting a question type, **When** they choose "Find the Mistake", **Then** they can input a context/text and highlight the specific segment that is the error.
2. **Given** a student is taking a "Find the Mistake" question, **When** they select the incorrect segment in the text, **Then** the system records it as the correct answer.

### Edge Cases

- What happens when an audio upload fails (e.g., unsupported format, size limit exceeded)?
- How does the system handle students requesting a hint during a strict/timed exam where hints might be disallowed?
- What happens if a teacher updates the written correction or hint after students have already submitted their answers?
- How is the "Find the Mistake" interaction handled on small mobile screens where text selection might be clumsy?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow instructors to upload an audio file (voice note) alongside any question without restrictive size limits.
- **FR-002**: System MUST allow instructors to add rich text feedback/correction alongside any question.
- **FR-003**: System MUST display the written correction and audio playback to students during the review phase of their submission.
- **FR-004**: System MUST automatically grade and provide initial feedback on essay-type answer submissions using AI, and provide an interface for instructors to review, adjust, and approve the final grade.
- **FR-005**: System MUST allow instructors to define hints (text or media) for questions.
- **FR-006**: System MUST allow students to reveal hints during practice/assessments without incurring any penalty or point deduction.
- **FR-007**: System MUST support a new question type: "Find the Mistake" where instructors define a text block and define a specific sub-string or region as the "mistake".
- **FR-008**: System MUST allow students to interact with the "Find the Mistake" question by selecting the incorrect part of the provided text.

### Key Entities

- **Question**: Extended to support `audioUrl`, `writtenCorrection`, `hintText`.
- **EssaySubmission**: Represents a student's answer to an essay question, including `teacherScore` and `teacherFeedback`.
- **FindTheMistakeQuestion**: Represents a specific question configuration containing the base text, and the start/end indices of the mistake.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Teachers can successfully attach audio and written feedback to questions.
- **SC-002**: Students can play audio feedback and read written corrections on their graded assessments 100% of the time.
- **SC-003**: Instructors can review and assign scores to submitted essay questions, updating the student's exam grade accordingly.
- **SC-004**: Students can interactively highlight/select text in the "Find the Mistake" questions on both mobile and desktop.

## Assumptions

- Standard audio formats (mp3, wav, etc.) will be supported for the voice collection.
- Essay question grading will use AI for initial correction and feedback, while teachers retain final approval authority.
- The "Find the Mistake" question requires text-based interactions (e.g., selecting a word or phrase) rather than an image hotspot.
- Uploads (audio) will use the existing project storage infrastructure (e.g., S3/Cloud storage).
