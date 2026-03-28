# Feature Specification: Admin Student Details Profile

**Feature Branch**: `011-admin-student-profile`  
**Created**: 2026-03-26  
**Status**: Ready for Planning  
**Input**: User description: "عايز لمي اخش ع الطالب ف العمود ادوس عليه يظهرلي كل تفاصيل الطالب بكل حاجه ليه بكل حاجه و اني اذدوا مشاهدات او اي حاجه واديني كل حاجه ممكن نضفها فيه"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Comprehensive Student Overview (Priority: P1)

As an administrator, I want to click on any student row in the table to view their full profile in a dedicated screen or large modal, so I can see all their personal, academic, and financial details in one place without navigating multiple pages.

**Why this priority**: It's the core feature to understand a student's history, current status, and identify issues quickly during support or management tasks.

**Independent Test**: Can be fully tested by clicking a student in the users table and verifying that an organized profile view opens showing correct demographic data, enrolled packages, recent activities, and account status.

**Acceptance Scenarios**:

1. **Given** I am viewing the student users table, **When** I click on a student's row or name, **Then** the student's comprehensive profile interface opens.
2. **Given** the student profile is open, **When** I click through categorized tabs (e.g., Personal Info, Academic Progress, Payments, Devices), **Then** I see the relevant contextual dataset for that specific student.

---

### User Story 2 - Video Views overrides and Adjustments (Priority: P1)

As an administrator, I want to be able to manually increase or reset the video view limits for specific lessons directly from the student's profile, so I can resolve technical issues or grant extensions for specific students quickly.

**Why this priority**: Student view limits are a frequent source of support tickets; fixing them directly from the student's file reduces resolution time drastically.

**Independent Test**: Can be fully tested by opening the student profile, navigating to their academic/lessons area, selecting a video, adding +2 views, and verifying the new limit is reflected immediately.

**Acceptance Scenarios**:

1. **Given** the student's profile is open, **When** I navigate to the "Overrides" or "Content" section and add 2 extra views to a specific lesson, **Then** the system registers the override and the student can immediately watch that lesson again.
2. **Given** a student has reached their limit on a video, **When** I click "Reset Views" on their profile for that video, **Then** their view count is zeroed out and access is reinstated.

---

### User Story 3 - Administrative Actions & Account Controls (Priority: P2)

As an administrator, I want to execute quick administrative actions (like force-logout from devices, deactivate account, adjust gamification points, view audit logs) directly from the student's profile interface, so I can enforce security or reward students effortlessly.

**Why this priority**: Centralizing student-specific actions around the student profile streamlines admin operations and eliminates the need to jump between multiple disjointed management pages.

**Independent Test**: Can be fully tested by opening the profile, clicking "Disconnect Devices" or adding +50 gamification points, and confirming the action takes effect on the student's account.

**Acceptance Scenarios**:

1. **Given** I am on the student profile, **When** I click "Disconnect All Devices", **Then** the student's active sessions are terminated.
2. **Given** a student answered a question creatively, **When** I award them 50 bonus points via the quick actions menu, **Then** their rank and total points instantly update.

---

### Edge Cases

- What happens when an admin tries to view a student that was recently deleted or archived? The system should display a clear "Student not found or archived" state instead of crashing.
- How does the system handle concurrent edits if two admins are overriding views for the same student on the same lesson simultaneously? The last action is recorded, but both actions should appear chronologically in the audit trail.
- What happens if the admin tries to add views to a package the student hasn't bought or doesn't have access to? The system should either alert the admin or automatically enroll them explicitly before adding overrides.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a comprehensive profile view (via dedicated page or large modal) when an admin clicks on a student from the global list.
- **FR-002**: System MUST segment the profile into logical categories: Summary Dashboard, Personal Details, Subscriptions/Packages, Watch History & Progress, Financial/Invoices, Devices, and Gamification.
- **FR-003**: System MUST provide a mechanism to search and select specific lessons/videos to grant view extensions or reset view counts for that specific student.
- **FR-004**: System MUST allow admins to terminate active device sessions directly from the "Devices" tab of the student.
- **FR-005**: System MUST allow admins to activate or deactivate the student's account with a single click and an optional reason.
- **FR-006**: System MUST allow adjusting the student's gamification points (adding/deducting) with note tracking.
- **FR-007**: System MUST maintain and display an audit, showing a timeline of all administrative actions taken on this student by any admin (who did what and when).

### Key Entities

- **Student Profile**: The central record representing demographic, authentication, and status data.
- **Student Package/Enrollment**: Represents courses/packages the student has access to.
- **Video Overrides**: Specific exceptions logged against standard content view limits.
- **Device Sessions**: Active authenticated tokens associated with the student's hardware.
- **Admin Audit Trail**: A chronological log of actions executed by admins affecting the student.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Time to resolve internal support tickets related to "adding views" decreases by at least 40% due to centralized profile actions.
- **SC-002**: Administrators can locate any student's specific detail (e.g. parent phone number or recent device) within 3 clicks from the main users list.
- **SC-003**: 100% of administrative adjustments (views added, points added, devices unlinked) made from the profile are systematically logged in the audit trail.
- **SC-004**: Loading the comprehensive student profile completes in under 1.5 seconds, even for students with extensive history.
