# Feature Specification: E2E Testing Coverage

**Feature Branch**: `004-e2e-testing-all`  
**Created**: 2026-03-23  
**Status**: Draft  
**Input**: User description: "عايز اعمل e2e لكل حاجه علشان يجرب" 

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Auth and Access Flow (Priority: P1)

As a Quality Assurance system, I want to automatically verify that a student can successfully log in and that their device limits and authentication state are respected, so that we ensure security is not compromised.

**Why this priority**: Authentication and device management are the gatekeepers to the system. If they break, the system is fundamentally broken.

**Independent Test**: Can be fully tested by simulating a login request with known credentials and expecting a valid JWT token, followed by a second device attempt.

**Acceptance Scenarios**:

1. **Given** a valid student account, **When** the student logs in with correct credentials, **Then** they should receive an auth token and access the student dashboard.
2. **Given** an invalid password, **When** the student attempts to log in, **Then** they should see an "Invalid phone number or password" error.
3. **Given** a student who has reached their device limit (2 devices), **When** they try to log in from a 3rd new device, **Then** they should be blocked with a device limit error.

---

### User Story 2 - Admin Content Management (Priority: P1)

As an Admin, I want the system to ensure I can create and manage Packages, Sections, Lessons, and Videos smoothly so that students always have access to current educational material.

**Why this priority**: Content generation is the core value driver for the business.

**Independent Test**: Can be independently tested by using an admin account to create a dummy package, section, lesson, and video, and verifying they appear in the student's catalog.

**Acceptance Scenarios**:

1. **Given** an admin dashboard, **When** the admin creates a new Package with a title and price, **Then** the package is created successfully and visible in the admin content list.
2. **Given** an existing Package and Section, **When** the admin creates a Lesson and attaches a Video (Vimeo ID), **Then** the video is properly mapped to the lesson.

---

### User Story 3 - Student Lesson Consumption & Exams (Priority: P1)

As a Student, I want to be able to watch my enrolled videos and take the required exams so that I can progress through my packages.

**Why this priority**: This is the primary interactive flow for the end-user. Ensuring exams and video limits work is critical for monetization and academic integrity.

**Independent Test**: Can be tested by having a test student account consume a video, incrementing their watch count, and taking an attached exam.

**Acceptance Scenarios**:

1. **Given** an unlocked lesson with a video, **When** the student watches the video, **Then** their view count should increment.
2. **Given** a video that has reached its maximum view limit (e.g., 5 views), **When** the student tries to watch it again, **Then** they should be blocked and told their limit is reached.
3. **Given** an exam at the end of a lesson, **When** the student answers questions and submits, **Then** they should receive an immediate score.

---

### User Story 4 - Access Codes and Unlock (Priority: P2)

As an Admin, I want to ensure that generated access codes work perfectly when a student redeems them, so that offline and external sales function correctly.

**Why this priority**: Access codes are a primary monetization method in centers.

**Independent Test**: Can be tested by invoking the admin code generation, taking one code, and redeeming it as a student.

**Acceptance Scenarios**:

1. **Given** a package, **When** the admin bulk generates 10 access codes, **Then** 10 unique codes are created in the database.
2. **Given** an unused valid access code, **When** a student enters it in the redemption page, **Then** the corresponding package/lesson is unlocked for them and the code becomes "consumed".

---

### Edge Cases

- What happens when a student tries to redeem a code that was already used? (Should gracefully show "Code already consumed").
- How does the system handle concurrent UI automated test executions accessing the same user account? (Users and devices could conflict if not isolated properly).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST have an automated test suite that visually and functionally validates the core user journeys (Auth, Content Creation, Content Consumption, Code Redemption).
- **FR-002**: The test suite MUST be able to execute safely without permanently destroying production user data (e.g. running against isolated test accounts or a local/staging DB).
- **FR-003**: The E2E tests MUST interact with the actual browser UI, filling forms and clicking buttons as a real human user would.
- **FR-004**: System MUST capture screenshots or videos if an E2E test fails, to make debugging easier for the developer.
- **FR-005**: System MUST run the chosen E2E UI testing framework using **Playwright**.

### Key Entities

- **Test Sandbox Environment**: An environment containing both the Next.js frontend and C# backend connected to a clean database specifically for automated test runs.
- **Test User Accounts**: Predictable users specifically seeded for automated testing that bypass usual OTP/registration hurdles if any exist.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Achieving 100% automated coverage of the defined P1 user journeys (Auth, Content Management, Student Consumption).
- **SC-002**: E2E test suite completely executes in under 10 minutes locally.
- **SC-003**: Zero "flaky" tests remaining (tests that randomly fail without code changes) across 10 consecutive runs.
- **SC-004**: Critical flows are protected such that any major UI breakage is caught by tests before release.
