# Data Model and Query Projections: 116-performance-deep-remediation

No new database tables or columns are introduced. However, we are optimizing how queries are projected and how relationships are handled.

## CodeGroup & AccessCode Projections

Currently, listing Code Groups includes all access codes, which is a major performance bottleneck:
```csharp
// Current (slow)
var groups = await _context.CodeGroups
    .Include(cg => cg.AccessCodes)
    .ToListAsync();
```

Optimized projection:
```csharp
// Optimized (fast)
var groups = await _context.CodeGroups
    .AsNoTracking()
    .Select(cg => new CodeGroupDto
    {
        Id = cg.Id,
        Name = cg.Name,
        CodeCount = cg.AccessCodes.Count(),
        UsedCount = cg.AccessCodes.Count(c => c.IsConsumed),
        // Other primitive properties...
    })
    .ToListAsync();
```

## Student Dashboard Query

Currently, dashboard retrieval does deep includes of terms, sections, and lessons, causing full object graphs to load.
We will change this to retrieve summary counts and active study positions via direct SQL aggregates or flat queries:
- Total lessons count: query `_context.Lessons.Count(l => l.Section.Term.PackageId == packageId)`.
- Completed lessons: query count from `LessonProgress`.
- Current position: single query to find the first uncompleted lesson.
- Upcoming exams: select exams targeting the package lessons and query them in a single `.Where(e => ...).ToListAsync()` instead of checking each in a loop.
