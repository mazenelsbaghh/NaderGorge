# Feature Specification: watch-requests-refinements-and-repurchases

**Feature Branch**: `132-watch-requests-refinements-and-revocations`  
**Created**: 2026-06-16  
**Status**: Clarified  
**Input**: User description: "ممكن نعمل اللي هو ف الغاء الطلب ينفع نعدلوا و نزود و ينفع لمي الطالب يخلص او يرفض يكون عندو اوبشن يشتري الحصه دي تاني بسعرها يعني يظهرلوا كل حاجه بس البلاير يقولوا اشتري تاني. وفكك من حوار الاغاء"

## User Scenarios & Testing

### User Story 1 - Correct Date Timezone Handling (Priority: P1)
As an Admin viewing the watch requests dashboard, I want to see the relative date/time of the request formatted correctly according to Cairo local time (UTC+3) rather than being offset by 3 hours, so that I can track request times accurately.

**Acceptance Scenarios**:
1. **Given** a watch request is created at UTC time `T`, **When** Cairo timezone is UTC+3 and the current local time is `T + 3 hours`, **Then** the relative date shown is "منذ ثوانٍ/دقائق" (just now/minutes ago), not "منذ 3 ساعات".

---

### User Story 2 - Modify and Add Views in Watch Requests (Priority: P1)
As an Admin, I want to be able to modify my decision on any watch request (changing approved to rejected, or rejected to approved) directly from the requests table, and customize the number of views to add (e.g., +1, +2, +3, etc.) when approving.

**Acceptance Scenarios**:
1. **Given** a watch request was previously `Approved`, **When** the admin clicks the `تعديل القرار` button and selects `Rejected`, **Then** the request status changes to `Rejected`, the student's custom watch limit is decremented by 1 (or reverted to default limit), and the student's video is locked.
2. **Given** a watch request is `Pending` or `Rejected`, **When** the admin approves it or edits it to `Approved`, **Then** the admin can specify the number of views to add (e.g., 2), and the student's custom watch limit is incremented by that number.

---

### User Story 3 - Lesson Repurchase on Lock (Priority: P1)
As a Student, when I exhaust my watch limit on a video (or my watch request is rejected/locked), I want to see everything else on the lesson page normally but have a prominent "Buy again" button inside the video player to repurchase the lesson at its price. Doing so should deduct the price from my balance, reset my watch counts on all videos of this lesson to 0, and unlock them.

**Acceptance Scenarios**:
1. **Given** a student has exhausted their views on a video (or their request is rejected), **When** they open the lesson page and select that video, **Then** the player blocks playback and shows: `تم الوصول للحد الأقصى للمشاهدات` along with a button `شراء الحصة مجدداً (سعر الحصة ج.م)`.
2. **Given** the student clicks the buy button and confirms, **When** they have sufficient balance, **Then** the price is deducted, the `WatchCount` for all videos in this lesson is reset to 0, the `IsLocked` flag is set to false, any existing watch requests for this lesson's videos are cleared, and the videos are immediately playable.

---

## Edge Cases

- **Zero Price Lessons**: If a lesson is free (price = 0), repurchasing is allowed, costs 0 balance, and resets the watch statistics.
- **Insufficient Balance**: If the student has insufficient balance, clicking the button shows an error toast: "رصيدك الحالي لا يكفي لشراء الحصة".

---

## Requirements

### Functional Requirements

- **FR-001**: Date timezone parsing in `admin-utils.ts` MUST append `Z` to raw unspecified ISO strings.
- **FR-002**: `ApproveWatchRequestCommand` and `RejectWatchRequestCommand` MUST allow resolving requests even if their current status is not `Pending`.
- **FR-003**: `ApproveWatchRequestCommand` MUST accept `AddedViews` (default 1) and increment `CustomMaxWatchCount` by that amount.
- **FR-004**: `RejectWatchRequestCommand` MUST decrement `CustomMaxWatchCount` when changing a request from Approved to Rejected.
- **FR-005**: `PurchaseContentCommand` MUST allow lesson repurchases when at least one video in the lesson is locked/exhausted or has a rejected request, bypassing the "already purchased" and "covered by parent" checks.
- **FR-006**: Upon lesson repurchase, the system MUST reset `WatchCount = 0`, `IsLocked = false`, `CustomMaxWatchCount = null`, `TimeWatchedInSeconds = 0` for all videos in that lesson, and delete all `ExtraWatchRequest` rows for these videos.
- **FR-007**: The frontend player `SecureVideoPlayer.tsx` MUST accept `lessonPrice` and `lessonId` props, rendering a repurchase button if the video is locked.

---

## Success Criteria

- **SC-001**: Admins can edit watch requests decisions within 5 seconds of the action.
- **SC-002**: The admin dashboard supports modifying 1 requests status dynamically.
- **SC-003**: If a video is locked, it takes 0 seconds for the "Buy again" option to be displayed to users.
- **SC-004**: Repurchasing resets watch counts for all items in the lesson.
- **SC-005**: Date updates appear within 5 seconds for admins in Cairo local timezone.
