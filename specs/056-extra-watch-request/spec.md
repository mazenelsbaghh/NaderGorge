# Feature Specification: Extra Watch Request

**Feature Branch**: `056-extra-watch-request`  
**Created**: 2026-04-04  
**Status**: Draft  
**Input**: User description: "إضافة زر للطالب لطلب مشاهدة إضافية بعد انتهاء مرات المشاهدة المسموحة، على أن يظهر الطلب للدعم الفني للموافقة عليه."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Requesting Extra Views (Priority: P1)

As a student whose video watch limit has reached the maximum, I want to click a button to request an additional view, so that I can continue studying the material with support's approval.

**Why this priority**: Without this functionality, students are completely blocked from viewing the content and have to manually reach out via external channels. This automates the bottleneck.

**Independent Test**: Can be fully tested by locking a video for a user, verifying the button appears, clicking the button, and ensuring a request is created in the backend.

**Acceptance Scenarios**:

1. **Given** the current video watch status is `isLocked = true` and no pending requests exist, **When** the student views the video player area, **Then** an option/button "Request Extra Watch" is visible.
2. **Given** the "Request Extra Watch" button is visible, **When** the student clicks the button, **Then** a request is submitted and the button changes to "Request Pending".
3. **Given** a request is already pending, **When** the student reloads the page, **Then** the UI still reflects the "Request Pending" state.

---

### User Story 2 - Admin Management of Requests (Priority: P1)

As an admin or technical support staff, I want to view a list of all extra watch requests submitted by students and approve or reject them, so that I can manage student access effectively.

**Why this priority**: The student's request is useless if the technical support team has no way to view and interact with it.

**Independent Test**: Can be fully tested by creating dummy requests in the database and checking if the admin dashboard lists them correctly, and if approving them alters the student's watch limits.

**Acceptance Scenarios**:

1. **Given** multiple students have requested extra watches, **When** the admin navigates to the "Watch Requests" dashboard, **Then** a list of pending requests is displayed showing student name, video title, and requested date.
2. **Given** a pending request, **When** the admin clicks "Approve", **Then** the request is marked as approved, and the student's lock on that specific video is lifted.
3. **Given** a pending request, **When** the admin clicks "Reject", **Then** the request is marked as rejected, and the student remains locked out.

---

### Edge Cases

- What happens when a student requests an extra watch, gets rejected, and tries to apply again? (Should there be a limit or cooldown on requests?)
- How does system handle concurrent approvals by two admins? (Idempotent update handling).
- What happens if the video's global settings change while a request is pending?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a "Request Extra Watch" button on locked video players for which the user doesn't already have a pending or rejected status.
- **FR-002**: System MUST prevent duplicate pending requests for the same user and video combination.
- **FR-003**: System MUST provide an Admin Dashboard view listing all pending extra watch requests.
- **FR-004**: System MUST allow admins to approve a request, which subsequently increases the user's allowed watch counts or resets the locked status.
- **FR-005**: System MUST allow admins to reject a request, updating its state to rejected.
- **FR-006**: System MUST persist the state of extra watch requests (Pending, Approved, Rejected).
- **FR-007**: System MUST notify the student (via UI status text) if their latest request was rejected or is still pending.

### Key Entities *(include if feature involves data)*

- **ExtraWatchRequest**: Represents a student's request to watch a locked video again. Key attributes: `Id`, `UserId`, `LessonVideoId`, `Status` (Pending, Approved, Rejected), `CreatedAt`, `ResolvedAt`.
- **VideoWatchEvent** (Existing): The user's tracking entity that might need to be explicitly modified (e.g. unlocking it) when an `ExtraWatchRequest` is approved.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A student with a locked video can submit a request under 5 seconds.
- **SC-002**: Technical support can view and approve/reject requests directly from the dashboard, replacing external communication workflows.
- **SC-003**: After approval, the student is instantly granted access without needing technical intervention beyond the approval click. 

## Assumptions

- We assume no email / SMS notifications are required for V1 (students will just refresh the page to see if it un-locks).
- We assume admins have a central place in the existing dashboard to add a new "Watch Requests" page.
- We assume unlocking means simply setting `IsLocked = false` or increasing an explicit allowance in the `VideoWatchEvent` record.
