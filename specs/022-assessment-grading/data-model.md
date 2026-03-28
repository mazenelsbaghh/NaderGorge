# Data Model: Assessment Grading

## Modified Entities

### 1. `Exam`
- **Fields Added**:
  - `TotalScore` (decimal): Explicit total score possible on this exam.
  - `PassingScore` (decimal): Explicit score required to pass.
- **Rules**:
  - `PassingScore <= TotalScore`.

### 2. `Homework` (in `NaderGorge.Domain.Entities.Homework`)
- **Fields Already Present**:
  - `PassingScoreThreshold` (int/decimal).
- **Fields Added**:
  - `TotalScore` (decimal): Explicit maximum points.
  - Update `PassingScoreThreshold` semantic to represent an absolute value rather than a points threshold if necessary, and ensure consistency.

### 3. `StudentExamResult`
- **Fields Added**:
  - `EarnedScore` (decimal): Scaled final points achieved by the student.
  - `Evaluation` (string): Textual grading representation ("ممتاز", "جيد جداً", etc.)
- **Rules**:
  - `Evaluation` is populated at creation (on submission).

### 4. `StudentHomeworkResult`
- **Fields**:
  - `EarnedScore` (decimal).
  - `Evaluation` (string): Output grading scale string matching Exam scales.
