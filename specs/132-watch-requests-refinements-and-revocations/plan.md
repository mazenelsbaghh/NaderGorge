# Technical Plan: watch-requests-refinements-and-repurchases

## Technical Context
We need to update the watch requests administration flows and student video player. The goal is to allow modifying decisions, specifying extra views, fixing Egypt timezone relative date formatting, and implementing student-side lesson repurchase when locked. Our research is documented in `research.md` (or the "Research" sections of features).

## Constitution Check
This plan aligns with `.specify/memory/constitution.md` design principles.

## Phase 0: Scaffolding and Schema Analysis
We analyze existing data structures. The "Data Model" remains the same as in `data-model.md` and doesn't require new entity types or tables. We only modify database queries and update records in the database.

## Phase 1: Implementation Details

### Backend Components

#### 1. Command Updates (`NaderGorge.Application`)

##### [MODIFY] [ApproveWatchRequestCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveWatchRequestCommand.cs)
- Update the command parameters to include `int AddedViews = 1`.
- Remove the check `if (req.Status != RequestStatus.Pending)`.
- Increment the student's custom max watch count (`CustomMaxWatchCount`) by the specified `AddedViews` (minimum 1).
- Add the corresponding `VideoOverride` record detailing the change.

##### [MODIFY] [RejectWatchRequestCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/RejectWatchRequestCommand.cs)
- Remove the check `if (req.Status != RequestStatus.Pending)`.
- If the previous request status was `Approved`, set the video to locked (`IsLocked = true`) and decrement the student's `CustomMaxWatchCount` by 1 to revert the approval.

##### [MODIFY] [PurchaseContentCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs)
- Check if the requested content type is `CodeType.Lesson`.
- Determine if the student is eligible for a repurchase (has existing access, but at least one video in the lesson is locked/exhausted, or there is a rejected watch request for it).
- If eligible for repurchase, bypass the `alreadyPurchased` and `coveredBy` blocks.
- Upon successful purchase:
  - Reset `WatchCount = 0`, `IsLocked = false`, `CustomMaxWatchCount = null`, `TimeWatchedInSeconds = 0` for all videos in that lesson.
  - Delete all `ExtraWatchRequest` rows for this student and these videos.
  - Write SignalR outbox events for clients (`ExtraWatchRequestUpdated`).

#### 2. Controller Updates (`NaderGorge.API`)

##### [MODIFY] [AdminController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/AdminController.cs)
- Update `ApproveWatchRequestBody` to include `int? AddedViews`.
- Pass `AddedViews` parameter from request body to `ApproveWatchRequestCommand`.

---

### Frontend Components

#### 1. Services (`frontend/src/services`)

##### [MODIFY] [admin-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/admin-service.ts)
- Modify `approveWatchRequest` to accept `addedViews?: number` and send it in the JSON body.

#### 2. Admin Panel (`frontend/src/app/admin`)

##### [MODIFY] [WatchRequestsPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/watch-requests/WatchRequestsPageClient.tsx)
- Render the `Approve` and `Reject` buttons or a `تعديل القرار` (Edit Decision) button for resolved rows (status != 0).
- In the Approve modal, add a number input for `عدد المشاهدات الإضافية المراد زيادتها` (default 1).
- Add an Edit Decision modal that allows changing status between Approved/Rejected, entering a reason, and specifying added views (if approved).

#### 3. Student Lesson Page (`frontend/src/components`)

##### [MODIFY] [SecureVideoPlayer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/video/SecureVideoPlayer.tsx)
- Accept `lessonPrice?: number` and `lessonId?: string` props.
- If `status === 'locked'` and `lessonPrice !== undefined && lessonId`, display a prominent button: `شراء الحصة مجدداً (${lessonPrice} ج.م)`.
- On click, prompt confirmation, call `balanceService.purchaseContent('Lesson', lessonId)`, and reload the page (`window.location.reload()`) upon success.

##### [MODIFY] [LessonCarousel.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/lessons/%5BlessonId%5D/components/LessonCarousel.tsx)
- Accept `lessonPrice` and `lessonId` in props, passing them to `<SecureVideoPlayer>`.

##### [MODIFY] [LessonViewer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/content/LessonViewer.tsx)
- Pass `lesson.price` and `lesson.id` to `<LessonCarousel>`.

---

## Verification Plan & Quickstart
Our verification is outlined in `quickstart.md` (or the "Verification" sections of features).

### Automated Tests
- Build backend and run tests:
  `cd backend && dotnet build && dotnet test`
- Build frontend to ensure TypeScript compiles:
  `cd frontend && npm run build` (or verify pages load without lint errors)
