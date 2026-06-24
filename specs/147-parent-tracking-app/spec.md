# Feature Specification: Parent Tracking System & Mobile Apps

**Feature Branch**: `147-parent-tracking-app`  
**Created**: 2026-06-24  
**Status**: Approved  
**Input**: User description: "Parent Tracking System & Mobile Apps"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Student Tracking Code Web UI (Priority: P1)

As a Student logged into the web dashboard, I want to see my unique 6-character parent tracking code so that I can copy it and give it to my parent.

**Why this priority**: Crucial for bootstrap/discovery of tracking codes. Without this, parents cannot link students.

**Independent Test**: Student logs in, sees a beautiful glassmorphic modal displaying their tracking code. They copy the code, click close, and the modal is dismissed. On page reload, the modal does not reappear. A permanent badge in the header shows the code.

**Acceptance Scenarios**:

1. **Given** a student profile has not acknowledged the tracking code popup (`HasSeenTrackingCodePopup = false`), **When** they log in and access the student shell, **Then** they see a glassmorphic modal with their code, a copy button, and an close button.
2. **Given** a student clicks the close button on the tracking code modal, **When** the backend is called to acknowledge it, **Then** `HasSeenTrackingCodePopup` is updated to `true`, the modal is dismissed, and it does not reappear on subsequent page loads.
3. **Given** any student view, **When** they look at the header next to notifications, **Then** they see a permanent pill showing `رمز المتابعة: [CODE] [Copy Icon]`.

---

### User Story 2 - Mobile App Student Linking and Multi-Student Selection (Priority: P1)

As a Parent using the Android or iOS mobile app, I want to link one or more students using their tracking codes and switch between their academic dashboards.

**Why this priority**: Essential to support parents who have multiple children enrolled. It avoids requiring separate accounts/logins for parents.

**Independent Test**: Parent opens the app, enters a tracking code, and clicks "Link". The app registers the device token for push notifications, retrieves the JWT token and student name, and adds the student. The parent can add a second student and switch between them using a dropdown selector.

**Acceptance Scenarios**:

1. **Given** a parent inputs a valid tracking code in the linking view, **When** they click link, **Then** the app requests a JWT token from `/api/parent/verify-code`, saves the `{ studentId, name, token }` locally, and displays a success message.
2. **Given** a parent has linked multiple students, **When** they tap the student selector, **Then** they can choose which child to view, and the dashboard updates immediately using the child's specific JWT token.
3. **Given** a parent removes a linked student, **When** they confirm the action, **Then** the local token is deleted, and the device registers a logout to unregister the FCM token for that student.

---

### User Story 3 - Academic Tracking Dashboard (Priority: P1)

As a Parent viewing a linked student, I want to see a comprehensive academic dashboard of their grades, attendance, homework, and warnings.

**Why this priority**: This is the core value proposition of the mobile apps, showing detailed student progress.

**Independent Test**: Parent selects a student, and the overview shows attendance stats, video completion rates, detailed exam scores, homework submissions, and warnings.

**Acceptance Scenarios**:

1. **Given** a parent token is valid for a student, **When** they fetch `/api/parent/student-details`, **Then** the API returns detailed student data (attendance completions, exams, homeworks, warnings).
2. **Given** the parent dashboard, **When** the exams tab is selected, **Then** it shows list of exams with title, score, percentage, date, and status (Passed/Failed).
3. **Given** the parent dashboard, **When** the warnings tab is selected, **Then** it shows warnings sorted by date with severity highlighted.

---

### User Story 4 - Push Notifications on Student Events (Priority: P2)

As a Parent, I want to receive push notifications on my phone when my child completes an exam, submits homework, or receives a warning.

**Why this priority**: Keeps parents actively informed of key student milestones and warning events.

**Independent Test**: Student submits an exam, backend registers it, queue worker fires Firebase push notification to all parent devices registered to that student, and parent receives a push notification.

**Acceptance Scenarios**:

1. **Given** a student completes an exam, **When** the backend queue worker processes the notification job, **Then** it fetches all registered FCM tokens in `ParentDeviceTokens` for that student and pushes the message "تم حل اختبار الكيمياء العضوية الشامل بنجاح" via Firebase Admin SDK.

---

### User Story 5 - Automated Mobile Build and Verification (Priority: P1)

