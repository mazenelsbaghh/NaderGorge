# Data Model Updates: Exam Editor Enhancements

## Modified Entities

### 1. Exam Entity
Location: `NaderGorge.Domain.Entities.Exam`

**New Fields Added**:
- `TimePerQuestionSeconds` (int, nullable): Stores the global boundary time indicating how long a student has to answer *each* individual question in the exam.

**Schema Updates**:
Requires an EF Core Migration to alter the `Exams` table.

```csharp
public class Exam : BaseEntity
{
    // ... existing fields (Title, PassingScore, DurationMinutes)
    
    // New Timer property
    public int? TimePerQuestionSeconds { get; set; }
}
```

### 2. QuestionBankItem Entity
Location: `NaderGorge.Domain.Entities.QuestionBankItem`

**Behavioral Changes**:
- No structural column changes required. The existing `Text` column (which maps to `text` or `varchar` in PostgreSQL) natively supports HTML string payloads.
- The constraint now is that frontend apps reading this entity MUST treat `Text` as raw HTML rather than a pure literal string.

## Migration Path

1. Add explicit field to entity model.
2. Generate migration (`dotnet ef migrations add AddExamTimePerQuestionLimit`).
3. Apply migration to DB.
