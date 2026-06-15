# Data Model: watch-requests-refinements-and-repurchases

## Existing Data Structures Used
- **ExtraWatchRequest**: Stores student requests for extra views. Fields used: `Status` (Approved, Rejected, Pending), `RejectionReason`, `ResolvedAt`, `LessonVideoId`, `UserId`.
- **VideoWatchEvent**: Tracks student watch statistics. Fields modified: `WatchCount` (reset to 0), `IsLocked` (unlocked / locked), `CustomMaxWatchCount` (incremented/decremented/nullified), `TimeWatchedInSeconds` (set to -1 or 0), `UpdatedAt`.
- **VideoOverride**: Logs audit trail for custom max watch count adjustments.
- **StudentAccessGrant**: Relates students to purchased content.
- **OutboxEvent**: Dispatches SignalR notifications (`ExtraWatchRequestUpdated`) to update active client video players.
