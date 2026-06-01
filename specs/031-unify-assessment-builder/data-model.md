# Backend Data Model: Unified Assessment Builder

## 1. Domain Entities Modified

### `Homework`

```csharp
public class Homework : BaseEntity
{
    public Guid LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? PassingScoreThreshold { get; set; }
    public decimal TotalScore { get; set; }
    public bool IsMandatory { get; set; } = true;

    // Added Field
    public bool IsRandomized { get; set; } = false;

    public ICollection<HomeworkQuestion> Questions { get; set; } = new List<HomeworkQuestion>();
    // ...
}
```

### `Exam`

```csharp
public class Exam : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PassingScore { get; set; }
    public decimal TotalScore { get; set; }
    public int? DurationMinutes { get; set; }
    public int? TimePerQuestionSeconds { get; set; }

    // Added Fields
    public bool IsMandatory { get; set; } = true;
    public bool IsRandomized { get; set; } = false;

    // Optional relation back to lesson (though Lesson maintains ExamId currently)
    // ...
}
```

## 2. API Contract Updates

The REST APIs need to be extended to accept the new `IsRandomized` and `IsMandatory` (for Exam) variables.

### Homework Payload: `CreateHomeworkCommand` & `UpdateHomeworkCommand`

```json
{
  "lessonId": "uuid",
  "title": "استاتيكا 1",
  "description": "قوى متوازية",
  "isMandatory": true,       // Existing
  "isRandomized": true,      // New
  "passingScoreThreshold": 50,
  "totalScore": 100,
  "questions": [ ... ]
}
```

### Exam Payload: `CreateExamCommand`

```json
{
  "lessonId": "uuid",
  "title": "اختبار شهر 1",
  "description": "...",
  "durationMinutes": 60,
  "timePerQuestionSeconds": 120,
  "passingScore": 25,
  "totalScore": 50,
  "isMandatory": true,       // New
  "isRandomized": true,      // New
  "targetVideoId": null,
  "questions": [ ... ]
}
```
