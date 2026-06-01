# Feature Specification: Lesson Progression Stepper

**Feature Branch**: `029-lesson-progression-stepper`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "خلي عندي زرار وانا بعمل امتحان انو يجي عشواي يعني لو ١٠ اسئله ترتيب ال ١٠ ائله لكل حد هيفتح يبقي مختلف عن التاني. و ترتيب الاجابات. ويبقي الزامي يعني انو ماينفعش يفتح الفيدو او الحصه غير لمي يحصل و ان يبقي فيه سقوط و نجاح ولا لا عادي. واننا هنخلي الواجب هو هو الامتجان يعني بيتحل و كل ده و ان الحصه اللي بعدها ماينفعش تتفتح غير لمي يحل الواجب الحصه قبلها و يحل الامتحان بتاع الحصه ذات نفسها. واعمل ده shared compoonts علاشن نتسخدم ف المتحان و في الواجب و عايزوا هو هو بنفس الانيمشن بتاعتوا بس بهويتنا" + React Bits Stepper code.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sequential Lesson Unlocking (Priority: P1)

As a student, I must complete the homework and exam of the current lesson before I can access the video or content of the next lesson, ensuring I master the material sequentially.

**Why this priority**: Enforcing prerequisites is the core objective of the progression system. Without this, students can skip critical evaluations.

**Independent Test**: Can be independently tested by attempting to open a locked lesson without completing the prior lesson's homework and exam. The system should block access and prompt completion.

**Acceptance Scenarios**:

1. **Given** a student has not completed Lesson 1's homework or exam, **When** they try to open Lesson 2, **Then** the system blocks access and displays a message indicating the prerequisites.
2. **Given** a student has successfully passed Lesson 1's homework and exam, **When** they try to open Lesson 2, **Then** the system grants access to Lesson 2's content.

---

### User Story 2 - Randomized Assessments (Priority: P2)

As a student taking an exam or homework, I receive a randomized order of questions and answers so that my assessment experience is unique and deters cheating.

**Why this priority**: Randomization protects the integrity of the assessments, which is crucial for a reliable success/fail grading system.

**Independent Test**: Can be tested by opening the same exam with two different student accounts. The questions and their respective multiple-choice answers must appear in a uniquely shuffled order.

**Acceptance Scenarios**:

1. **Given** an exam with 10 questions, **When** Student A opens the exam, **Then** the questions are presented in a specific random sequence (e.g., Q3, Q7, Q1...).
2. **Given** the same exam, **When** Student B opens it, **Then** the questions and their answer options are presented in a different random sequence than Student A.

---

### User Story 3 - Unified Stepper Assessment Experience (Priority: P3)

As a student, I interact with both homework and exams using the same animated, step-by-step interface (Stepper) that aligns with the academy's premium brand identity, making the experience engaging and consistent.

**Why this priority**: A unified, animated UI reduces cognitive load and provides a premium feel matching the platform's editorial design system.

**Independent Test**: Can be tested by navigating through both an exam and a homework assignment. Both must use the same stepper component with fluid spring animations and identical branding.

**Acceptance Scenarios**:

1. **Given** a student starts a homework assignment, **When** they navigate between questions, **Then** they experience smooth sliding animations matching the academy's branding.
2. **Given** a student starts an exam, **When** they navigate between questions, **Then** the exact same animated stepper interface is used.

### Edge Cases

- What happens if an admin adds new questions to an exam while a student is actively taking it?
- How does the system handle progression if a lesson has no homework or exam attached to it? (Assumption: It automatically unlocks the next lesson).
- What happens if a student fails the exam? (Assumption: They must retake it until they achieve the passing score to unlock the next lesson).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST mandate the successful completion (passing score) of both the homework and exam of Lesson N before granting access to Lesson N+1.
- **FR-002**: The system MUST randomize the order of questions for every new assessment attempt.
- **FR-003**: The system MUST randomize the order of multiple-choice answers within each question for every new attempt.
- **FR-004**: The system MUST utilize a shared, animated Stepper component for presenting both homework and exams one question at a time.
- **FR-005**: The system MUST evaluate the student's submission against a defined passing criteria (success/fail) to determine if prerequisites are met.
- **FR-006**: The system MUST clearly indicate locked lessons in the UI and communicate the required actions to unlock them.

### Assumptions

- Passing thresholds for exams and homework are defined by the admin (e.g., 50% or 100%). If not set, completing the assessment yields an automatic pass.
- Lessons without attached homework or exams are considered "completed" once the video is watched or marked done.

### Key Entities

- **AssessmentAttempt**: Tracks a student's progress, randomized sequence of questions/answers, and final score for a specific homework or exam.
- **LessonCompletion**: A record indicating whether a student has satisfied all prerequisites (homework + exam) for a specific lesson.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of students are blocked from accessing a subsequent lesson if they have not passed the prior lesson's required assessments.
- **SC-002**: Two concurrent attempts of the same assessment by different users have less than a 5% chance of presenting the exact same question and answer sequence.
- **SC-003**: The shared Stepper component is reused for both Homework and Exams, achieving 0 duplication of core assessment UI logic.
- **SC-004**: Assessment navigation between steps (questions) completes animations smoothly within 0.5 seconds, enhancing user satisfaction.
