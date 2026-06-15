# Requirements Checklist: watch-requests-refinements-and-repurchases

**Purpose**: Verify all functional requirements for watch requests timezone fixes, decision updates, and student lesson repurchases are implemented and tested.
**Created**: 2026-06-16
**Feature**: [spec.md](../spec.md)

## Timezone Fixes
- [ ] CHK001 Update `formatDate` and `formatRelativeDate` in `admin-utils.ts` to append 'Z' to raw unspecified ISO strings.
- [ ] CHK002 Verify that relative dates show "منذ ثوانٍ/دقائق" for newly created entries in Egypt local timezone.

## Admin Decision Editing & Custom Views
- [ ] CHK003 Modify `ApproveWatchRequestCommand` and `RejectWatchRequestCommand` to support changing status of already resolved requests.
- [ ] CHK004 Modify `ApproveWatchRequestCommand` to accept `AddedViews` and increase watch limits accordingly.
- [ ] CHK005 Modify `RejectWatchRequestCommand` to decrement custom watch limits when changing a request from Approved to Rejected.
- [ ] CHK006 Add "تعديل القرار" button and custom edit modal in `WatchRequestsPageClient.tsx`.
- [ ] CHK007 Add number input for custom extra views in Approve and Edit modals.

## Student Lesson Repurchase
- [ ] CHK008 Modify `PurchaseContentCommand` to bypass "already purchased" and "covered by parent" checks if lesson is eligible for repurchase.
- [ ] CHK009 Upon successful repurchase, reset watch statistics (count, locked, custom limit, time watched) for all videos in the lesson.
- [ ] CHK010 Delete all extra watch requests for the lesson's videos on repurchase.
- [ ] CHK011 Pass `lessonPrice` and `lessonId` down through `LessonViewer` -> `LessonCarousel` -> `SecureVideoPlayer`.
- [ ] CHK012 Render "شراء الحصة مجدداً" button in `SecureVideoPlayer` when the video is locked, prompting confirmation and handling the purchase flow.
