# Research: Live Support AI Refinements and Performance Dashboard

## Decisions and Rationale

### 1. Unique Key Constraint Violation in Policy Publishing
- **Decision**: Perform a sequential update by calling `SaveChangesAsync` immediately after setting the old policy's `IsEnabled = false` and before enabling the new policy.
- **Rationale**: EF Core batches updates in transactions. When swapping boolean values on a unique filtered index (like `IsEnabled = true` where `Status = Published`), EF Core may order the `IsEnabled = true` update statement before the `IsEnabled = false` statement. By forcing an intermediate save, we guarantee the database state clears the unique index constraint before the new policy is enabled.
- **Alternatives Considered**: 
  - Dropping the unique index: Rejected because we want to guarantee that at most one policy version is active at any time.
  - Using a raw SQL command: Rejected because sequential EF Core saves are cleaner and safer.

### 2. Time-Period Preset Filtering
- **Decision**: Define an enum or string parameter for pre-defined ranges: `last-24h`, `last-7d`, `last-30d`, `lifetime`.
- **Rationale**: Users preferred quick, predefined filters over a complex date picker. We map each option to a UTC timestamp offset:
  - `last-24h`: `DateTime.UtcNow.AddDays(-1)`
  - `last-7d`: `DateTime.UtcNow.AddDays(-7)`
  - `last-30d`: `DateTime.UtcNow.AddDays(-30)`
  - `lifetime`: `null` (no date filtering)

### 3. Metric Calculations
- **Active Conversations**:
  - Query: `db.LiveSupportAIConversationStates.CountAsync(x => x.Mode == LiveSupportAIMode.AiActive)`
  - Rationale: A snapshot metric representing currently running conversations.
- **Resolved Issues**:
  - Query: `db.LiveSupportAIConversationStates.CountAsync(x => x.Mode == LiveSupportAIMode.AiResolved && x.ResolvedAt >= threshold)`
- **Handoffs to Staff**:
  - Query: `db.LiveSupportAIConversationStates.CountAsync(x => x.HandedOffAt != null && x.HandedOffAt >= threshold)`
- **Total Messages Sent by AI**:
  - Query: `db.LiveSupportMessages.CountAsync(x => x.SenderType == LiveSupportSenderType.AI && x.SentAt >= threshold)`
- **Successful Actions**:
  - Query: `db.LiveSupportAIPendingActions.CountAsync(x => x.Status == LiveSupportAIPendingActionStatus.Succeeded && x.CompletedAt >= threshold)`

## UI Layout and Animation
- **Tabs**: Add a standard Next.js admin page tab bar with two tabs:
  - "الإعدادات وقاعدة القرار" (Settings)
  - "الإحصائيات والأداء" (Performance Stats)
- **Active Badge**: A glowing green pulse indicator next to "حالة المساعد" when enabled:
  - Style: Custom CSS pulse animation or tailwind `animate-pulse` with a bright green ring.
