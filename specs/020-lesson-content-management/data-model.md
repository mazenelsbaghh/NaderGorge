# Data Model: Lesson Content Management

## Core Entities (Pre-existing in EF Core)

1. **Lesson** (Domain/Entities/ContentEntities.cs)
   - Handles the main hierarchy.
   - Contains: `Id`, `Title`, `Order`, `ExamId` (Nullable foreign key), `ICollection<LessonVideo>`, `ICollection<LessonResource>`.

2. **LessonVideo** (Domain/Entities/ContentEntities.cs)
   - Represents linked videos.
   - Core fields: `Title`, `Provider`, `ProviderVideoId`, `Order`, `MaxWatchCount`.

3. **LessonResource** (Domain/Entities/ContentEntities.cs)
   - Represents attached files/documents.
   - Core fields: `Title`, `FileUrl`, `ResourceType`, `LessonId`.

4. **Homework** (Domain/Entities/Homework/Homework.cs)
   - Represents an assignment/homework.
   - Core fields: `Id`, `LessonId`, `Title`, `Description`, `IsMandatory`, `PassingScoreThreshold`.

**Note:** No complex EF migration is required because all these entities and relationships are already modeled in the codebase. Only command updates are necessary to allow CRUD on these entities via the new Cockpit UI.
