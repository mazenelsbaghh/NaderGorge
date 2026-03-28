# Implementation Plan: Assessment Grading

## Summary

The goal is to enable an absolute and decoupled grading setup for all Student Assessments (Exams and Homeworks). Currently, the system builds an exam from questions, but lacks the concept of a single "Target" or "Total" score separate from the raw sum of points. This plan introduces explicit "Total Score" (الدرجة النهائية) and "Passing Score" (درجة النجاح) configurations for assessments, and adds automatic percentage-based scaling and textual evaluation generation (ممتاز, جيد جداً, الخ) upon grading.

## Technical Context

**Language/Version**: C# 12.0 / .NET 8, TypeScript (Next.js 15)  
**Primary Dependencies**: React, Tailwind CSS, Entity Framework Core, MediatR  
**Storage**: PostgreSQL  
**Testing**: XUnit (if existing) or manual endpoint tests  
**Project Type**: Web Application (Backend & Frontend pair)  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Handled via `NaderGorge.Domain` modification and CQRS.
- **Academic Content Integrity**: Grading falls inside the bounds of evaluating specific Homeworks and Exams inside a Lesson without diluting the structure.
- **Premium Design System**: Frontend additions to `InlineExamEditor` and `AddHomeworkForm` will use Tailwind classes matching the "Editorial Scholar" theme (rounded borders, neutral spacing, consistent form controls).
- All gates pass successfully.

## Proposed Changes

### Database Entities & Migrations (NaderGorge.Domain & NaderGorge.Infrastructure)
#### [MODIFY] [ExamEntities.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Domain/Entities/ExamEntities.cs)
- Add `public decimal TotalScore { get; set; }` to `Exam`.
- Add `public decimal PassingScore { get; set; }` to `Exam`.

#### [MODIFY] [HomeworkEntities.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Domain/Entities/Homework/Homework.cs)
- Note: It already has `PassingScoreThreshold`.
- Add `public decimal TotalScore { get; set; }` to `Homework`.

#### [MODIFY] [AppDbContext.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs)
- Create and apply a migration for the added fields.

---

### Application Logic (CQRS)
#### [MODIFY] [AdminExamCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs)
- Update `CreateInlineExamCommand` to accept `TotalScore` (re-routing passing score explicitly).
- Validate that `PassingScore <= TotalScore`.

#### [MODIFY] [AdminContentCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs)
- Update `AttachHomeworkCommand` to accept and set `TotalScore`.

#### [NEW] [GradingEvaluationService.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Services/GradingEvaluationService.cs)
- Implement a shared utility to calculate `EarnedScore` and textual `Evaluation`:
  - `CalculateScaledScore(decimal rawPointsEarned, decimal rawPointsPossible, decimal targetTotalScore)`
  - `DetermineEvaluation(decimal scaledScore, decimal passingScore, decimal totalScore)` yielding `ممتاز`, `جيد جداً`, `جيد`, `مقبول`, or `ضعيف`.

---

### Dashboard Frontend & Integration
#### [MODIFY] [AddHomeworkForm.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AddHomeworkForm.tsx)
- Embed the numeric inputs for "الدرجة النهائية" (Total Score) and pair it with the existing Passing score threshold logic.
- Validate `Passing <= Total`.

#### [MODIFY] [InlineExamEditor.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/InlineExamEditor.tsx)
- Expose the "Total Score" (الدرجة الكلية) explicitly. Right now it just shows the sum of question points visually, but we will make it explicit to the backend.

## Open Questions

- > [!IMPORTANT]
  > Is it acceptable to dynamically update "TotalScore" on the frontend while adding questions by defaulting it to the sum of points, but allowing the Admin to edit it manually as well? Yes, this seems the most flexible approach according to standard use cases.

## Verification Plan

### Automated/Manual Testing
- Create a test `Exam` with `TotalScore` = 100 and `PassingScore` = 50. Add 3 questions worth 5 points each.
- The exam must save correctly without validation errors.
- Create a test payload simulating a student grading calculation: score 15 points (100%), verifies logic translates it to 100 / 100 ("ممتاز").
