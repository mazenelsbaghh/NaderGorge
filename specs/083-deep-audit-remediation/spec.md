# Feature Specification: Deep Technical Audit Remediation

**Feature Branch**: `083-deep-audit-remediation`  
**Created**: 2026-06-06  
**Status**: Draft  
**Input**: User description: "حل كل المشاكل دي تفصيليا docs/deep-technical-audit-2026-06-06.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Restore Critical Workflows Safely (Priority: P1)

Admins, teachers, and students can use the highest-impact workflows from the audit without broken requests, privilege gaps, or route mismatches.

**Why this priority**: The audit identifies broken worker monitoring, QR code redemption, and homework submission as immediate workflow blockers or authorization risks.

**Independent Test**: Validate worker monitoring from an authorized staff session, QR redemption from a logged-in student session, and homework pending/submit flows. Unauthorized or wrong-role users must be rejected with clear failure states.

**Acceptance Scenarios**:

1. **Given** a staff user with the required role, **When** they view or control AI worker jobs, **Then** requests include the real user authorization and server-side checks prevent non-staff access.
2. **Given** a logged-in student opens a QR redemption link, **When** the student is already authenticated in the current browser, **Then** the code redemption completes or lands on the student redemption flow without requiring a nonexistent cookie.
3. **Given** a student has pending homework, **When** the student opens and submits homework, **Then** the frontend calls the existing homework endpoints and receives the correct result.
4. **Given** a user with Assistant role only, **When** they open admin screens, **Then** visible actions match backend permissions and unavailable actions are not shown as usable controls.

---

### User Story 2 - Protect Financial, Access, and Watch State from Race Conditions (Priority: P1)

Students cannot gain duplicate access, overspend balance, or bypass watch restrictions by sending repeated or forged requests.

**Why this priority**: Code redemption, balance purchase, and watch tracking directly affect paid access and academic progression.

**Independent Test**: Run concurrent redemption, purchase, and watch-progress scenarios. Only one eligible state change may succeed, balances must remain consistent, and watch limits must lock at the intended threshold.

**Acceptance Scenarios**:

1. **Given** one unused access code, **When** two students or sessions redeem it at the same time, **Then** only one redemption grants access and the other receives a clear already-consumed response.
2. **Given** a student balance that covers only one purchase, **When** two purchase requests run at the same time, **Then** only one purchase succeeds and the final balance is correct.
3. **Given** a student reports video watch progress, **When** the reported seconds exceed a plausible playback delta, **Then** the accepted progress is capped or rejected and the stored watch state remains truthful.
4. **Given** a video has a maximum watch count, **When** the student reaches the limit, **Then** the video locks at the limit without granting an extra full watch.

---

### User Story 3 - Harden Video, Worker, Internal, and Operations Surfaces (Priority: P2)

Sensitive operational and playback surfaces avoid leaking secrets, trusting unauthenticated messages, or relying on unsafe defaults.

**Why this priority**: These issues may not block every user immediately, but they increase security exposure and make operations harder to diagnose.

**Independent Test**: Verify video embed sessions do not expose reusable secrets, local messages validate origin, worker logs redact sensitive payloads, queue history remains available long enough for support, and destructive test endpoints are impossible to invoke outside allowed test conditions.

**Acceptance Scenarios**:

1. **Given** a video player is embedded, **When** inspecting URL history or request logs, **Then** no reusable playback key or decryptable token is exposed in query parameters.
2. **Given** a browser window or iframe sends a forged playback message, **When** its origin is not trusted, **Then** the player ignores the message.
3. **Given** worker jobs process AI or media data, **When** logs are emitted, **Then** raw student content, raw AI responses, and sensitive media URLs are redacted unless explicitly running in debug diagnostics.
4. **Given** a deployment uses default configuration, **When** it starts outside development, **Then** long-lived token defaults, unsafe test modes, and missing secrets fail closed or are replaced by safe defaults.

---

### User Story 4 - Improve Quality Gates and Product UI Consistency (Priority: P3)

The team can detect regressions earlier and keep admin/student UI aligned with Massar Academy's product design system.

**Why this priority**: The audit flags maintainability, test coverage, and product UI drift that increase future bug risk.

**Independent Test**: Run the documented quality commands, inspect endpoint inventory freshness, run Python smoke tests from a reproducible environment, and verify touched UI follows the product register: Arabic-first, dense where needed, accessible, responsive, and not card-heavy.

**Acceptance Scenarios**:

1. **Given** endpoint inventory is regenerated, **When** endpoints change, **Then** the test gate detects stale inventory before merge.
2. **Given** a fresh local setup, **When** the documented test command runs, **Then** Python dependencies install or are already available and tests execute.
3. **Given** admin/student screens are touched by remediation, **When** reviewed at mobile and desktop widths, **Then** controls have visible focus, readable contrast, stable dimensions, and no avoidable nested-card or glass decoration.
4. **Given** frontend services model admin data, **When** TypeScript checks run, **Then** critical DTOs avoid unsafe catch-all types where explicit shapes are available.

### Edge Cases

