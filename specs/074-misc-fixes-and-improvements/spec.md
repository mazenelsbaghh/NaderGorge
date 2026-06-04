# Feature Specification: Miscellaneous Fixes and Improvements

**Feature Branch**: `074-misc-fixes-and-improvements`  
**Created**: 2026-06-04  
**Status**: Draft  
**Input**: User description: Miscellaneous fixes and improvements requested by the user.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - QR Code Scanner Base URL Fix (Priority: P1)

Students scanning printed access codes must be redirected to the actual platform URL, not local development addresses like `0.0.0.0`.

**Why this priority**: High. Prevents local network/docker container URLs from leaking into production printouts, ensuring mobile devices can resolve the URL.

**Independent Test**: Print a QR code locally with `NEXT_PUBLIC_APP_URL` set to a public IP or domain and verify that the QR code encodes the specified domain.

**Acceptance Scenarios**:
1. **Given** an admin generates access codes, **When** they view the printable QR codes, **Then** the QR code encodes the URL with the base path specified by `NEXT_PUBLIC_APP_URL`.
2. **Given** `NEXT_PUBLIC_APP_URL` is not set, **When** rendering QR codes, **Then** the QR code falls back safely to `window.location.origin` (or standard fallback).

---

### User Story 2 - Admin Cancel Package with Refund option & Purchase Method Warning (Priority: P2)

Admins must be able to cancel a student's package (subscription) and optionally refund the purchase price to the student's wallet balance. The action must warn the admin if the student originally purchased the package using an Access Code versus Wallet Balance.

**Why this priority**: High. Essential for managing student access, processing refunds, and tracking whether purchases were made with code vs balance.

**Independent Test**: Try canceling a package purchased by a student with a code, see the warning, choose to refund the balance, and check that the student's balance increases by the package price.

**Acceptance Scenarios**:
1. **Given** a student has a package purchased via Access Code, **When** the admin clicks "Cancel Package", **Then** the system displays a warning "Warning: This package was purchased using an Access Code" and asks "Do you want to refund the price of [X] to the student's wallet?".
2. **Given** a student has a package purchased via Wallet Balance, **When** the admin clicks "Cancel Package", **Then** the system displays a warning "This package was purchased using Wallet Balance" and asks "Do you want to refund the price of [X] to the student's wallet?".
3. **Given** the admin confirms cancellation with refund, **When** the transaction completes, **Then** the package is marked as Canceled in the packages table, and the refund amount is added to the student's wallet balance.

---

### User Story 3 - Arabize "Account Disabled" Message with Reason and Support Number (Priority: P3)

When a student's account is disabled by the admin, the admin should enter a reason. When that student tries to log in, they must see an Arabic error message containing the custom reason and the technical support number.

**Why this priority**: High. Essential for student support and clear communication when accounts are blocked.

**Independent Test**: Disable a student's account in the admin panel with the reason "Violation of terms", attempt to log in as the student, and check that the error message is translated and displays the custom reason and support number.

**Acceptance Scenarios**:
1. **Given** an admin is disabling a student account, **When** they click disable, **Then** a modal prompts them for a "Reason for suspension/disabling".
2. **Given** a disabled student tries to log in, **When** they submit credentials, **Then** they see the message: 
   `الحساب معطل. السبب: [السبب المدخل]. يرجى التواصل مع الدعم الفني على رقم: [رقم الدعم]`

---

### User Story 4 - Sidebar/Navbar Navigation Hover Labels (Priority: P4)

Hovering over navigation items in the sidebar should reveal the name/label of each icon to provide a clearer, more responsive user experience.

**Why this priority**: Medium. Improves desktop navigation and dashboard ease of use.

**Independent Test**: Hover over a collapsed navigation item in the sidebar and verify that its label is revealed with a smooth CSS transition.

**Acceptance Scenarios**:
1. **Given** the sidebar is collapsed, **When** the user hovers over any icon, **Then** the text label is shown next to it smoothly.

---

### User Story 5 - Redirect Logged-In Users from Login Page (Priority: P5)

If an already authenticated user tries to access the `/login` route, they must be redirected automatically to their dashboard or home page rather than seeing the login form.

**Why this priority**: Medium. Prevents double-authentication issues and improves user journey flow.

**Independent Test**: Log in to the platform, manually type `/login` in the browser address bar, and verify that the page immediately redirects to `/student` (for students) or `/admin` (for admins).

