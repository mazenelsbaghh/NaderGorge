# Data Model: Extra Watch Request

## Entities

### `ExtraWatchRequest`
Stores a user's request to get an extra view for a locked video.

**Fields**:
- `Id` (Guid, PK)
- `UserId` (Guid, FK to User)
- `LessonVideoId` (Guid, FK to LessonVideo)
- `Status` (Enum/Int): `Pending = 0`, `Approved = 1`, `Rejected = 2`
- `CreatedAt` (DateTime)
- `ResolvedAt` (DateTime, Nullable)

**Relationships**:
- Belongs to `User`
- Belongs to `LessonVideo`

## State Transitions
- **Pending** -> **Approved**: `ResolvedAt` is set to `UtcNow`. Associated `VideoWatchEvent` has `WatchCount` decremented/reset to `MaxWatchCount - 1` and `IsLocked = false`.
- **Pending** -> **Rejected**: `ResolvedAt` is set to `UtcNow`. `VideoWatchEvent` remains locked.

## Validation Rules
- Cannot create a new `ExtraWatchRequest` for a user and video if there is already a `Pending` request. (Could also disallow if `Rejected` within a certain cooldown, but for V1 we just block if `Pending` OR `Rejected`). Wait, if rejected they shouldn't just be able to spam again immediately. For V1 blocking if there's any `Pending` is sufficient.  

## DTOs

### `ExtraWatchRequestDto`
- `Id`
- `UserId`
- `StudentName`
- `StudentPhone`
- `LessonVideoId`
- `VideoTitle`
- `Status`
- `CreatedAt`
- `ResolvedAt`

### `CreateExtraWatchRequestDto`
- `LessonVideoId`
