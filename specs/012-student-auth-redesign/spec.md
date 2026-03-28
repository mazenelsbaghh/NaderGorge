# Feature Specification: Student Auth Redesign

**Feature Branch**: `012-student-auth-redesign`  
**Created**: 2026-03-26  
**Status**: Ready for Planning  
**Input**: User description: "Redesign student registration and login page using a provided Stitch UI (User Management - Dark Mode, Screen 65f9b2f134b14b93b4c3b584c04a39f1), removing the short-code step and filling all fields at once with admin-style dark mode theme."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single-Step Student Registration (Priority: P1)

A new student navigates to the registration page. Instead of entering an access code first and then continuing, the student sees a comprehensive registration form containing all necessary fields (Name, Phone, Parent Phone, Grade, Password) on a single page. After submitting the form, their account is created immediately.

**Why this priority**: Streamlined onboarding is critical for user conversion. Removing the multi-step access code requirement reduces registration friction.

**Independent Test**: Can be fully tested by navigating to `/register` and successfully creating a complete student profile in one form submission.

**Acceptance Scenarios**:

1. **Given** a new student on the registration page, **When** they fill out all required fields (Name, Phone, Parent Phone, Grade, Password) and submit, **Then** a new student account is created successfully.
2. **Given** a missing required field or existing phone number, **When** the student attempts to submit, **Then** appropriate validation errors are displayed.

---

### User Story 2 - Dark Mode Admin-Style Login (Priority: P1)

A returning student navigates to the login page. They are presented with a premium, dark-mode login interface that visually matches the Admin dashboard style (using the same design tokens, glassmorphism, and color palette). The layout mimics the "Light Royal Version" Stitch template but strictly utilizes the application's dark theme variables.

**Why this priority**: Consistent, premium theming across all entry points builds trust and improves perceived app quality.

**Independent Test**: Can be tested by visiting the login page and verifying visual fidelity against the admin theme and verifying successful authentication.

**Acceptance Scenarios**:

1. **Given** the login page, **When** it loads, **Then** it must utilize the dark mode admin design tokens, glassmorphism card effect, and layout structure provided in the Stitch template.
2. **Given** valid credentials (Phone, Password), **When** submitted, **Then** the user is authenticated and redirected to their dashboard.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a single-step registration form for students that collects all required profile data upfront (Name, Phone, Parent Phone, Grade, Password).
- **FR-002**: System MUST remove any previous workflows that required inputting an "access code" as a prerequisite for rendering the registration form.
- **FR-003**: System MUST provide a login interface that structurally matches the provided Stitch UI (Phone input, Password input, Remember Me, Forgot Password).
- **FR-004**: System MUST apply the application's dark mode administrative design tokens (`--admin-bg`, `--admin-card`, `--admin-primary`, etc.) to the login UI.
- **FR-005**: System MUST include a seamless transition/navigation link between the Login and Registration pages (e.g., "ليس لديك حساب؟ إنشاء حساب طالب").

### Key Entities

- **User**: The system identity created during registration.
- **StudentProfile**: The profile data mapped during the single-step registration.

### Edge Cases

- What happens if the student registers with a phone number that is already in use? (Should show clear error).
- Validation for proper Egyptian phone number format (starts with 01 and 11 digits).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Registration can be completed in under 2 minutes without external code dependencies.
- **SC-002**: Visual consistency is achieved; the login screen uses 100% of the admin dark mode aesthetic without introducing new conflicting CSS static colors.
- **SC-003**: Students can successfully log in and register using the new unified interfaces without encountering breaking UI bugs on mobile or desktop viewports.
