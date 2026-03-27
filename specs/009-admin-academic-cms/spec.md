# Feature Specification: Phase 2.5: Admin CMS for Homework and Assistants

**Feature Branch**: `009-admin-academic-cms`  
**Created**: 2026-03-26  
**Status**: Draft  
**Input**: User description: "Phase 2.5: Admin CMS for Homework and Assistants"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin can manage Assistant roles (Priority: P1)

Admins need to be able to create or promote existing users to have the "Assistant" role directly from the User Management dashboard, so they dont have to do it through backend seeding.

**Why this priority**: Without this, Nader George's team cannot invite or assign real assistants to grade homework.

**Independent Test**: Can be fully tested by taking a raw phone number, assigning the assistant role, and logging in with that number to see the Assistant Dashboard.

**Acceptance Scenarios**:

1. **Given** an admin is on the User Management page, **When** they click "Add User" and select the "Assistant" role, **Then** an assistant account is created and listed.
2. **Given** an existing student account, **When** the admin edits their role to include "Assistant", **Then** the user gains access to `/assistant/dashboard`.

---

### User Story 2 - Admin can add Homework to Lessons (Priority: P1)

Admins and Content Managers need to be able to attach Textual and MCQ Homework questions to specific lessons directly from the Lesson Detail page.

**Why this priority**: Required to actually serve homework to students via the Content Management platform.

**Independent Test**: Can be fully tested by navigating to a lesson in the Admin panel, adding a mandatory essay homework, saving it, and verifying it appears in the student's lesson view.

**Acceptance Scenarios**:

1. **Given** an admin is viewing a Lesson's details, **When** they navigate to the "Homework" tab, **Then** they see an interface to add or edit existing assignments.
2. **Given** the homework creation form, **When** the admin adds an essay question and sets `IsMandatory` to true, **Then** it is saved and the lesson enforces completion.
3. **Given** the homework list, **When** they click to edit a question's text, **Then** the update is persisted to the database immediately.

---

### User Story 3 - Admin can easily copy the Parent Report Link (Priority: P2)

Admins need a simple, one-click button in the User Management list to copy the Parent Report link for any specific student, to share it easily via WhatsApp.

**Why this priority**: Improves administrative efficiency drastically when handling parent requests.

**Independent Test**: Can be fully tested by generating the link, clicking copy, and pasting it into the browser to successfully load the parent report.

**Acceptance Scenarios**:

1. **Given** the User Management table, **When** an admin views a student record, **Then** there is a "Copy Parent Link" quick action.
2. **Given** the admin clicks the action, **When** the action resolves, **Then** the exact URL format `http://[domain]/parent-report/[studentId]` is copied to their clipboard and they receive a toast confirmation.

---

### Edge Cases

- What happens if an admin tries to create a user with a phone number that already exists? (Should show a friendly warning and suggest updating the existing user).
- How the system handles adding homework to a lesson that already has submissions (Should warn the admin that active students are currently processing this lesson).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow Admins to select "Role" (Student, Assistant, Admin) when creating or modifying users via the CMS.
- **FR-002**: System MUST expose a "Homework" tab in the `admin/content/.../lessons/[lessonId]` interface.
- **FR-003**: System MUST provide a form to capture Homework Question Type (Essay, MCQ), Question Text, IsMandatory flag, and acceptable Points.
- **FR-004**: System MUST allow deleting or updating existing Homework items.
- **FR-005**: System MUST provide a clipboard-copy function mapping to the `/parent-report/[StudentId]` route within the Admin Users Data Table.

### Key Entities

- **User / Assistant**: The entity being managed in the user table, augmented with role dropdowns.
- **Homework**: The assignment metadata being attached to a `LessonId`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Administrators can fully onboard a new Assistant without triggering backend scripts or DB direct access.
- **SC-002**: Admins can add a homework assignment to a lesson in under 1 minute from the Admin Dashboard.
- **SC-003**: Parent links can be retrieved in 1 click per student directly from the directory.
