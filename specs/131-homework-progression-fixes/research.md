# Research: Progression Locking and Homework Layout

## Findings

### 1. Progression Lock Query
The query for determining the `previousLesson` to check locks currently behaves as follows:
```csharp
var previousLesson = await _db.Lessons
    .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order < lesson.Order)
    .OrderByDescending(l => l.Order)
    .FirstOrDefaultAsync(ct);

if (previousLesson == null)
{
    var previousSection = await _db.ContentSections
        .Where(s => s.TermId == lesson.ContentSection.TermId && s.Order < lesson.ContentSection.Order)
        .OrderByDescending(s => s.Order)
        .FirstOrDefaultAsync(ct);

    if (previousSection != null)
    {
        previousLesson = await _db.Lessons
            .Where(l => l.ContentSectionId == previousSection.Id)
            .OrderByDescending(l => l.Order)
            .FirstOrDefaultAsync(ct);
    }
}
```
If we remove the `if (previousLesson == null)` fallback block, progression requirements (mandatory exam/homework check) will only apply to lessons that have `ContentSectionId` matching the current lesson's `ContentSectionId` and whose `Order` is less than the current lesson's `Order`.
This completely isolates progression locking to a single section.

### 2. Homework Layout
In `LessonViewer.tsx`, the block:
```typescript
{lesson.homework && (
  <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 shadow-sm lg:p-10">
    ...
  </div>
)}
```
renders the multistepped question form inline. Removing this ensures that the homework is solved only via the dedicated route `/student/homework/[homeworkId]?packageId=[packageId]` which is linked via the `HomeworkButton` inside the `LessonCarousel` component.
This resolves the user's issue with having the homework solver show up on the lesson details page itself.

## Decisions
- **Decision 1**: Remove fallback block in `GetLessonDetailQueryHandler` (backend).
- **Decision 2**: Remove fallback block in `GetLessonsQuery` (backend).
- **Decision 3**: Remove the interactive homework solver rendering from `LessonViewer.tsx` (frontend).
