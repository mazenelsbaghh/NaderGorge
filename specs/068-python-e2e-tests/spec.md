# Feature Specification: Python E2E Integration Test Suite

**Feature Branch**: `068-python-e2e-tests`  
**Created**: 2026-06-01  
**Status**: Draft  
**Input**: User description: "حلل كل الفيتشرز بتاع لبلمشروع كلها كامله و اعمل كل واحهده منها تيست بقي واستهدم subaget الاول و كمل /speckit-all"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Authentication & Device Limits (Priority: P1)

As a student and an admin, I want the system to enforce secure registration (with Egyptian phone numbers and four-part Arabic names), authentication, and device fingerprint locks (maximum 2 active devices) so that academic integrity is protected.

**Why this priority**: Core security gatekeeper. If authentication or device limits can be bypassed, the platform's monetization model is broken.

**Independent Test**: Can be tested by invoking registration, login with varying device fingerprints, and admin-triggered device removal.

**Acceptance Scenarios**:
1. **Given** registration details, **When** the student registers with a non-Egyptian phone or a name of less than 4 space-separated Arabic parts, **Then** the request is rejected with validation errors.
2. **Given** a valid student account, **When** the student logs in from 2 distinct device fingerprints, **Then** both logins succeed and active devices are registered.
3. **Given** a student with 2 active devices, **When** they attempt to login with a 3rd unique device fingerprint, **Then** the login is blocked with a device limit error.
4. **Given** a blocked student, **When** an admin disconnects one of their devices, **Then** the student can successfully log in using the 3rd fingerprint.

---

### User Story 2 - Access Codes, Packages & Balance Redemption (Priority: P1)

As a student and an admin, I want to manage access packages, generate access codes, and redeem codes (which grant package access or add wallet balance) so that registration transactions are fully validated.

**Why this priority**: Enables students to unlock paid educational content and buy lessons using balance or redeem codes.

**Independent Test**: Can be tested by generating code groups, redeeming codes as a student, and asserting package access changes.

**Acceptance Scenarios**:
1. **Given** a Package, **When** an admin generates a code group of 10 codes, **Then** 10 unique codes are persisted.
2. **Given** an unused valid access code for a package, **When** a student activates the code, **Then** they gain active student access grant for the package, and the code status changes to consumed.
3. **Given** a balance access code of value 150 EGP, **When** the student activates it, **Then** their wallet balance increases by 150 EGP and the code is consumed.
4. **Given** an active wallet balance, **When** a student purchases a package, **Then** the balance is deducted and access is granted.

---

### User Story 3 - Playback Session Watch Tracking & Limits (Priority: P1)

As a student, I want my video watch progress tracked, and my access restricted once the maximum watch count is exceeded, unless an extra watch request is approved by an administrator.

**Why this priority**: Essential to limit content piracy and prevent infinite viewing of courses.

**Independent Test**: Can be tested by initiating a video session, periodic watch pings matching thresholds, watch count increments, locking of sessions, and admin overrides.

**Acceptance Scenarios**:
1. **Given** access to a lesson video, **When** the student starts a playback session, **Then** they receive an encrypted token containing claims.
2. **Given** an active video session, **When** the student pings progress, **Then** the accumulated watch time is recorded, and if it exceeds the configured threshold percentage of the video duration, the watch count increments.
3. **Given** a student has reached the maximum watch limit (e.g., 2 watches), **When** they request another session, **Then** the request is blocked and the video status is returned as locked.
4. **Given** a locked video, **When** the student submits an extra watch request and an admin approves it, **Then** the video is unlocked and allows exactly one more watch session.

---

### User Story 4 - Homework & Exam Progression Locks (Priority: P1)

As a student, I want to complete lessons sequentially, taking mandatory homework and exams to unlock subsequent lessons or videos, including grading of essay questions.

**Why this priority**: Core educational flow. Ensures students follow the structured curriculum and pass assessments.

**Independent Test**: Can be tested by verifying lesson lock responses, exam submissions, MCQ grading, and the callback flow for AI essay grading.

