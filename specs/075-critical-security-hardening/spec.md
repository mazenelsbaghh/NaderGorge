# Feature Specification: Critical Security Hardening

**Feature Branch**: `075-critical-security-hardening`  
**Created**: 2026-06-04  
**Status**: Draft  
**Input**: Full implementation of `docs/audit-remediation-phase-1-security-critical.md`.

## User Scenarios & Testing

### User Story 1 - No Default Production Credentials (Priority: P0)

As the platform owner, I need default admin/student seeding to be disabled outside explicit safe test/development modes so a new production database cannot be taken over with known credentials.

**Independent Test**: Start the API without `SeedDefaults:Enabled=true` and verify default users are not created.

**Acceptance Scenarios**:
1. **Given** the API runs in Production, Docker, or any non-Development/non-E2e environment, **When** startup reaches database initialization, **Then** default user seeding is skipped.
2. **Given** the API runs in Development or E2e with `SeedDefaults:Enabled=true`, **When** startup reaches database initialization, **Then** development seed data may be created.

### User Story 2 - Service Callbacks Cannot Use Default Secrets (Priority: P0)

As an operator, I need backend internal callback endpoints and worker admin endpoints to reject missing/default secrets so attackers cannot forge job completion or control queues.

**Independent Test**: Call an internal callback without a valid service token and verify `401`.

**Acceptance Scenarios**:
1. **Given** the API starts without a valid internal callback secret, **When** startup validation runs, **Then** startup fails with a safe error message.
2. **Given** a callback uses `secretxyz`, **When** it reaches the API, **Then** it is rejected.
3. **Given** worker status/cancel/retry/UI endpoints are requested without the configured admin token, **When** the worker receives the request, **Then** it returns `401`.

### User Story 3 - Parent Reports Require Signed Expiring Tokens (Priority: P0)

As a parent, I should only be able to view a report through a signed, expiring link, not by knowing a student GUID.

**Independent Test**: Request the report with `studentId` only and verify it fails; request with a valid token and verify it succeeds.

**Acceptance Scenarios**:
1. **Given** no signed token is supplied, **When** a parent report is requested, **Then** the API rejects it.
2. **Given** a valid signed token for student A, **When** the report is requested, **Then** only student A's report is returned.
3. **Given** an expired or malformed token, **When** the report is requested, **Then** the API rejects it.

### User Story 4 - Rich Text Cannot Execute Script (Priority: P0)

As a student or admin, I need exam/question rich text and video watermark data to render safely so malicious stored HTML cannot steal sessions.

**Independent Test**: Render rich text containing `<img onerror=...>` and verify no script executes and unsafe attributes are removed.

**Acceptance Scenarios**:
1. **Given** question HTML contains script tags or event handlers, **When** it is rendered, **Then** unsafe content is removed.
2. **Given** normal formatting tags like `b`, `i`, `p`, and `ul`, **When** they are rendered, **Then** formatting is preserved.
3. **Given** a student name includes HTML characters, **When** the video watermark renders, **Then** it appears as text only.

### User Story 5 - Student Policy and Test Endpoints Are Safe (Priority: P0)

As a student, I need gamification endpoints to work; as an operator, I need destructive E2E endpoints unavailable outside explicitly authorized E2E execution.

**Independent Test**: Student can access gamification status; `/api/e2e` rejects requests without E2E environment and token.

### User Story 6 - Reset Password Revokes Old Sessions (Priority: P1)

As a user, when my password is reset, old refresh sessions must be revoked so a stolen or old refresh token cannot remain valid.

**Independent Test**: Reset a password and verify existing refresh tokens for that user are revoked.

## Requirements

- **FR-001**: API MUST seed default users only when `SeedDefaults:Enabled=true` and environment is `Development` or `E2e`.
- **FR-002**: API MUST validate required secrets at startup and reject missing, placeholder, short, or known default values in non-development contexts.
- **FR-003**: `RequireStudent` authorization policy MUST be registered and require role `Student`.
- **FR-004**: Internal callback validation MUST not use default fallback secrets and MUST compare tokens safely.
- **FR-005**: Worker admin/status endpoints and Bull Board MUST require a configured service token.
- **FR-006**: Frontend worker proxy MUST allow only explicit safe paths and require an admin bearer token for mutating operations.
- **FR-007**: Parent report endpoint MUST require a signed expiring token tied to the target student.
- **FR-008**: Rich text rendering MUST sanitize HTML through a strict allowlist before `dangerouslySetInnerHTML`.
- **FR-009**: Video embed route MUST inject user-visible watermark data using text-safe APIs or JSON string escaping, never interpolated `innerHTML`.
- **FR-010**: Password reset MUST revoke existing refresh tokens for the affected user.
- **FR-011**: E2E controller MUST reject requests unless environment is `E2e`, an E2E secret is configured, and the request supplies it.
- **FR-012**: Example config files MUST not contain live secrets or default production secrets.

## Edge Cases

- Development may run without production-grade secrets, but known dangerous defaults such as `secretxyz` must still be rejected when used for service authorization.
- Parent report tokens must fail closed on malformed base64, bad signature, missing student id, or expired timestamp.
- Sanitization must not break normal Arabic rich text formatting.
- Worker UI should be unavailable rather than public if no worker admin token is configured.

## Success Criteria

- **SC-001**: Code search finds no active `secretxyz` fallback.
- **SC-002**: API, frontend, and worker builds complete after changes.
- **SC-003**: Backend tests pass or any pre-existing failures are documented.
- **SC-004**: XSS payloads in question HTML are stripped while safe formatting remains.
- **SC-005**: Parent report by raw student id is no longer accepted.
