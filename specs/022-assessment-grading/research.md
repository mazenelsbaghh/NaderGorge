# Research: Assessment Grading

## Technical Context Exploration

1. **How should `TotalScore` and `PassingScore` be persisted?**
   - **Decision**: Add them as required properties (`decimal`) in the Domain Entities `Exam` and `Homework`.
   - **Rationale**: The spec requires exact bounds for the final grade, regardless of individual sub-question scores. `TotalScore` serves as the upper bound for the scale, and `PassingScore` serves as the pass/fail threshold.
   - **Alternatives**: dynamically calculating passing score based on aggregated points. Rejected because the spec explicitly states the admin MUST set them "independently of the sum of the individual question points".

2. **How to structure the evaluation (التقدير) logic?**
   - **Decision**: We will implement the evaluation logic centrally in a dedicated Domain Service or inside the Application Layer's Command Handler (`SubmitExamCommandHandler` / `SubmitHomeworkCommandHandler`). The calculated evaluation string ("ممتاز", etc.) will be stored directly on the `StudentExamResult` and `StudentHomeworkResult` entities as a `string Evaluation`.
   - **Rationale**: Pre-calculating and saving the string is more efficient for reporting (e.g. `ParentReport` pages) than calculating it on the fly every time.
   - **Alternatives**: Adding a calculated property `[NotMapped]` on the entity. Rejected as it couples viewing to business rules logic and makes complex database querying (e.g. "Find all Excellent students") more difficult.

3. **How does the Frontend handle explicit grading limits?**
   - **Decision**: Form inputs for "Total Score" and "Passing Score" must enforce `PassingScore <= TotalScore`.
   - **Rationale**: Immediate user feedback is preferred (FR-003).

4. **Score Scaling (FR-005) Mechanism**:
   - **Decision**: Mathematical scaling during grading: `EarnedScore = (SumOfCorrectPoints / SumOfAllQuestionPoints) * TotalScore`.
   - **Rationale**: Ensures the final result fits cleanly into the admin's explicitly defined grading scale metrics.
