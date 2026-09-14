# Feature Specification: Authentication, Sessions, and Permission Safety

**Feature Branch**: `154-auth-session-permission-safety`  
**Created**: 2026-06-30  
**Status**: Draft  
**Input**: User description: "اعمل Phase 1 من full-platform-defects-remediation-phases-2026-06-29.md باستخدام speckit-all، بالنطاق الكامل: P1-1..P1-4 + P2-1 + P2-5 + P2-17. Parent report token يبقى في URL لكن يكون قصير العمر جدا مع Referrer-Policy."

## Clarifications

### Session 2026-06-30

- No critical specification ambiguity required an additional user question: the user already confirmed full Phase 1 scope and selected the parent-report strategy of short-lived URL token plus strict referrer policy.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stop Invalid Long-Lived Sessions (Priority: P1)

As a platform owner, I need old student and staff sessions to stop working after account state changes so inactive users, reset-password users, role-changed users, and revoked devices cannot continue using stale tokens.

**Why this priority**: This closes the highest-risk session continuation issues before financial, data-integrity, or UI remediation phases.

**Independent Test**: Disable a user, reset a user's password, change a user's role, or revoke a user's device, then verify that previously issued tokens and refresh attempts no longer grant access.

**Acceptance Scenarios**:

1. **Given** an active user has a valid session, **When** an administrator disables the user, **Then** the user's existing access token and refresh token no longer allow protected access or session renewal.
2. **Given** a student has a long-lived access token, **When** the student's password reset version changes, **Then** the old access token is rejected on the next protected request.
3. **Given** a staff user has an access token issued before a role or permission change, **When** the role state changes, **Then** the old token cannot continue using the previous privileges.
4. **Given** a user device is revoked, **When** that device attempts to refresh, **Then** refresh is rejected and no new access token is issued.

---

### User Story 2 - Preserve Cross-Surface Sessions Without Persistent Access Tokens (Priority: P1)

As a user moving between student, teacher, staff, and admin surfaces after login, I need the app to recover my authenticated session from the secure refresh session so redirects do not cause login loops or forced re-login.

**Why this priority**: Cross-surface login currently depends on browser storage that is origin-scoped and exposes access tokens to browser storage risks.

**Independent Test**: Clear browser access-token storage while keeping the refresh session valid, load a protected surface, and verify the app hydrates the authenticated user from the refresh session.

**Acceptance Scenarios**:

1. **Given** browser access-token storage is empty and a valid refresh session exists, **When** the frontend starts on a protected surface, **Then** it attempts session hydration and restores the authenticated user without requiring manual login.
2. **Given** session hydration fails because the refresh session is expired or revoked, **When** the protected surface loads, **Then** the user is redirected to the correct login flow with auth state cleared.
3. **Given** the user receives a permission denial, **When** the response is a forbidden result, **Then** the frontend does not clear the user's authenticated session as if the user were unauthenticated.

---

### User Story 3 - Deny Unknown or Unauthorized Admin Routes (Priority: P1)

As an administrator of staff permissions, I need every admin surface to be denied by default unless a matching permission rule allows it, so staff-like accounts cannot open direct URLs that are missing from the route map.

**Why this priority**: Admin route access is a high-risk business-rule boundary and must not depend on incomplete client-side route lists.

**Independent Test**: Sign in as an assistant or staff account with limited permissions, open mapped and unmapped admin direct URLs, and verify that only explicitly permitted routes are accessible.

**Acceptance Scenarios**:

1. **Given** a staff-like user has admin surface access but lacks a route permission, **When** the user opens the route directly, **Then** the route is denied and no protected page data is shown.
2. **Given** an admin route has no matching permission rule, **When** a non-admin user opens it directly, **Then** the route is denied by default.
3. **Given** a route is visible in admin navigation for a user's permissions, **When** the user opens the direct URL, **Then** the route is allowed consistently with the navigation rule.

---

### User Story 4 - Return Correct Authorization Failures (Priority: P2)

As a signed-in user, I need the system to distinguish between unauthenticated and forbidden actions so I am not logged out when I simply lack permission for an action.

**Why this priority**: Incorrect 401 responses cause confusing logout behavior and weaken audit clarity.

**Independent Test**: Execute a forbidden action while signed in with insufficient permissions and verify that the result is forbidden, not unauthenticated.

**Acceptance Scenarios**:

1. **Given** a signed-in user lacks a required permission, **When** they call a protected action, **Then** the system returns a forbidden outcome rather than an unauthenticated outcome.
2. **Given** an unauthenticated visitor calls the same protected action, **When** no valid session exists, **Then** the system returns an unauthenticated outcome.

---

### User Story 5 - Reduce Parent Report Link Leakage (Priority: P2)

As a parent opening a public student report link, I need the link to work only for a short time and avoid leaking the report token through referrers, while preserving the current token-in-URL product flow.

**Why this priority**: Parent report links can appear in browser history, screenshots, logs, and referrers; this phase must reduce leakage without redesigning the parent login model.

**Independent Test**: Open a valid parent report link during its allowed lifetime, then retry after expiration and verify the report is denied; inspect response/page policy and verify referrer leakage is reduced.

**Acceptance Scenarios**:

