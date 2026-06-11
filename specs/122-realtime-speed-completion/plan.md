# Implementation Plan: Real-time Speed Completion — Phase 2

**Branch**: `122-realtime-speed-completion` | **Date**: 2026-06-11 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/122-realtime-speed-completion/spec.md)
**Input**: Feature specification from `/specs/122-realtime-speed-completion/spec.md`
**Predecessor**: `121-realtime-platform-speed` (infrastructure — 100% complete)

## Summary

This plan completes the remaining items from the realtime-platform-speed audit. The infrastructure (PlatformHub, OutboxEvent, usePlatformEvents, OutboxProcessor, Idempotency, Rate Limiting) is fully operational. This phase focuses on: (1) building a frontend cache invalidation registry, (2) eliminating all `router.refresh()`/`window.location.reload()` calls, (3) expanding outbox event coverage to ~25+ event types, (4) eliminating fast polling, (5) lazy-loading heavy JS, and (6) expanding idempotency coverage.

## Technical Context

**Language/Version**: C# (.NET 9, C# 13) backend, TypeScript (Next.js 16.2.1, React 19) frontend.
**Existing Infrastructure**: `PlatformHub.cs`, `OutboxEvent.cs`, `OutboxProcessorBackgroundService.cs`, `usePlatformEvents.ts` (singleton + listener registry), `RedisIdempotencyService.cs`, `IdempotentAttribute.cs`.
**Testing**: `dotnet test` (backend), `npm run build` + `npm run lint` (frontend).
**Constraints**: Zero `window.location.reload()` or `router.refresh()` after this phase. No polling below 30s when SignalR connected.

## Proposed Changes

### Component 1: Frontend Cache Invalidation Registry

#### [NEW] `frontend/src/lib/cache-invalidation.ts`

Create a centralized cache invalidation registry that maps event types to cache keys. Provides:
- `invalidate(key: string)` — invalidates a single cache key pattern
- `invalidateMany(keys: string[])` — invalidates multiple keys
- `registerCacheStore(name, clearFn, refetchFn)` — registers a cache store
- Built-in debounce/throttle to prevent stampeding when multiple events arrive

Integration with existing services:
- `content-service.ts` → register its cache (packages, lessons)
- `balance-service.ts` → register its cache (balance)
- `shell-store` → register shell data cache
- `usePlatformEvents.ts` → call `invalidateMany()` on event receipt

#### [MODIFY] `frontend/src/hooks/usePlatformEvents.ts`

- Add new event types to the listener registry: `PackagePublished`, `LessonUpdated`, `VideoFailed`, `ExamSubmitted`, `HomeworkSubmitted`, `CommunityPostCreated`, `NotificationRead`, `AiJobCompleted`, `AiJobFailed`
- Add corresponding TypeScript payload interfaces
- Wire each event to `invalidateMany()` calls from the registry

#### [MODIFY] `frontend/src/services/content-service.ts`

- Register cache store with the invalidation registry
- Export individual invalidation functions that the registry can call

---

### Component 2: Refresh/Reload Elimination

#### [MODIFY] `frontend/src/components/balance/PurchaseContentModal.tsx`

- Line 85: Replace `router.refresh()` with `invalidateMany(["student:shell", "content:packages"])` + call `onPurchaseSuccess()` callback
- Update success message from "سيتم تحديث الصفحة الآن..." to "تم الشراء بنجاح!"

#### [MODIFY] `frontend/src/app/qr/[codeHash]/QrRedeemClient.tsx`

- Line 78: Replace `window.location.reload()` with a retry function that re-calls the `redeem()` function directly

#### [MODIFY] `frontend/src/app/admin/students/AdminStudentsPageClient.tsx`

- Line 415: Replace `window.location.reload()` with a targeted data refetch function

---

### Component 3: Expanded Outbox Events (Backend)

All changes follow the established pattern: create `new OutboxEvent { Type = "EventName", ... }` and `_db.OutboxEvents.Add(outboxEvent)` before `SaveChangesAsync`.

#### [MODIFY] `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`