As a Developer/QA Engineer, I want the Android and iOS apps to compile, build, and test successfully using simple automation commands.

**Why this priority**: Required by the project guidelines to ensure compilation and unit test health.

**Independent Test**: Run `make build-mobile` or similar command, and the Gradle container compiles Android and the Swift CLI compiles iOS, and unit tests run.

**Acceptance Scenarios**:

1. **Given** the mobile codebases are ready, **When** the Makefile commands are executed, **Then** the Android unit tests pass and app builds in Docker (openjdk/gradle image), and Swift tests pass and app compiles on host.

---

### Edge Cases

- **Invalid Tracking Code**: When parent enters a code that doesn't exist, show "الرمز غير صالح، يرجى التحقق وإعادة المحاولة".
- **Network Offline**: When mobile app has no connection, show cached offline data with a warning banner.
- **FCM Token Refresh**: When parent device token changes, update the registration token in `ParentDeviceTokens` for that student.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Student Web Flow**: Student logs in on web, copies code from popup, closes popup. Refresh to verify popup does not show. Badge in header remains.
- **Manual QA Mobile Flow**: Install parent app, enter valid student code. Verify details are loaded. Switch between two students.
- **Docker Acceptance**: `make build` compiles backend, Next.js, worker. Verification script `validate_run.py` passes.
- **External Dependencies**: Requires a mock or real Firebase service account JSON configuration.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST automatically generate a unique, indexable 6-character uppercase alphanumeric `ParentTrackingCode` for each student profile upon profile creation or bootstrapping.
- **FR-002**: System MUST persist the student's acknowledgement state `HasSeenTrackingCodePopup` in the database.
- **FR-003**: System MUST expose a JWT token service for parents that issues long-lived tokens (e.g. 1 year) containing role `Parent` and the specific `StudentId` claim.
- **FR-004**: System MUST secure all parent-facing endpoints (`/api/parent/*`) to require the `RequireParent` policy.
- **FR-005**: System MUST allow registering and removing multiple FCM device tokens for a student in `ParentDeviceToken`.
- **FR-006**: Student web client MUST display a one-time glassmorphism modal with the tracking code on initial login and a copy pill in the header.
- **FR-007**: Node worker MUST process parent push notifications using BullMQ and Firebase Admin SDK when triggered by backend events (exam completed, homework submitted, warning created).
- **FR-008**: Android application MUST be implemented using Kotlin and Jetpack Compose, supporting multi-student JWT storage and a dashboard switcher.
- **FR-009**: iOS application MUST be implemented using Swift and SwiftUI, supporting multi-student JWT storage, dashboard switcher, and the Liquid Glass design aesthetic.
- **FR-010**: System MUST include Makefile and Docker scripts to automate the compilation and unit testing of both Kotlin (Android) and Swift (iOS) apps.

### Key Entities

- **StudentProfile**: Represents the student. Linked to `ParentTrackingCode` (string) and `HasSeenTrackingCodePopup` (boolean).
- **ParentDeviceToken**: Represents a parent's registered device. Contains `StudentId` (Guid), `DeviceToken` (string), `Platform` (string), and `CreatedAt` (DateTime).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All C# backend tests, Node worker tests, and web frontend builds complete with 100% success rate.
- **SC-002**: Both Android (Kotlin) and iOS (Swift) mobile apps compile and build completely with zero errors in under 3 minutes.
- **SC-003**: Unit tests for both mobile apps run and pass with a 100% success rate.
- **SC-004**: Verification script `validate_run.py` completes successfully.

## Assumptions

- **A-001**: Parents do not have credentials (username/password) in the `Users` table; authorization is purely stateless JWT tokens containing the `StudentId` stored in the mobile app.
- **A-002**: Firebase Admin SDK will use a mock service configuration if the real Firebase JSON credentials are not provided during automated testing.
- **A-003**: The mobile apps target Android SDK 34 / Kotlin 1.9+ (Jetpack Compose) and iOS 17+ / Swift 5.9+ (SwiftUI).
- **A-004**: Host machine has `swiftc` or Swift compiler tools for iOS compilation, and Java/Gradle is built via a Docker container (`mobiledevops/android-sdk-image:34.0.0`) to bypass missing local Java.