1. **Given** a parent report link is valid and unexpired, **When** the parent opens it, **Then** the report loads successfully.
2. **Given** a parent report link is expired, malformed, or invalid, **When** it is opened, **Then** the report is denied without exposing student data.
3. **Given** a parent report page is loaded, **When** it navigates to another resource, **Then** the page uses a strict referrer policy so the token is not sent as a full URL referrer.

### Edge Cases

- Refresh token exists but the linked user record is missing, inactive, or has a changed security version.
- Access token lacks the required version claims because it was issued before this remediation.
- A user has multiple active refresh tokens across devices and one device is revoked.
- Admin navigation and direct route rules disagree.
- Browser storage contains stale access-token data while the refresh session is valid.
- Browser storage is empty and the refresh session is unavailable.
- Parent report token is copied after expiration.
- Forbidden responses occur inside background bootstrapping or route guards.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Student Flow**: Disable a student account, then attempt refresh and open a student page from the same browser. Expected result: access is denied and no new session is created.
- **Manual QA Staff/Admin Flow**: Change a staff user's role or permissions, then open a previously allowed and an unmapped `/admin/*` direct URL. Expected result: old privileges do not continue; unmapped routes are denied.
- **Manual QA Cross-Surface Flow**: Sign in on one surface and redirect to the appropriate target surface with browser access-token storage empty. Expected result: the target surface restores the session through refresh-cookie hydration or asks for login only when refresh is invalid.
- **Manual QA Parent Report Flow**: Open a parent report link during and after its lifetime. Expected result: valid links load, expired links fail safely, and response/page policy prevents full URL referrer leakage.
- **Manual QA Negative Check**: Trigger a forbidden admin action as a signed-in staff user. Expected result: forbidden state appears without clearing the user's session.
- **Docker Acceptance**: `docker compose config -q` must pass; backend and frontend build/test commands selected in the plan must pass or have documented external-service limitations.
- **External Dependencies**: Real production domains and cookie domain behavior require environment-specific QA; local verification may use localhost surfaces and mocked/seeded users.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST reject protected access when the token's user is inactive, missing, or no longer matches the user's current security state.
- **FR-002**: The system MUST invalidate or reject refresh tokens for inactive users.
- **FR-003**: The system MUST invalidate refresh capability when a password reset, role/permission change, or device revocation makes the old session unsafe.
- **FR-004**: The system MUST include enough account-state version information in issued sessions to detect stale long-lived tokens.
- **FR-005**: The frontend MUST attempt secure session hydration from the refresh session when browser access-token storage is empty.
- **FR-006**: The frontend MUST reduce reliance on persistent browser storage for access tokens and keep any temporary fallback explicitly bounded.
- **FR-007**: The admin surface MUST deny `/admin/*` routes by default when no permission rule matches.
- **FR-008**: The admin route permission rules MUST be consistent with the admin navigation source of truth.
- **FR-009**: The system MUST distinguish unauthenticated failures from forbidden failures in both backend responses and frontend handling.
- **FR-010**: Forbidden failures MUST NOT automatically clear the user's authenticated session.
- **FR-011**: Parent report links MUST be short-lived and fail safely after expiration or validation failure.
- **FR-012**: Parent report responses/pages MUST use a strict referrer policy that prevents sending full token-bearing URLs as referrers.
- **FR-013**: The system MUST preserve existing valid login flows for students, teachers, staff, and admins except where a stale or unsafe session must be rejected.
- **FR-014**: The remediation MUST include automated tests for disabled refresh, stale token rejection, permission denial semantics, frontend hydration, admin route denial, and parent report expiration/policy.

### Key Entities *(include if feature involves data)*

- **User Account**: Existing account record whose active status, password reset version, role state, and permission state determine whether sessions remain valid.
- **Refresh Session**: Existing refresh token/session record tied to a user and device that may be revoked or rejected when account state changes.
- **Device Session**: Existing device-level session state used to identify and revoke a user's specific device access.
- **Admin Route Permission Rule**: A route-to-permission mapping that determines whether admin direct URLs are allowed.
- **Parent Report Token**: A signed public report token tied to a student report purpose and expiration window.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of refresh attempts by inactive users are rejected in automated coverage.
- **SC-002**: 100% of tested stale tokens issued before password reset or role/security-state change are rejected within 1 request.
- **SC-003**: 100% of tested unmapped admin routes are denied for non-admin staff-like users within 1 page load.
- **SC-004**: 100% of tested forbidden actions return a forbidden outcome and do not clear the active frontend session within 1 request.
- **SC-005**: 100% of tested bootstrap flows with empty browser token storage and a valid refresh session hydrate the authenticated user within 3 seconds.
- **SC-006**: 100% of tested expired or invalid parent report links fail without exposing student report data within 1 request.
- **SC-007**: All Phase 1 verification commands selected in the plan pass, or any environment-dependent gaps are explicitly documented with manual QA evidence.

## Assumptions

- The existing user, role, refresh token, and device records will be reused rather than replaced.
- If a dedicated role/permission version field does not already exist, the implementation may add the minimum durable versioning needed to invalidate stale sessions.
- Persistent browser access-token storage may remain only as a temporary compatibility fallback if removing it immediately breaks existing surfaces; any fallback must be documented and bounded in the plan/tasks.
- Parent report links will keep the current token-in-URL flow for this phase, with shorter lifetime and referrer-policy hardening rather than cookie exchange.
