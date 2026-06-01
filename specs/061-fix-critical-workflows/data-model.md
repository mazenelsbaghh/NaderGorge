# Data Model: Restore Critical Learning Workflows

## 1. Community Comment Moderation Record

### Purpose

Represents a student comment attached to a community post together with the moderation information needed to control visibility.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | UUID | Yes | Existing comment identifier |
| `PostId` | UUID | Yes | Parent community post |
| `AuthorUserId` | UUID | Yes | Student author |
| `Body` | Text | Yes | Comment content |
| `CreatedAt` | DateTime | Yes | Existing creation timestamp |
| `Status` | Enum | Yes | `Pending`, `Approved`, `Rejected` |
| `RejectionReason` | Text | No | Present only when a moderator rejects the comment |
| `ModeratedByUserId` | UUID | No | Recommended if an existing moderation audit pattern already exists |
| `ModeratedAt` | DateTime | No | Recommended if an existing moderation audit pattern already exists |

### Validation Rules

- `Body` must remain subject to the current max-length and non-empty validation rules.
- New comments default to `Pending`.
- `RejectionReason` may be empty on approval and optional on rejection unless product policy later requires a reason.
- Student-facing listings may only return comments with `Status = Approved`.

### Relationships

- Many comments belong to one community post.
- Many comments belong to one authoring user.
- A comment may optionally reference one moderator user if that audit detail is persisted.

### State Transitions

| Current State | Action | Next State |
|---------------|--------|------------|
| `Pending` | Admin approves | `Approved` |
| `Pending` | Admin rejects | `Rejected` |
| `Approved` | Student read | `Approved` |
| `Rejected` | Student read | Not returned to student-facing queries |

## 2. Find-The-Mistake Submission

### Purpose

Represents the student's answer payload and evaluation data for a question whose correct answer is a specific mistake selection rather than a traditional option choice.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `ExamQuestionId` | UUID | Yes | Target exam question |
| `SelectedMistakeIndex` | Integer | Conditionally | Primary answer representation when the question stores an index |
| `SelectedText` | Text | Conditionally | Fallback answer representation when the question stores text |
| `IsCorrect` | Boolean | Yes | Derived during submission grading |
| `PointsAwarded` | Decimal | Yes | Derived from correctness |

### Validation Rules

- At least one supported answer representation must be provided for find-the-mistake questions.
- The submission payload must not be graded with multiple-choice logic.
- If both representations are present, the canonical backend comparison order must be deterministic and documented in the implementation.

### Relationships

- One submission maps to one exam question within one exam attempt.
- Review output for this question type is derived from the canonical answer payload rather than an option lookup.

## 3. Essay Submission

### Purpose

Tracks the full grading lifecycle for a student's essay answer from initial submission through AI scoring and teacher finalization.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | UUID | Yes | Existing essay submission identifier |
| `StudentId` | UUID | Yes | Authoring student |
| `QuestionId` | UUID | Yes | Essay question |
| `StudentExamAttemptId` | UUID | Yes | Parent exam attempt |
| `AnswerText` | Text | Yes | Existing written answer |
| `AiInitialScore` | Decimal | No | Populated when AI scoring completes |
| `AiFeedback` | Text | No | Populated when AI scoring completes |
| `TeacherFinalScore` | Decimal | No | Populated when teacher grading completes |
| `TeacherFeedback` | Text | No | Optional final teacher notes |
| `Status` | Enum | Yes | `WaitAI`, `AIScored`, `WaitTeacher`, `TeacherGraded` |

### Validation Rules

- New essay submissions default to `WaitAI`.
- AI callback may populate AI score and feedback only for existing submissions.
- Teacher grading is allowed only after the submission reaches the teacher-ready stage.
- Final exam recalculation must use teacher score when present; otherwise the attempt remains partial or pending.

### Relationships

- Many essay submissions belong to one student exam attempt.
- Each essay submission belongs to one question and one student.

### State Transitions

| Current State | Action | Next State |
|---------------|--------|------------|
| `WaitAI` | AI callback stores result | `AIScored` |
| `AIScored` | Submission becomes available to teacher workflow | `WaitTeacher` |
| `WaitTeacher` | Teacher grades submission | `TeacherGraded` |
| `TeacherGraded` | Teacher reopens or overwrites grade | `TeacherGraded` (idempotent update only if business rules permit) |

## 4. Exam Attempt Result State

### Purpose

Represents the aggregate visibility of an exam attempt while essay answers are still moving through grading.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `AttemptId` | UUID | Yes | Exam attempt identifier |
| `ScoreAchieved` | Decimal | Yes | Current calculated score |
| `TotalScore` | Decimal | Yes | Total possible score |
| `ResultState` | Enum/String | Yes | `Pending`, `PartiallyGraded`, or `Completed` |
| `IsPassed` | Boolean | Conditional | Final only when grading is complete or when policy allows provisional pass/fail |
| `EssayStatuses` | Collection | No | Returned by grading-status contract for visibility |

### Validation Rules

- Attempts with at least one essay not fully teacher graded must not be exposed as final.
- Result builders and latest-result queries must use the same aggregate-state rules.

### Relationships

- One attempt has zero or more essay submissions.
- One attempt result state is derived from all answers attached to that attempt.
