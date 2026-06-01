# Phase 0: Outline & Research

## Problem: DbUpdateConcurrencyException in AiAnalysisCompletedCommandHandler

### Discovery & Context
The exception `DbUpdateConcurrencyException` is thrown during `SaveChanges` inside `AiAnalysisCompletedCommandHandler`. 
When the Gemini processing is extremely fast or retried by `bullmq`, multiple webhook operations might overlap.
The current code logic:
1. Loads `LessonVideo` with `Include(v => v.VideoChapters)`
2. `video.VideoChapters.Clear()` and `_db.VideoChapters.RemoveRange(...)` are called
3. Adds new `VideoChapter` objects into the collection.
4. Calls `_db.SaveChangesAsync()`.

Because EF Core uses Optimistic Concurrency, if two webhook calls execute step 1 concurrently, the `LessonVideo` entity is tracked. One call completes step 4 successfully. The second call attempts step 4, but since the `LessonVideo` or `VideoChapters` were already modified/deleted (e.g. changing the `UpdatedAt` concurrency tokens or modifying child keys), EF Core realizes 0 rows were updated instead of 1, resulting in the concurrency exception.

### Best Practices for Idempotent EF Core Operations
- Instead of relying on Tracked Entity changes which enforce Concurrency Tokens for the entire aggregate root (`LessonVideo`), we should utilize EF Core 7+ bulk update methods (`ExecuteUpdateAsync` and `ExecuteDeleteAsync`).
- By bypassing the change tracker for simple updates, we allow the database locks (or lack thereof) to naturally process the changes without crashing the background worker.

### Decision
- **Decision**: Refactor `AiAnalysisCompletedCommandHandler` to use `ExecuteUpdateAsync` and `ExecuteDeleteAsync`.
- **Rationale**: Bulk operations translate directly to SQL `DELETE` and `UPDATE` statements safely handling concurrent executions seamlessly without causing `DbUpdateConcurrencyException`. If the operation is repeated by BullMQ, it simply re-deletes and re-inserts idempotently.
- **Alternatives considered**: 
  - *Retrying via Polly*: Rejected because it adds overhead and delays webhook responses while waiting.
  - *Transaction serializable locking (Pessimistic)*: Rejected because EF Core does not intuitively support it out of the box outside of raw SQL `SELECT FOR UPDATE`, which diminishes clean architecture.
