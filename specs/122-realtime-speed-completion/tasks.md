# Tasks: Real-time Speed Completion — Phase 2

**Input**: Design documents from `/specs/122-realtime-speed-completion/`
**Prerequisites**: plan.md (required), spec.md (required), predecessor feature 121-realtime-platform-speed (100% complete)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Cache Invalidation Registry (Frontend) — US1 MVP 🎯

**Purpose**: Create a centralized cache invalidation system that all platform events route through.

**⚠️ CRITICAL**: This phase must be completed before refresh elimination (Phase 2). All other phases depend on this.

- [x] T001 Create `frontend/src/lib/cache-invalidation.ts` with:
  - A `Map<string, { clear: () => void; refetch: () => void }>` called `cacheStores`
  - Export `registerCacheStore(name: string, clear: () => void, refetch: () => void): void`
  - Export `invalidate(key: string): void` — finds matching store by key prefix and calls `clear()` then `refetch()` with a 100ms debounce
  - Export `invalidateMany(keys: string[]): void` — calls `invalidate` for each key, deduplicating within a 200ms window
  - Key patterns: `"student:shell"`, `"student:balance"`, `"content:packages"`, `"content:lesson:{id}"`, `"admin:students"`, `"admin:ai-monitor"`, `"notifications"`

- [x] T002 In `frontend/src/services/content-service.ts`, at the module level (after the class/service definition), call `registerCacheStore("content:packages", clearPackagesCache, refetchPackages)`. Add a `refetchPackages` function that triggers the next data fetch. Export a `registerContentCacheInvalidation()` function that performs this registration (to be called from app init).

- [x] T003 In `frontend/src/hooks/usePlatformEvents.ts`, add new event types to the `listeners` registry object and the `PlatformEventHandlers` interface:
  - `PackagePublished` with payload `{ packageId: string }`
  - `LessonUpdated` with payload `{ lessonId: string, packageId: string, title: string }`
  - `VideoFailed` with payload `{ lessonId: string, videoId: string, error: string }`
  - `ExamSubmitted` with payload `{ examId: string, attemptId: string }`
  - `HomeworkSubmitted` with payload `{ homeworkId: string }`
  - `CommunityPostCreated` with payload `{ postId: string, authorId: string }`
  - `AiJobCompleted` with payload `{ jobId: string, lessonVideoId: string }`
  - `AiJobFailed` with payload `{ jobId: string, error: string }`
  For each, add: interface definition, Set in `listeners`, handler wrapper in hook, `conn.on()` registration, cleanup in return.
  Follow the exact same pattern as existing events (e.g., `VideoReady`).

- [x] T004 In `frontend/src/hooks/usePlatformEvents.ts`, inside each `conn.on()` callback, after dispatching to listeners, call `invalidateMany()` from `cache-invalidation.ts`:
  - `BalanceChanged` → `invalidateMany(["student:shell", "student:balance"])`
  - `NotificationCreated` → `invalidateMany(["student:shell", "notifications"])`
  - `CodeActivated` → `invalidateMany(["student:shell", "content:packages", "student:balance"])`
  - `LessonPublished` → `invalidateMany(["content:packages"])`
  - `LessonUpdated` → `invalidateMany(["content:packages", "content:lesson:" + payload.lessonId])`
  - `VideoReady` → `invalidateMany(["content:lesson:" + payload.lessonId])`
  - `VideoFailed` → `invalidateMany(["content:lesson:" + payload.lessonId])`
  - `ResourceReady` → `invalidateMany(["content:lesson:" + payload.lessonId])`
  - `PackageUpdated` → `invalidateMany(["content:packages"])`
  - `PackagePublished` → `invalidateMany(["content:packages"])`
  - `ExtraWatchRequestUpdated` → `invalidateMany(["content:lesson:" + payload.videoId])`
  - `AiJobProgress` → `invalidateMany(["admin:ai-monitor"])`
  - `AiJobCompleted` → `invalidateMany(["admin:ai-monitor"])`
  - `AiJobFailed` → `invalidateMany(["admin:ai-monitor"])`

