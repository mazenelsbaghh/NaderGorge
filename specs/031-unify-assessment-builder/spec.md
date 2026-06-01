# Feature Specification: Unified Assessment Builder

**Feature Branch**: `031-unify-assessment-builder`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "خلي فيه زرار  بتاع اني عايزوا عشوائي ولا لا و انو الزامي ولا لا واضافه الواجب تبقي هي اضافه الامتحان تبقي هي كل حاجه واني اضيف اسئله و كل حاجه"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Unified Builder Interface (Priority: P1)

As an administrator, I want to use a single, unified interface to create both exams and homework assignments, so that the process of adding questions and configuring settings is identical and standardized for any type of student assessment.

**Why this priority**: Unifying the builder reduces duplicated code and offers a seamless experience for admins to manage content questions. It establishes the core foundation.

**Independent Test**: Can be fully tested by opening the builder inside a lesson, selecting "Homework" or "Exam", adding questions, saving, and verifying the correct entity was created in the database.

**Acceptance Scenarios**:

1. **Given** the admin is in the lesson content manager, **When** they click to add an assessment, **Then** a unified modal/page opens that allows them to select the assessment type (Homework vs Exam).
2. **Given** the unified builder is open, **When** the admin adds questions and saves, **Then** the questions are correctly linked to the chosen assessment type without needing a separate UI flow.

---

### User Story 2 - Assessment Configuration Toggles (Priority: P2)

As an administrator, I want to be able to toggle whether an assessment's questions should be randomized and whether completing it is mandatory, so that I have fine-grained control over the student learning experience.

**Why this priority**: These settings directly dictate how the student will consume the test and whether the progression system blocks them.

**Independent Test**: Can be tested by creating an assessment, enabling randomization and mandatory flags, then verifying as a student that the questions appear in random order and the next lesson is locked until passed.

**Acceptance Scenarios**:

1. **Given** the unified assessment builder, **When** the admin creates an assessment, **Then** they see clear toggle switches for "Randomize Questions" and "Is Mandatory".
2. **Given** an active randomized assessment, **When** two different students open it, **Then** the questions and their options are presented in different orders.
3. **Given** an active non-mandatory assessment, **When** a student ignores it, **Then** they can still access the subsequent lesson content.

---

### Edge Cases

- What happens when a previously mandatory assessment is toggled to non-mandatory while students are in the middle of a course? (Progression locks should dynamically unlock for those students).
- What happens if an admin toggles "Randomize Questions" on an assessment that relies on an ordered sequence of questions? (The admin must be aware that randomization applies purely randomly without respect to sequence).
- How does the system handle an inline video exam (Pop Quiz) being marked as mandatory? (It should prevent continuing the video until passed).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single unified UI interface for creating and editing assessments (Exams and Homework).
- **FR-002**: The system MUST allow the admin to select whether the assessment being built is an Exam or a Homework assignment.
- **FR-003**: The system MUST allow the admin to toggle a "Randomize Questions" setting that shuffles the order of questions and options presented to the student.
- **FR-004**: The system MUST allow the admin to toggle an "Is Mandatory" setting.
- **FR-005**: The system MUST enforce the "Is Mandatory" setting by blocking students from accessing subsequent content within the section/term until passing the assessment.
- **FR-006**: The system MUST support adding, editing, and deleting questions directly within this unified builder.

### Key Entities

- **Assessment Entity (Exam / Homework)**: Needs to support flags for `IsMandatory` and `IsRandomized`.
- **Lesson**: The content container that links to these assessments.
- **Student Exam/Homework Attempt**: Tracks individual student progress against the assessment, utilizing the randomized parameters when generating the session data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can successfully create a homework assignment and an exam using the exact same underlying builder UI without switching context.
- **SC-002**: At least 80% fewer lines of code are duplicated between the frontend UI layers of Homework creation and Exam creation.
- **SC-003**: A student opening a randomized assessment 3 times sees a different order of questions/options each iteration.
- **SC-004**: The progression system correctly respects the explicit "mandatory" flag, replacing implicit mandatory logic.