**Acceptance Scenarios**:
1. **Given** a student is logged in, **When** they navigate to `/login`, **Then** they are redirected to `/student`.
2. **Given** an admin is logged in, **When** they navigate to `/login`, **Then** they are redirected to `/admin/codes` (or main admin dashboard).

---

### User Story 6 - Align Balance Edit Button and Icon (Priority: P6)

The balance edit button in the admin's student profile page has a misaligned layout where the icon sits on top of the button text. This must be aligned inline and centered.

**Why this priority**: Low. Cosmetic/Visual bug.

**Independent Test**: Open the student profile in the admin view and verify the balance edit button matches the alignment of other action buttons on the page.

**Acceptance Scenarios**:
1. **Given** the student profile page in the admin view, **When** looking at the balance edit button, **Then** the edit icon and text are aligned horizontally and centered inside the button container.

---

### User Story 7 - Increase Rate Limiting Threshold (429 Limit) (Priority: P7)

Increase the rate limit threshold in the backend to prevent users from encountering the `429 Too Many Requests` error too easily during active studying or page transitions.

**Why this priority**: High. Affects user experience and usability.

**Independent Test**: Generate consecutive fast API calls and verify the rate limit threshold is higher before a `429` is triggered.

**Acceptance Scenarios**:
1. **Given** a user is active on the site, **When** they make standard page actions and fetch content, **Then** they do not hit the rate limit threshold (429) under normal learning session usage.

---

### Edge Cases

- **User Story 1**: What happens if the `NEXT_PUBLIC_APP_URL` contains a trailing slash or is formatted incorrectly? The system must sanitize it.
- **User Story 2**: What if a package is canceled but the refund fails due to database concurrency? The cancellation transaction must roll back entirely.
- **User Story 3**: What if no disabling reason was provided? Fall back to a default Arabic reason like "مخالفة شروط الاستخدام".
- **User Story 5**: What if the token is expired but still present? The redirect check should validate token freshness/validity before redirecting.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The frontend QR generation MUST use `process.env.NEXT_PUBLIC_APP_URL` as the base URL if configured, fallback to `window.location.origin` safely, and warn the admin if the domain contains `0.0.0.0` or `localhost`.
- **FR-002**: Backend MUST support canceling a Student Package Purchase, optionally refunding the package cost to the Student's wallet balance, and return the purchase details (wallet vs code) to display a warning.
- **FR-003**: The admin panel UI MUST display a cancel button in the student packages table, prompt for refunding, and display package status (Active / Canceled / Refunded) in the table.
- **FR-004**: The Student/User entity MUST store `SuspensionReason` (or equivalent field).
- **FR-005**: When an admin disables a user, they MUST be prompted to enter a suspension reason, which is sent to the backend.
- **FR-006**: Auth middleware/handlers MUST return a translated message with the support phone number and the custom suspension reason if the account is disabled.
- **FR-007**: The Login page (`/login`) MUST check auth status client-side and server-side (middleware or page check) and redirect authenticated users.
- **FR-008**: The Sidebar component MUST display labels next to icons on hover using CSS animations/transitions.
- **FR-009**: The balance edit button styling MUST align the edit icon and text horizontally.
- **FR-010**: Rate limit configurations in the backend MUST be adjusted upward.

### Key Entities

- **LessonVideo / PackagePurchase / Subscription**: Represents the relationship of student to package. Needs status field (`Canceled`, `Refunded` etc).
- **User / StudentProfile**: Stores user attributes, including `IsActive`, `SuspensionReason`, and `Balance`.
- **SupportNumber**: Retrieved from configuration or application settings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: QR code scanner successfully resolves to the domain specified in `NEXT_PUBLIC_APP_URL`.
- **SC-002**: Admins can cancel a student's package with a single click, see purchase warnings (balance vs code), choose to refund, and watch the student's balance update in real time.
- **SC-003**: Disabled students cannot log in and are presented with a localized error screen showing the specific suspension reason and support phone number.
- **SC-004**: Logged-in users are redirected away from the login page within 100ms.
- **SC-005**: Sidebar hover labels display smoothly on hover.
- **SC-006**: Rate limits allow at least double the previous request threshold before returning 429.

## Assumptions

- We assume a default Support Number is configured in settings or environment variables (e.g. `TECHNICAL_SUPPORT_NUMBER` or similar).
- Existing Next.js auth cookies are used for login checks.