**Acceptance Scenarios**:
1. **Given** Video N with an attached exam, **When** the student attempts to access Video N+1 without passing Exam N, **Then** Video N+1 is locked.
2. **Given** an exam submission with MCQ questions, **When** the student submits, **Then** it is graded instantly and unlocks Video N+1 if passing score is met.
3. **Given** an exam submission with essay questions, **When** the student submits, **Then** the attempt is marked as pending grading and subsequent videos remain locked.
4. **Given** a pending essay submission, **When** the AI grading callback is triggered with a passing score, **Then** the attempt transitions to Passed and Video N+1 unlocks.
5. **Given** a lesson with mandatory homework, **When** the student requests the next lesson, **Then** it remains locked until the homework is submitted and passed.

---

### User Story 5 - Interactions, Community timeline & Comments Moderation (Priority: P2)

As a student and assistant, I want to write lesson comments and post on the community timeline (including poll voting), with comments/posts defaulting to pending state until approved by a moderator.

**Why this priority**: Moderating student interaction is required to prevent inappropriate or off-topic posts.

**Independent Test**: Can be tested by creating posts/comments, liking, voting, and checking assistant moderation routes.

**Acceptance Scenarios**:
1. **Given** a logged-in student, **When** they create a community post or lesson comment, **Then** its status is Pending and it is not returned in public timelines.
2. **Given** a pending post or comment, **When** an assistant approves it, **Then** the status becomes Approved and it is returned in timelines.
3. **Given** an approved post, **When** students vote in its poll or toggle likes, **Then** counts increment correctly, enforcing one-vote/one-like constraints.

---

### User Story 6 - Birthday Greetings and Leap Year Sweep (Priority: P2)

As the system, I want to automatically congratulate students on their birthdays on Cairo local time, handling leap-year anomalies, by sending WhatsApp notifications via Evolution API and creating in-app events.

**Why this priority**: Enhances student engagement and retention.

**Independent Test**: Can be tested by setting Cairo timezone birthdays, executing the sweeping script, and asserting generated notifications.

**Acceptance Scenarios**:
1. **Given** Cairo date is March 1st on a non-leap year, **When** the sweep script executes, **Then** students born on February 29th and March 1st are both swept.
2. **Given** a swept student, **When** the script executes, **Then** an in-app notification event is inserted and a WhatsApp API call is prepared.

---

## Edge Cases

- **Concurrent logins**: Multiple rapid requests on the same student account with different device fingerprints must not let device limit exceed.
- **Timer expiration**: If the student's exam timer runs out before they click submit, the API must reject any further answers and record score as 0 with "time expired".
- **Leap year on Cairo offset**: Since Cairo timezone is UTC+2 or +3 (standard Cairo offset is UTC+2, but historically sometimes UTC+3 during DST, current standard is UTC+2), date matches must translate UTC birthdates to local Cairo day before checking.
- **Double code redemption**: Student redeeming the same code twice, or two students redeeming the same code concurrently, must be prevented by transactional database locks.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The test suite MUST be written in Python using `pytest` and `requests`.
- **FR-002**: The test suite MUST leverage `E2eTestingController` helper routes to reset, seed, and populate mock data dynamically.
- **FR-003**: The test suite MUST cover Egyptian phone validation, 4-part name check, and 2-device limits.
- **FR-004**: The test suite MUST cover the entire video watch progress tracking, watch limits, and extra watch request approvals.
- **FR-005**: The test suite MUST cover exam locks, timers, एमसीक्यू grading, and AI callback processing for essay grading.
- **FR-006**: The test suite MUST cover community posts, likes, comments, poll votes, and approval moderation.
- **FR-007**: The test suite MUST cover the birthday sweeps with leap year checks.
- **FR-008**: The tests MUST run in a clean, isolated environment against local/Docker backend in E2E mode, leaving production databases untouched.

### Key Entities

- **E2E Client**: A python class wrapper that stores authorization tokens, attaches device headers, and intercepts responses.
- **Database Fixture**: Pytest fixtures that call `/api/e2e/seed` to prepare a fresh database state before runs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The Python test suite covers 100% of the defined user stories (US1-US6).
- **SC-002**: Total test execution time is under 1 minute for local suite runs.
- **SC-003**: Zero test dependencies exist between individual test files (independent seed resets).
- **SC-004**: Complete test runs return a zero exit code (all assertions pass).

## Assumptions

- The backend is running in `E2e` mode locally or under docker, enabling the `/api/e2e/*` endpoints.
- The `API_CALLBACK_SECRET` is configured as `secretxyz`.
- Direct execution of the daily birthday congratulator script via python subprocess or direct node execution is supported.