Add outbox events for:
- `LessonUpdated` — in UpdateLesson handler
- `SectionCreated` — in CreateSection handler
- `TermCreated` — in CreateTerm handler
- `VideoDeleted` — in DeleteVideo handler
- `ResourceDeleted` — in DeleteResource handler

#### [MODIFY] `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`

Add `ExamSubmitted` outbox event targeting `User_{studentId}`.

#### [MODIFY] `backend/src/NaderGorge.Application/Features/Homework/Commands/SubmitHomeworkCommandHandler.cs`

Add `HomeworkSubmitted` outbox event targeting `User_{studentId}`.

#### [MODIFY] `backend/src/NaderGorge.Application/Features/Student/Commands/CreateExtraWatchRequestCommand.cs`

Add `ExtraWatchRequestCreated` outbox event targeting `Role_Admin`.

#### [MODIFY] `backend/src/NaderGorge.Application/Features/Internal/Commands/AiAnalysisCompletedCommand.cs`

Add `AiJobCompleted` outbox event (in addition to existing `VideoReady`).

#### [MODIFY] Worker AI failure callback handler

Add `AiJobFailed` outbox event when AI analysis fails.

---

### Component 4: Polling Elimination

#### [MODIFY] `frontend/src/app/admin/ai-monitor/AIMonitorPageClient.tsx`

- Line 208: Remove the `setInterval(poll, 2500)` for job detail polling. Replace with SignalR-only updates when connected.
- Lines 844-846: Change minimum fallback interval from 3000ms to 30000ms even when disconnected.
- Add exponential backoff: 30s → 60s → 120s when SignalR disconnected.

#### [MODIFY] `frontend/src/components/admin/LessonVideoList.tsx`

- Line 69: Change fallback interval from 3000ms to 30000ms when disconnected.
- When SignalR connected: rely purely on `VideoReady`/`VideoFailed` events, no polling.

---

### Component 5: Lazy Loading Heavy JS

#### [MODIFY] `frontend/src/components/ui/circular-gallery.tsx`

- Convert to dynamic import with `next/dynamic` and `ssr: false`
- Add loading fallback (static placeholder)

#### [MODIFY] `frontend/src/components/ui/ripple-grid.tsx`

- Convert to dynamic import with `next/dynamic` and `ssr: false`

#### [MODIFY] `frontend/src/components/ui/SplitText.tsx`

- Convert GSAP imports to dynamic `import()` calls inside `useGSAP` hook
- Only load GSAP when the component is actually rendered

#### [MODIFY] `frontend/src/components/codes/QrScanner.tsx`

- Wrap `Scanner` component with `next/dynamic` and `ssr: false`
- Add loading state fallback

---

### Component 6: Expanded Idempotency

#### [MODIFY] `backend/src/NaderGorge.API/Controllers/ExamController.cs` (or equivalent)

Add `[Idempotent]` attribute to exam submission endpoint.

#### [MODIFY] `backend/src/NaderGorge.API/Controllers/HomeworkController.cs` (or equivalent)

Add `[Idempotent]` attribute to homework submission endpoint.

#### [MODIFY] `backend/src/NaderGorge.API/Controllers/ContentController.cs` (or equivalent)

Add `[Idempotent]` attribute to extra watch request endpoint.

---

### Component 7: Bundle Analyzer Setup

#### [MODIFY] `frontend/package.json`

Add script: `"analyze": "ANALYZE=true next build"`

#### [NEW or MODIFY] `frontend/next.config.ts`

Add `@next/bundle-analyzer` integration when `ANALYZE=true` environment variable is set.

---

## Verification Plan

### Automated Tests

```bash
cd backend && dotnet test
cd frontend && npm run build
cd frontend && npm run lint
```

### Manual Verification

```bash
# Verify zero refresh/reload patterns
cd frontend && rg -n "window\.location\.reload|router\.refresh\(" src

# Verify no fast polling when connected
cd frontend && rg -n "setInterval" src --glob "*.tsx" | grep -v "30000\|60000"

# Count outbox event types
cd backend && rg "Type = \"" src/NaderGorge.Application --glob "*.cs" | sort -u | wc -l
```

## Complexity Tracking

*No constitution violations present. All changes follow established patterns from 121-realtime-platform-speed.*
