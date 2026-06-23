# Tasks: Live Support AI Refinements and Performance Dashboard

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification
- [x] Phase 2: Arabic Clarification
- [x] Phase 3: Technical Planning
- [x] Phase 4: Detailed Task Breakdown

## Task Checklist

### Backend Changes

- [x] Task 1: Fix sequential SaveChangesAsync in `PublishAsync` inside `LiveSupportAIAdminService.cs`
  - **File**: `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIAdminService.cs`
  - **Action**: Inside the `PublishAsync` method, if `current` (the currently published policy version) is not null, update its state and explicitly call `await db.SaveChangesAsync(ct);` before enabling the new policy. This clears the unique constraint filter (`IsEnabled = true`) first.

- [x] Task 2: Create DTO class `LiveSupportAIStatsDto` in `LiveSupportAIDtos.cs`
  - **File**: `backend/src/NaderGorge.Application/Features/LiveSupportAI/Dtos/LiveSupportAIDtos.cs`
  - **Action**: Add `public sealed record LiveSupportAIStatsDto(int ActiveConversations, int ResolvedIssues, int Handoffs, int TotalMessagesSent, int SuccessfulActions);`.

- [x] Task 3: Declare and implement `EnableAsync` and `GetStatsAsync` in service layer
  - **File**: `backend/src/NaderGorge.Application/Features/LiveSupportAI/Interfaces/ILiveSupportAIAdminService.cs`
  - **Action**: Add signatures:
    - `Task<LiveSupportAIPolicyDto> EnableAsync(Guid adminUserId, CancellationToken cancellationToken);`
    - `Task<LiveSupportAIStatsDto> GetStatsAsync(string period, CancellationToken cancellationToken);`
  - **File**: `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIAdminService.cs`
  - **Action**: Implement `EnableAsync` to set `IsEnabled = true` on the published policy version. Throw an exception if no published policy exists.
  - **Action**: Implement `GetStatsAsync` to aggregate metrics based on the provided period (`last-24h`, `last-7d`, `last-30d`, `lifetime`).

- [x] Task 4: Add HTTP endpoints in `LiveSupportAIAdminController.cs`
  - **File**: `backend/src/NaderGorge.API/Controllers/LiveSupportAIAdminController.cs`
  - **Action**: Add `POST enable` calling `service.EnableAsync` and `GET stats` calling `service.GetStatsAsync`.

- [x] Task 5: Implement backend tests for the new admin controls and stats query
  - **File**: `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AIAdminAuthorizationTests.cs`
  - **Action**: Add test cases to verify role validation and expected behavior of the new endpoints.

### Frontend Changes

- [x] Task 6: Add Axios service definitions in `live-support-ai-service.ts`
  - **File**: `frontend/src/services/live-support-ai-service.ts`
  - **Action**: Declare `AIStats` interface and add `enable` and `getStats` methods to the `live-supportAIService` object.

- [x] Task 7: Update `AdminAISupportPageClient.tsx` to support tabs, toggles, pulse indicators, and stats
  - **File**: `frontend/src/app/admin/live-support/ai/AdminAISupportPageClient.tsx`
  - **Action**: Add active tab state, render tabs, add Enable button when disabled, add green pulsating badge when active, and render the statistics card grid with Cairo styling.

### Final Verification and Quality Gates

- [x] Task 8: Deep architectural, code, and UI/UX critique and fixes
- [x] Task 9: Run `clean-code-guard` against changed files
- [x] Task 10: Run `test-guard` against changed test files
- [x] Task 11: Execute feature tests, build checks, and verify all tests pass