- An authenticated non-staff user attempts to call worker control routes directly.
- A logged-in student opens a QR link in a new tab with client-side storage available but no server cookie.
- Two requests redeem the same code or spend the same balance within the same second.
- A video progress request is delayed, retried, or reports seconds larger than real elapsed time.
- A worker job completes or fails before an admin opens the monitor.
- The application starts in a production-like environment with missing secrets, unsafe test mode, or overly long token lifetime.
- A Docker first-run environment has no pre-created named volumes.
- A browser sends playback messages from an unexpected origin or after iframe replacement.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Staff-only worker monitoring and control MUST require a verified authenticated staff user, not merely the presence of an authorization header.
- **FR-002**: Frontend worker calls MUST use a shared request helper or equivalent path that attaches the active user authorization consistently and handles unauthorized states.
- **FR-003**: Student QR redemption MUST work with the current client-side authenticated session and MUST not depend on a cookie that the login flow does not create.
- **FR-004**: Homework pending and submission flows MUST call the existing homework routes and MUST surface actionable errors when the backend rejects a request.
- **FR-005**: Admin navigation and action visibility MUST align with backend role permissions so unsupported actions are hidden or disabled for assistants.
- **FR-006**: Code redemption MUST be atomic so a code cannot be consumed successfully more than once under concurrent requests.
- **FR-007**: Balance debit and content purchase MUST be atomic so duplicate concurrent purchases cannot overspend a student balance or create duplicate grants.
- **FR-008**: Watch progress tracking MUST validate plausible progress against server-side state and reject or cap forged client seconds.
- **FR-009**: Watch limit enforcement MUST lock when the configured maximum is reached according to a documented rule.
- **FR-010**: Playback messaging MUST validate trusted origins and avoid wildcard messaging for local embed coordination.
- **FR-011**: Video embed access MUST avoid placing reusable secrets or decryptable access material in browser-visible query strings.
- **FR-012**: Video session consumption MUST not permanently consume a session before playback is established or recoverable progress begins.
- **FR-013**: Internal callback authorization MUST be represented as a formal policy/filter or equivalent auditable control, not only hidden method logic.
- **FR-014**: Destructive E2E/testing endpoints MUST remain unavailable outside explicitly allowed test environments with configured strong secrets.
- **FR-015**: Worker logging MUST redact sensitive payloads, raw AI content, and media URLs by default.
- **FR-016**: Worker job history MUST remain diagnosable long enough for admin/support workflows.
- **FR-017**: Production-like defaults MUST avoid overly long access token lifetimes and unsafe secret fallbacks.
- **FR-018**: Redis and Docker local setup MUST be documented or configured so first-run and native development use predictable ports and volumes.
- **FR-019**: Password reset policies MUST use the same minimum strength as registration unless a stricter policy applies.
- **FR-020**: Endpoint inventory, Python smoke tests, and frontend/backend/worker build checks MUST be runnable from documented commands.
- **FR-021**: Critical frontend service DTOs touched by this remediation MUST use explicit TypeScript shapes instead of avoidable unsafe catch-all types.
- **FR-022**: Student-facing theme tokens touched by this remediation MUST use student/surface semantics rather than admin-only naming.
- **FR-023**: UI touched by this remediation MUST follow the Massar product register: Arabic-first, readable, responsive, keyboard-accessible, restrained colors, and no decorative glass/card-heavy additions.

### Key Entities *(include if feature involves data)*

- **Access Code**: A redeemable paid or academic entitlement that can transition from unused to consumed exactly once.
- **Student Balance**: The student's spendable credit with transactional debit history.
- **Student Access Grant**: A permission record created after eligible redemption or purchase.
- **Video Watch Event**: Per-student video state including seconds watched, watch count, lock state, and last progress timestamp.
- **Video Playback Session**: Short-lived authorization context for a student/video playback attempt.
- **Worker Job**: Background AI/media/notification job visible to authorized staff with status, retry, cancel, and diagnostic metadata.
- **Endpoint Inventory**: Generated review artifact that maps backend endpoints to authorization and test coverage expectations.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All P0 audit workflows pass manual or automated verification: worker monitor, QR redemption, and homework pending/submit.
- **SC-002**: Concurrent duplicate redemption and duplicate purchase tests show at most one successful state-changing outcome.
- **SC-003**: Watch progress tests prove forged or excessive client seconds cannot unlock progression faster than plausible playback.
- **SC-004**: Production-like configuration no longer defaults to multi-year access token lifetimes or permissive destructive test endpoints.
- **SC-005**: Worker diagnostics remain visible for support after completion/failure and logs do not include raw AI responses or sensitive URLs in normal mode.
- **SC-006**: Frontend lint/build, backend build/test, worker build, endpoint inventory check, and Python smoke-test setup commands complete successfully or document an environment blocker with remediation.
- **SC-007**: Any touched UI meets WCAG AA contrast for body text, has keyboard-visible focus, and shows no horizontal overflow at 375px, 768px, 1024px, and 1440px review widths.

## Assumptions

- The audit file is the source-of-truth backlog for this remediation run.
- Existing authentication remains JWT-based for this iteration; full refresh-token migration to HttpOnly cookies may be planned or partially staged if it exceeds this remediation's safe scope.
- Existing PostgreSQL relational state remains the authority for codes, balances, purchases, and watch state.
- Existing Next.js routes, backend API paths, and Docker surface separation are preserved unless a change is required to close an audited risk.
- Existing Massar Academy brand/product context overrides generic design-system recommendations where they conflict.
