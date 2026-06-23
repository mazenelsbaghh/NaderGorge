# Data Model & Query Definitions: Live Support AI Refinements

No database migrations or new tables are required for this feature. We query and modify existing tables.

## Entity Mappings

### `LiveSupportAIPolicyVersion`
We modify this table to update the `IsEnabled` status:
- `IsEnabled` (boolean): Shows if the policy is currently active. Unique filtered index exists for `Status == Published && IsEnabled == TRUE`.
- `Status` (enum `LiveSupportAIPolicyStatus`): `Draft (0)`, `Published (1)`, `Superseded (2)`.

### `LiveSupportAIConversationState`
We query this table to compute active, resolved, and handed-off counts:
- `Mode` (enum `LiveSupportAIMode`): `AiActive`, `AiResolved`, `HumanQueued`, `HumanAssigned`.
- `ResolvedAt` (DateTime?): Timestamp when conversation was marked resolved by AI.
- `HandedOffAt` (DateTime?): Timestamp when conversation was handed off to staff.

### `LiveSupportMessage`
We query this table to count messages sent by the AI:
- `SenderType` (enum `LiveSupportSenderType`): Filter for `LiveSupportSenderType.AI`.
- `SentAt` (DateTime): Timestamp of message.

### `LiveSupportAIPendingAction`
We query this table to count successfully executed actions:
- `Status` (enum `LiveSupportAIPendingActionStatus`): Filter for `LiveSupportAIPendingActionStatus.Succeeded`.
- `CompletedAt` (DateTime?): Timestamp of action completion.
