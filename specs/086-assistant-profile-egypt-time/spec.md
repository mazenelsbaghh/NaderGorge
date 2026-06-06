# Feature Specification: Assistant Profile & Egypt Timezone Localization

**Feature Branch**: `086-assistant-profile-egypt-time`  
**Created**: 2026-06-06  
**Status**: Draft  
**Input**: User description: "لمي بعمل مساعد بيعملوا كطالب مش مساعد و عايز لروفبل للمساعد بكل حاجه عملها و عايز التوقيت ك؛لو يبقي علي توقيت مصر"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fix Assistant Classification & Routing Bug (Priority: P1)

As an administrator, when I create a custom Assistant account with dynamic roles (e.g., custom permissions or roles like `ntu`, `رفع`), the system must treat them as an Assistant/Staff instead of falling back to a Student. 

**Why this priority**: It is a critical blocking bug (P1). Currently, dynamic assistants are misclassified as students, preventing them from accessing the admin portal and incorrectly routing them to the student portal.

**Independent Test**:
- Create an assistant user with a custom role (e.g. `ntu`).
- Log in as that user and verify they are correctly routed to the `/admin` portal (or staff portal) instead of `/student`.
- Verify they show up in the admin users list with their correct role label (educational assistant / مساعد تعليمي) instead of "طالب" (student).

**Acceptance Scenarios**:
1. **Given** a user has roles that do not contain "Student" (e.g., `["ntu"]`), **When** they log in, **Then** they are recognized as staff and routed to the `/admin` dashboard.
2. **Given** a user is displayed in the admin users list, **When** they have custom assistant roles, **Then** their role column shows "مساعد تعليمي" or their specific role name, not "طالب".
3. **Given** the platform is under maintenance, **When** a custom assistant logs in, **Then** they bypass the maintenance screen just like administrators do.

---

### User Story 2 - Assistant Activity Profile/Audit (Priority: P2)

As an administrator, I want to view a detailed profile drawer or modal for any assistant showing a list of all activities and actions they have performed.

**Why this priority**: High value for accountability (P2). Administrators need to audit what actions assistants have taken (e.g. adjusting balances, resetting device limits).

**Independent Test**:
- Click on an assistant user row in the admin users dashboard.
- Verify that a details/profile modal slides in showing the assistant's metadata and a history of their actions.
- Verify that performing an action (like adjusting a student's balance) is logged and immediately visible in that assistant's activity timeline.

**Acceptance Scenarios**:
1. **Given** the admin is on the users dashboard, **When** they click on an Assistant row, **Then** an Assistant Profile Modal opens.
2. **Given** the Assistant Profile Modal is open, **When** the admin views the "سجل النشاطات" (Activity Log) tab, **Then** they see a chronological timeline of actions performed by that assistant.
3. **Given** an action in the timeline, **When** displayed, **Then** it shows the action name in readable Arabic (e.g., "تعديل رصيد", "تجاوز حد مشاهدة فيديو") with the target student/entity and the exact timestamp.

---

### User Story 3 - Egypt Timezone Localization (Priority: P3)

As a student or administrator, I want all dates and times across the platform (such as exam start/submission times, balance transactions, audit logs, and scheduled tasks) to display strictly in Egypt Timezone (GMT+2/GMT+3/`Africa/Cairo`).

**Why this priority**: Essential for localized user experience (P3). Both students and staff reside in Egypt, so displaying dates in UTC or browser-local time (if outside Egypt) causes confusion.

**Independent Test**:
- Set local computer clock to a different timezone (e.g. GMT/UTC or GMT+1).
- Open the application (student dashboard or admin dashboard) and check formatted dates (e.g. watch requests, comments, or transaction times).
- Verify they match the exact time in Cairo.

**Acceptance Scenarios**:
1. **Given** a date timestamp returned from the database, **When** rendered in the browser, **Then** it is formatted in `Africa/Cairo` timezone.
2. **Given** both server-side pre-rendering (SSR) and client-side mounting, **When** a date is rendered, **Then** both match exactly using Cairo time, preventing hydration mismatch errors.
3. **Given** background worker jobs (like the daily birthday greeting script), **When** scheduled, **Then** they execute relative to Egypt local time.

---

### Edge Cases

- **No roles assigned**: If a user record has an empty roles list, the system should treat them as a Student (fallback default) to prevent unauthorized access to staff portals.
- **Audit logs with no entity association**: When an assistant deletes or performs an action that does not associate with a target database entity, the audit timeline should gracefully display a generic target label instead of crashing or showing empty values.
- **Daylight Saving Time (DST) transitions**: In Egypt, DST shifts the timezone offset between GMT+2 and GMT+3. Formatting utilities must use the IANA timezone ID `Africa/Cairo` to automatically adjust for DST.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST identify staff/assistants by checking if their role list does not contain `'Student'`.
- **FR-002**: System MUST route non-student users (including custom assistant roles) to the `/admin` portal upon successful login.
- **FR-003**: System MUST provide an API endpoint `/api/admin/users/{id}/audit-logs` that retrieves audit records where `PerformedByUserId` matches the given user ID, ordered by timestamp descending.
- **FR-004**: System MUST display an Assistant Profile Modal on the users management page when clicking an assistant row.
- **FR-005**: The Assistant Profile Modal MUST display a user's details and their action history in a timeline format.
- **FR-006**: The frontend MUST wrap global timezone formatting using the `Africa/Cairo` database identifier.
- **FR-007**: The dockerized containers MUST have the OS environment variable `TZ=Africa/Cairo` configured to align background jobs and database timestamps.

### Key Entities

- **AuditLog**: Represents a history item of administrative action.
  - `Action` (string): Action name.
  - `EntityType` (string): Affected entity type.
  - `PerformedByUserId` (Guid): The assistant who did it.
  - `CreatedAt` (DateTime): Date/time of action.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of custom assistant accounts can log in and successfully load their admin dashboard.
- **SC-002**: Under-maintenance login bypass functions correctly for custom assistants.
- **SC-003**: All date and time strings display Egypt local time regardless of user browser local settings.
- **SC-004**: Audit log retrieval completes in under 200ms.

## Assumptions

- We assume assistants only operate through the admin panel, and do not need access to the student panel.
- We assume all background tasks run within the Docker environment, where setting `TZ` will localize cron schedules.
