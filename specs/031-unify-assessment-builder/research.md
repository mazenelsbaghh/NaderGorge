# Research Notes: Unified Assessment Builder

This document resolves technical strategy and architectural decisions for unifying the Exam and Homework builder UI and backend implementations.

## Investigation Areas

### 1. Unified Frontend Implementation
- **Issue**: The current system has `InlineExamEditor.tsx` and `AddHomeworkForm.tsx` as two completely separate components. `AddHomeworkForm` is basic, while `InlineExamEditor` has robust question building functionality (as evidenced by the provided screenshot showing points, time per question, etc.).
- **Decision**: Refactor `InlineExamEditor.tsx` into a new shared component `UnifiedAssessmentBuilder.tsx`. This single component will accept a `type` prop (`'exam' | 'homework'`) to conditionally adjust any specific labels or API payloads.
- **Rationale**: Reduces code duplication significantly and ensures any future improvements to question building (e.g., auto-grading logic, formula support, rich text) benefit both homework and exams simultaneously.
- **Alternatives considered**: Keeping them separate and manually syncing code (rejected due to high maintenance overhead).

### 2. Randomization & Mandatory Flags in the Domain
- **Issue**: Need to add `IsRandomized` to assessments. Need to ensure `IsMandatory` exists and works correctly.
- **Current State**: `Homework` currently has `IsMandatory`. `Exam` does not have `IsMandatory` at the entity level, although `Lesson` logic checks exams for progression. Neither has `IsRandomized`.
- **Decision**: Add `IsRandomized` boolean directly to both `Homework` and `Exam` domain entities. Add `IsMandatory` to `Exam` domain entity to match `Homework`, allowing explicit control instead of implicit progression locking.
- **Rationale**: Keeping flags natively on the assessment tables directly matches the new UI toggles and avoids complex mapping.

### 3. API Contract Strategy
- **Issue**: Do we create a single `/api/Assessments/` endpoint or keep `/api/Admin/homework` and `/api/Admin/exams` separate?
- **Decision**: Keep the individual POST/PUT endpoints for Exam and Homework separately at the backend (`CreateExamCommand`, `CreateHomeworkCommand`) because their underlying database relationships (`ExamQuestions` vs `HomeworkQuestions`, `QuestionBankItem` dependencies) differ fundamentally in the database schema. The frontend `UnifiedAssessmentBuilder` will internally route the payload to the correct endpoint based on the `type` prop.
- **Rationale**: Changing the entire backend entity inheritance scheme would require a massive database migration and break all existing reporting/analytics queries. Unifying at the frontend layer achieves the user experience goal without destroying backend data integrity.

### 4. User Experience & Toggling
- **Issue**: Where do the toggles go in the UI shown in the image?
- **Decision**: Add a new "Settings" (الإعدادات) panel inside the existing UI (perhaps below the "Basic Settings" / إعدادات الامتحان الأساسية) that houses two switches:
  1. $\toggle$ أسئلة عشوائية (Randomize Questions)
  2. $\toggle$ التقييم إلزامي للتقدم (Mandatory to unlock next lesson)
- **Rationale**: Fits cleanly into the existing layout without overwhelming the user.

## Conclusion

All technical unknowns are resolved. The plan moves forward with creating a unified React component `UnifiedAssessmentBuilder` while extending the existing `Homework` and `Exam` domain entities with `IsRandomized` and `IsMandatory` flags. The frontend will abstract the differences from the user.
