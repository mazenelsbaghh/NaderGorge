# Research Phase: Exam Editor Enhancements

## Feature Context
Moving the timer to the exam layer to act as a default for all questions, and swapping the plain text question input for a structured Rich Text HTML editor to allow coloring and styling.

## Research Objectives

### 1. Identify optimal Rich Text library for Next.js

**Decision**: Use `react-quill`.

**Rationale**:
- Mature, robust library based on Quill.js.
- Extremely easy to drop into a React application natively.
- Supports exactly what the admin needs (colors, bold, list, basic formatting) without overwhelming the UI with unnecessary complex tools.
- Output is standard HTML strings which can easily be saved into the existing `QuestionBankItem.Text` field and rendered natively by `dangerouslySetInnerHTML` in the Next.js frontend safely.

**Alternatives considered**:
- `tiptap`: Very modern, but requires more boilerplate integration to get basic features like a color picker working.
- `Slate.js`: Highly customizable but overkill for a simple "bold + color text" feature.
- Custom `contenteditable` component: Highly prone to edge case bugs globally across mobile/desktop browsers.

---

### 2. Best structure for Exam Level Timers

**Decision**: Add `TimePerQuestionSeconds` (nullable int) to the `Exam` domain entity.

**Rationale**: 
- If the exam defines the duration for every question, storing it natively on the `Exam` entity ensures any student taking the exam immediately inherits this constraint.
- This is a non-breaking additive change for Entity Framework. 
- Allows the frontend to stop capturing the timer per question.

**Alternatives considered**:
- Looping over all questions during Exam Creation and applying the number to `ExamQuestion.DurationSeconds`. 
- *Why rejected*: Modifying existing questions later (via the `/add-question` route) would accidentally forget the timer unless the admin specifically remembered it. Storing it as a master setting on the `Exam` is safer.