**Checkpoint**: Run `cd frontend && npm run build` — must compile with zero errors.

---

## Phase 2: Refresh/Reload Elimination — US1

**Purpose**: Remove all `router.refresh()` and `window.location.reload()` calls.

- [x] T005 In `frontend/src/components/balance/PurchaseContentModal.tsx` line 82-86, replace:
  ```typescript
  setTimeout(() => {
    void onPurchaseSuccess?.();
    onClose();
    router.refresh();
  }, 2000);
  ```
  with:
  ```typescript
  setTimeout(() => {
    invalidateMany(["student:shell", "student:balance", "content:packages"]);
    void onPurchaseSuccess?.();
    onClose();
  }, 1500);
  ```
  Add `import { invalidateMany } from '@/lib/cache-invalidation';` at top. Remove unused `useRouter` import if no other usage remains. Change success message text from `"سيتم تحديث الصفحة الآن..."` to `"تمت العملية بنجاح!"`.

- [x] T006 In `frontend/src/app/qr/[codeHash]/QrRedeemClient.tsx` line 78, replace `onClick={() => window.location.reload()}` with `onClick={() => { setState("loading"); void redeem(); }}` — where `redeem` is the existing async function already defined in the component at line 33. Move the `redeem` function declaration outside the `useEffect` so it can be referenced from the button click handler. Ensure `redeem` is wrapped in `useCallback` with proper dependencies.

- [x] T007 In `frontend/src/app/admin/students/AdminStudentsPageClient.tsx` line 415, replace `onClick={() => window.location.reload()}` with a call to a `handleRefresh` function that re-fetches the student list data by calling the existing data fetch function. If there's a `fetchStudents` or similar function, call it directly. If not, extract the fetch logic into a named function and call it.

**Checkpoint**: Run `cd frontend && rg -n "window\.location\.reload|router\.refresh\(" src` — must return zero results. Run `cd frontend && npm run build` — must compile with zero errors.

---

## Phase 3: Expanded Outbox Events (Backend) — US2

**Purpose**: Add outbox events to command handlers that modify user-facing data.

All tasks follow this exact pattern (copy from existing `LessonPublished` in `AdminContentCommands.cs`):
```csharp
var outboxEvent = new OutboxEvent
{
    Type = "EventName",
    PayloadJson = JsonSerializer.Serialize(new { /* relevant fields */ }),
    TargetGroup = "GroupName",  // or null
    TargetUserId = userId,      // or null
};
_db.OutboxEvents.Add(outboxEvent);
```
Add BEFORE the `await _db.SaveChangesAsync()` call.

- [x] T008 In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`, find the UpdateLesson handler method. Add a `LessonUpdated` outbox event with `TargetGroup = $"Package_{lesson.Section.Term.PackageId}"` and payload `{ lessonId, packageId, title }`.

- [x] T009 In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`, find the CreateSection handler. Add a `SectionCreated` outbox event with `TargetGroup = $"Package_{section.Term.PackageId}"`.

