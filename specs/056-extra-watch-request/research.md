# Technical Research & Decisions

## Context
Feature: Extra Watch Request
Goal: Allow students to request extra views when a video locks, and allow admins to approve/reject them.

## Questions & Decisions

### 1. How do we unlock the video for the user?
- **Option A**: Reset the user's `WatchCount` to `MaxWatchCount - 1`. This effectively gives them 1 extra view.
- **Option B**: Add a new property `ExtraWatchAllowance` to `VideoWatchEvent`. The locking logic becomes `IsLocked = WatchCount >= (MaxWatchCount + ExtraWatchAllowance)`.
- **Decision**: **Option A** is the simplest and doesn't require modifying the `VideoWatchEvent` schema or the locking logic algorithm. When an admin approves, we find the `VideoWatchEvent`, set `WatchCount = MaxWatchCount - 1`, and `IsLocked = false`. BUT wait, if we drop `WatchCount`, analytics about how many times they actually watched are lost. So maybe **Option B** is better. Wait, what if we just set `IsLocked = false` and leave `WatchCount` alone? But the next time they watch it increments and locks immediately. 
- **Actually**, let's go with **Option C**: Set `IsLocked = false` and decrement `WatchCount` by 1. That gives exactly 1 more view before it locks again. It slightly alters absolute metrics but requires no schema change. But for cleaner data, modifying `VideoWatchEvent` to add `AllowedResets` or purely just changing `IsLocked = false` and not letting it auto-lock until it reaches a higher threshold? Let's just reset the lock by decrementing the count by 1. Wait, if `WatchCount` was 3 and `Max` was 3, we set `IsLocked = false` but next play it goes to 4 and locks. That gives exactly one view.
- **Decision Revision**: Actually, the simplest approach without side effects is: Set `IsLocked = false` and set `WatchCount` to `MaxWatchCount - 1`. For an extra view, `WatchCount = MaxWatchCount - 1`. 

### 2. How to represent the `ExtraWatchRequest` entity?
- Standard Entity Framework model `ExtraWatchRequest` with `Id`, `UserId`, `LessonVideoId`, `Status` (Pending = 0, Approved = 1, Rejected = 2), `CreatedAt`, `ResolvedAt`.

### 3. Frontend Admin Dashboard details
- Place the management table in `/admin/watch-requests` or integrate into an existing watch-limits page. We will create a standalone page `/admin/watch-requests`.

### 4. Admin vs Worker?
- Unlike AI generation, watch requests are purely synchronous fast operations. Admin clicks "Approve", backend updates `ExtraWatchRequest` state and updates `VideoWatchEvent`, then `await _db.SaveChangesAsync()`. No need for BullMQ. 

## Technical Output
All design parameters are known. Proceeding to Data Model.
