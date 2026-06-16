# Data Model: Audio Upload Restrictions & Review Display

## Entities & Table Structures

This feature does not require database schema migrations as it leverages existing fields.

### 1. EssaySubmission (Domain Model)
- **Table**: `EssaySubmissions`
- **Fields**:
  - `AnswerText` (`string`): The text answer submitted by the student.
  - `AudioUrl` (`string?`): The URL of the uploaded voice note answer. Tied to a specific exam question and attempt.

### 2. HomeworkAnswer (Domain Model)
- **Table**: `HomeworkAnswers`
- **Fields**:
  - `ProvidedAnswer` (`string`): Text block for essay/MCQ. In this feature, if the student uploads an audio answer for a homework essay question, the audio file URL (e.g. `/uploads/audio/...`) is stored directly as the value of `ProvidedAnswer`.

---

## Data Transfer Objects (DTOs)

### 1. ExamQuestionReviewDto (C# / TS)
- **Modifications**: Added `StudentAudioUrl` to expose the student's submitted audio answer.
- **C# Record**:
  ```csharp
  public record ExamQuestionReviewDto(
      Guid ExamQuestionId,
      int Order,
      string QuestionText,
      string? SelectedOptionText,
      bool IsAnswered,
      bool IsCorrect,
      decimal PointsAwarded,
      string? CorrectOptionText,
      string? AudioUrl,
      string? WrittenCorrection,
      string? StudentAudioUrl = null // New
  );
  ```
- **TypeScript Interface**:
  ```typescript
  export interface ExamQuestionReviewDto {
    examQuestionId: string;
    order: number;
    questionText: string;
    selectedOptionText?: string;
    isAnswered: boolean;
    isCorrect: boolean;
    pointsAwarded: number;
    correctOptionText?: string;
    audioUrl?: string;
    writtenCorrection?: string;
    studentAudioUrl?: string; // New
  }
  ```
