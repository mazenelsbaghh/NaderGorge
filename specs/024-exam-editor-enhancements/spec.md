# Feature Specification: Exam Editor Enhancements

**Feature Branch**: `024-exam-editor-enhancements`  
**Created**: 2026-03-28
**Status**: Draft  
**Input**: "وقت بالثواني للسوال دي خلها وانا بعمل الامتحجانم مش بعمل السوال وعايز اضيف text edit كده علشان لو عايز الون حاجه ف السوال و كده"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Global Per-Question Timer (Priority: P1)

As an administrator creating an exam, I want to set a "Time per question" limit for the entire exam once, so that I don't have to manually configure the timer for every single question I add.

**Why this priority**: Improves admin workflow efficiency and reduces repetitive data entry during exam creation.

**Independent Test**: Can be fully tested by creating a new exam, filling out the "Time per question" field, adding questions, and observing that the exam successfully saves and inherits the global timer.

**Acceptance Scenarios**:

1. **Given** the exam creation screen, **When** the admin is setting up the exam parameters, **Then** they see an optional input for "Time per question (Seconds)".
2. **Given** an existing exam, **When** an admin adds new questions to it, **Then** they do not need to specify individual timers for those new questions.

---

### User Story 2 - Rich Text Question Editing (Priority: P2)

As an administrator writing question text, I want to use a rich text editor so that I can specifically color certain words, use bold/italics, and format the question clearly for students.

**Why this priority**: Enhances the visual quality of the quizzes and enables complex questions where formatting (like highlighting a specific word in a grammar question) is critical context.

**Independent Test**: Can be tested by opening the "Add Question" view, typing text, applying colors and bold formatting using a toolbar, saving, and verifying the HTML output is preserved and rendered correctly.

**Acceptance Scenarios**:

1. **Given** the question editor UI, **When** the admin goes to write the question body, **Then** a rich text editor with a toolbar (bold, color, list, etc.) is presented instead of a plain textarea.
2. **Given** a formatted question is saved, **When** it is retrieved from the database and displayed to a student, **Then** it accurately retains and renders the HTML formatting tags.

### Edge Cases

- What happens to existing questions that had their own individual `DurationSeconds`? (The individual `DurationSeconds` should be migrated or fallback to the new Exam-level `TimePerQuestionSeconds`, depending on implementation choices, but future UI will rely on the Exam level entirely).
- How does the system handle rich text pasting from Word/Google Docs? (The rich text editor should sanitize input to prevent malicious scripts, keeping only safe tags).
- If an admin leaves `TimePerQuestionSeconds` empty, does the system panic? (It should simply mean the questions do not have an individual deadline, only relying on the overall Exam duration if it exists).

### Assumptions

- The frontend uses a trusted standard library for rich text editing that auto-sanitizes input.
- Database text fields (like `Text` on `QuestionBankItem`) are sufficiently large (e.g., typically `text` or `nvarchar(max)`) to hold HTML payload.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support storing a global `TimePerQuestionSeconds` value on the `Exam` entity in the database.
- **FR-002**: The `InlineExamEditor` UI MUST expose an input for `TimePerQuestionSeconds` alongside the overall `DurationMinutes`.
- **FR-003**: The individual `DurationSeconds` input on the `QuestionEditor` UI MUST be removed to enforce the exam-level constraint.
- **FR-004**: The Question Text input MUST utilize a Rich Text Editor component supporting basic HTML tags, bold, italics, underline, and text coloring.
- **FR-005**: All frontend displays reading the question text MUST safely parse and render the rich HTML content.

### Key Entities

- **Exam**: Requires a new `TimePerQuestionSeconds` (int, nullable) attribute.
- **QuestionBankItem**: The `Text` field will now store HTML instead of plain text string.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Administrators can fully format a question using colors and basic typography in under 10 seconds per question.
- **SC-002**: Exam setup requires exactly 1 timer entry for the entire exam instead of N manual text entries where N is the number of questions.
- **SC-003**: 100% of formatted questions render their internal HTML safely without breaking the container layout on mobile and desktop.
