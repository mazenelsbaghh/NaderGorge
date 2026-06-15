# Tasks: watch-requests-refinements-and-repurchases

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Technical Tasks

### Task Group 1: Backend Updates

#### Task 1.1: Modify ApproveWatchRequestCommand.cs
- **File**: `backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveWatchRequestCommand.cs`
- **Action**: Add `int AddedViews = 1` to `ApproveWatchRequestCommand` record.
- **Action**: Remove the `if (req.Status != RequestStatus.Pending)` validation check.
- **Action**: Update the approval logic to increment `CustomMaxWatchCount` by the `AddedViews` value. Record `VideoOverride` with the correct `AddedViews` count and new limit.
- **Expected Outcome**: The request status updates to `Approved` and the custom views limit is incremented by the specified amount.

#### Task 1.2: Modify RejectWatchRequestCommand.cs
- **File**: `backend/src/NaderGorge.Application/Features/Admin/Commands/RejectWatchRequestCommand.cs`
- **Action**: Remove the `if (req.Status != RequestStatus.Pending)` validation check.
- **Action**: If `req.Status` was previously `Approved`, set `watchEvent.IsLocked = true` and decrement `watchEvent.CustomMaxWatchCount` by 1.
- **Expected Outcome**: The request status updates to `Rejected` and the custom limit is decremented.

#### Task 1.3: Update AdminController.cs
- **File**: `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- **Action**: Update `ApproveWatchRequestBody` to include `int? AddedViews`.
- **Action**: Pass `AddedViews ?? 1` to `ApproveWatchRequestCommand` invocation in `ApproveWatchRequest` action.
- **Expected Outcome**: The endpoint accepts the number of views to add.

#### Task 1.4: Update PurchaseContentCommand.cs
- **File**: `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`
- **Action**: Inside the handler, check if the content type is `CodeType.Lesson`.
- **Action**: Query the lesson's videos and check if any video watch event is locked/exhausted or if there is a rejected watch request for it. If so, set `isRepurchase = true`.
- **Action**: Bypass the `alreadyPurchased` and `coveredBy` blocks if `isRepurchase` is true.
- **Action**: If `isRepurchase` is true, after deducting the balance, reset watch statistics (set `WatchCount = 0`, `IsLocked = false`, `CustomMaxWatchCount = null`, `TimeWatchedInSeconds = 0`) for all videos in that lesson.
- **Action**: Delete all `ExtraWatchRequest` records for this student and these videos.
- **Action**: Add outbox events (`ExtraWatchRequestUpdated`) for each video in the lesson to notify client players.
- **Expected Outcome**: Student repurchases the lesson, balance is deducted, and all lesson videos are unlocked with views reset to 0.

---

### Task Group 2: Frontend Updates

#### Task 2.1: Update admin-service.ts
- **File**: `frontend/src/services/admin-service.ts`
- **Action**: Update the `approveWatchRequest` method to accept `addedViews?: number`. Pass it in the post body request: `{ reason, addedViews }`.
- **Expected Outcome**: The frontend passes `addedViews` in the API call.

#### Task 2.2: Update WatchRequestsPageClient.tsx
- **File**: `frontend/src/app/admin/watch-requests/WatchRequestsPageClient.tsx`
- **Action**: Add an Edit Decision modal and button to allow modifying resolved requests (status Approved or Rejected).
- **Action**: Include a number input for `addedViews` in both the Approve modal and the Edit modal.
- **Action**: Update the action column to show `تعديل القرار` for already resolved rows instead of `-`.
- **Expected Outcome**: The admin can edit decision on resolved requests and specify custom extra views.

#### Task 2.3: Update SecureVideoPlayer.tsx
- **File**: `frontend/src/components/video/SecureVideoPlayer.tsx`
- **Action**: Add `lessonPrice?: number` and `lessonId?: string` to `SecureVideoPlayerProps`.
- **Action**: In `SecureVideoPlayerProps` declaration and destructuring, extract `lessonPrice` and `lessonId`.
- **Action**: If `status === 'locked'` and `lessonPrice !== undefined && lessonId`, render a button: `شراء الحصة مجدداً (${lessonPrice} ج.م)`.
- **Action**: On click, prompt confirmation, call `balanceService.purchaseContent('Lesson', lessonId)`, and reload the page (`window.location.reload()`) upon success. Ensure `toast` from `react-hot-toast` is imported.
- **Expected Outcome**: A locked player renders a "Buy again" button that prompts for confirmation and executes the repurchase.

#### Task 2.4: Update LessonCarousel.tsx
- **File**: `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel.tsx`
- **Action**: Update `LessonCarouselProps` and destructured props to accept `lessonPrice?: number` and `lessonId?: string`.
- **Action**: Pass `lessonPrice` and `lessonId` to `<SecureVideoPlayer>`.
- **Expected Outcome**: Price and ID are propagated down.

#### Task 2.5: Update LessonViewer.tsx
- **File**: `frontend/src/components/content/LessonViewer.tsx`
- **Action**: Pass `lesson.price` and `lesson.id` to the `<LessonCarousel>` instance.
- **Expected Outcome**: Lesson data is supplied correctly.

---

### Task Group 3: Quality Gates & Testing

#### Task 3.1: Deep Critique and Fixes
- [ ] Perform deep review of implementation correctness, validation, and error states. Expected outcome: clean execution.

#### Task 3.2: Clean Code Guard
- [ ] Run `clean-code-guard` against all changed production code files. Expected outcome: passes all checks.

#### Task 3.3: Test Guard
- [ ] Run `test-guard` against changed test files (if any). Expected outcome: passes all checks.

#### Task 3.4: Verification and Feature Tests
- [ ] Build backend and run tests with `dotnet build && dotnet test`. Expected outcome: passes all tests.
- [ ] Perform frontend build verification with `npm run build` in `frontend` folder. Expected outcome: passes build.
- [ ] Execute feature tests manually or automatically to verify decision modifies and repurchase reset watch stats.
