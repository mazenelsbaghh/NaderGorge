# Quickstart: Assessment Grading

## Steps for Developers

1. **Backend Database Migrations**:
   Run the EF core migration command to build the new properties (`TotalScore` for Exams and Homeworks).
   ```bash
   dotnet ef migrations add AssessmentGradingUpdate --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API
   dotnet ef database update --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API
   ```

2. **Backend Logic Extensions**:
   Update `AdminExamCommands.cs` and `AdminContentCommands.cs` to map `TotalScore` gracefully. Make sure the logic prevents any passing score greater than the total score.

3. **Frontend Editor Overhaul**:
   Inside `frontend/src/components/admin/InlineExamEditor.tsx` and `AddHomeworkForm.tsx`, ensure there are inputs for **Total Score** (الدرجة النهائية). Add UI validation checking the relationship with `passingScore`.

4. **Results Evaluation**:
   Whenever a Student's Assessment Submit command is implemented, calculate the scaled score:
   `ScaledScore = (SumOfEarnedPoints / SumOfAvailablePoints) * AssessmentTotalScore`.
   Then match against the specific enum thresholds to yield "ممتاز", "جيد جداً", "جيد", "مقبول", "ضعيف".