- [x] T010 In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`, find the CreateTerm handler. Add a `TermCreated` outbox event with `TargetGroup = $"Package_{term.PackageId}"`.

- [x] T011 In `backend/src/NaderGorge.Application/Features/Exams/Commands/SubmitExamCommand.cs`, after the exam submission is saved, add an `ExamSubmitted` outbox event with `TargetUserId = command.StudentId` and payload `{ examId = command.ExamId, attemptId = command.AttemptId }`.

- [x] T012 In `backend/src/NaderGorge.Application/Features/Homework/Commands/SubmitHomeworkCommandHandler.cs`, after homework submission is saved, add a `HomeworkSubmitted` outbox event with `TargetUserId = command.StudentId` and payload `{ homeworkId = command.HomeworkId }`.

- [x] T013 In `backend/src/NaderGorge.Application/Features/Student/Commands/CreateExtraWatchRequestCommand.cs`, after the watch request is created, add an `ExtraWatchRequestCreated` outbox event with `TargetGroup = "Role_Admin"` and payload `{ videoId, studentId, requestId }`.

- [x] T014 In `backend/src/NaderGorge.Application/Features/Internal/Commands/AiAnalysisCompletedCommand.cs`, alongside the existing `VideoReady` event, add an `AiJobCompleted` outbox event with `TargetGroup = "Role_Admin"` and payload `{ jobId, lessonVideoId }`.

- [x] T015 Add `AiJobFailed` event: In the worker failure callback handler (same file or adjacent handler for AI failures), add an `AiJobFailed` outbox event with `TargetGroup = "Role_Admin"` and payload `{ jobId, error }`.

**Checkpoint**: Run `cd backend && dotnet build` — must compile with zero errors. Run `cd backend && dotnet test` — all existing tests must pass.

---

## Phase 4: Polling Elimination — US3

**Purpose**: Remove fast polling; rely on SignalR events with slow fallback.

- [x] T016 In `frontend/src/app/admin/ai-monitor/AIMonitorPageClient.tsx` line 208, change `setInterval(poll, 2500)` to `setInterval(poll, 30000)`. This is the job detail polling interval — make it always 30s minimum regardless of SignalR status.

- [x] T017 In `frontend/src/app/admin/ai-monitor/AIMonitorPageClient.tsx` lines 844-846, change the disconnected fallback polling from `3000` to `30000`:
  Replace: `const pollInterval = isConnected ? 30000 : 3000;`
  With: `const pollInterval = isConnected ? 60000 : 30000;`

- [x] T018 In `frontend/src/components/admin/LessonVideoList.tsx` line 69, change:
  Replace: `const interval = setInterval(checkStatus, isConnected ? 30000 : 3000);`
  With: `const interval = setInterval(checkStatus, isConnected ? 60000 : 30000);`

**Checkpoint**: Run `cd frontend && rg -n "setInterval" src --glob "*.tsx"` — verify no interval below 30000ms. Run `cd frontend && npm run build` — must compile.

---

## Phase 5: Lazy Loading Heavy JS — US4

**Purpose**: Convert eager imports of OGL, GSAP, QR Scanner to dynamic imports.

- [x] T019 In `frontend/src/components/ui/ripple-grid.tsx`, the component uses `'use client'` and imports `ogl` at the top. Since it's used in multiple shell chromes, create a wrapper: In each parent file that imports `RippleGrid` (`TeacherShellChrome.tsx`, `AdminShellChrome.tsx`, `not-found.tsx`, `LoginPageClient.tsx`, `AssistantShellChrome.tsx`, `ForgotPasswordPageClient.tsx`, `RegisterPageClient.tsx`), replace the import:
  From: `import { RippleGrid } from '@/components/ui/ripple-grid';`
  To: `const RippleGrid = dynamic(() => import('@/components/ui/ripple-grid').then(mod => ({ default: mod.RippleGrid })), { ssr: false });`
  Add `import dynamic from 'next/dynamic';` at the top of each file (if not already present).

- [x] T020 In `frontend/src/components/codes/QrScanner.tsx`, change line 10 from:
  `import { Scanner } from '@yudiel/react-qr-scanner';`
  to a dynamic import inside the component using `React.lazy`:
  ```typescript
  const ScannerComponent = dynamic(() => import('@yudiel/react-qr-scanner').then(mod => ({ default: mod.Scanner })), { ssr: false, loading: () => <div className="aspect-square flex items-center justify-center"><Loader2 className="h-8 w-8 animate-spin" /></div> });
  ```
  Add `import dynamic from 'next/dynamic';` and `import { Loader2 } from 'lucide-react';` at top. Replace `<Scanner` with `<ScannerComponent` in the JSX.

- [x] T021 In `frontend/src/components/video/SecureVideoPlayer.tsx` line 11, replace:
  `import SplitText from '@/components/ui/SplitText';`
  with:
  `const SplitText = dynamic(() => import('@/components/ui/SplitText'), { ssr: false });`
  Add `import dynamic from 'next/dynamic';` at top (if not already present).

**Checkpoint**: Run `cd frontend && npm run build` — must compile with zero errors. Verify OGL/GSAP are not in main chunks by examining the build output.

---

## Phase 6: Expanded Idempotency — US5

**Purpose**: Add `[Idempotent]` attribute to more sensitive endpoints.

- [x] T022 In `backend/src/NaderGorge.API/Controllers/ExamsController.cs`, add `[Idempotent]` attribute above the `SubmitExam` action method (around line 62). Add `using NaderGorge.API.Filters;` at the top if not already present.

- [x] T023 In `backend/src/NaderGorge.API/Controllers/HomeworkController.cs`, add `[Idempotent]` attribute above the `SubmitHomework` action method (around line 28). Add `using NaderGorge.API.Filters;` at the top if not already present.

- [x] T024 In `backend/src/NaderGorge.API/Controllers/VideoSessionController.cs`, add `[Idempotent]` attribute above the CreateExtraWatchRequest action method (around line 119). Add `using NaderGorge.API.Filters;` at the top if not already present.

**Checkpoint**: Run `cd backend && dotnet build` — must compile. Run `cd backend && dotnet test` — all tests pass.

---

## Phase 7: Bundle Analyzer Setup — US5

- [x] T025 In `frontend/package.json`, add to `"scripts"`: `"analyze": "ANALYZE=true next build"`. Run `cd frontend && npm install --save-dev @next/bundle-analyzer`.

- [x] T026 In `frontend/next.config.ts` (or `.js`/`.mjs`), wrap the existing config export with bundle analyzer when `ANALYZE=true`:
  ```typescript
  import withBundleAnalyzer from '@next/bundle-analyzer';
  const analyzeBundleConfig = withBundleAnalyzer({ enabled: process.env.ANALYZE === 'true' });
  // wrap existing config: export default analyzeBundleConfig(existingConfig);
  ```

**Checkpoint**: Run `cd frontend && npm run build` — must compile with zero errors.

---

## Phase 8: Quality Gates & Verification

- [x] T027 Run full backend tests: `cd backend && dotnet test` — all tests must pass.
- [x] T028 Run full frontend build: `cd frontend && npm run build` — zero errors.
- [x] T029 Run frontend lint: `cd frontend && npm run lint` — zero errors (warnings acceptable).
- [x] T030 Verify zero refresh/reload: `cd frontend && rg -n "window\.location\.reload|router\.refresh\(" src` — zero results.
- [x] T031 Verify no fast polling: `cd frontend && rg -n "setInterval" src --glob "*.tsx"` — no intervals below 30000.
- [x] T032 Count outbox event types: `cd backend && rg "Type = " src/NaderGorge.Application --glob "*.cs" | grep -oP '"[A-Za-z]+"' | sort -u` — at least 17+ unique event types.
- [x] T033 Run `clean-code-guard` on all modified production C# and TypeScript files.
- [x] T034 Run `test-guard` on all modified test files.
- [x] T035 Update `docs/realtime-platform-status-2026-06-11.md` to mark completed items.

---

## Dependencies & Execution Order

- **Phase 1 (Cache Registry)**: Blocks Phase 2 (refresh elimination).
- **Phase 2 (Refresh Elimination)**: Can run in parallel with Phase 3.
- **Phase 3 (Outbox Events)**: Independent of frontend phases.
- **Phase 4 (Polling)**: Independent.
- **Phase 5 (Lazy Loading)**: Independent.
- **Phase 6 (Idempotency)**: Independent.
- **Phase 7 (Bundle Analyzer)**: Independent.
- **Phase 8 (Quality Gates)**: Blocks final completion; runs after all other phases.
