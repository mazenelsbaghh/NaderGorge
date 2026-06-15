# Feature Specification: Comprehensive Audit Remediation

**Feature Branch**: `[125-comprehensive-audit-remediation]`  
**Created**: 2026-06-15  
**Status**: Draft  
**Input**: User description: "Resolve all deep-audit findings. Teachers may read only their own code groups and codes and must never create codes."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Contain Critical Security and Financial Risk (Priority: P1)

As the platform owner, I need sensitive repository data removed from the active tree and financial/code permissions restricted so leaked data and unauthorized credit creation cannot harm students or platform finances.

**Why this priority**: The current repository contains sensitive operational data, and teachers can create balance-bearing codes. Both issues can cause immediate, material harm.

**Independent Test**: Verify tracked repository content contains no database dumps or production-like credentials, an administrator with the explicit code-generation permission can create valid codes, and every teacher creation attempt is denied while teacher-owned code groups remain readable.

**Acceptance Scenarios**:

1. **Given** a teacher authenticated on the teacher surface, **When** the teacher views code groups, **Then** only groups assigned to that teacher are visible and their codes can be read.
2. **Given** a teacher knows the code-generation endpoint, **When** any code-generation request is submitted, **Then** the request is denied and no group, code, balance, or audit mutation is created.
3. **Given** an administrator without the explicit code-generation permission, **When** code generation is requested, **Then** the request is denied.
4. **Given** repository scanning is executed, **When** tracked files are inspected, **Then** database dumps, refresh tokens, plaintext access codes, personal phone data, and seeded production credentials are absent.

---

### User Story 2 - Enforce Academic and Session Authorization (Priority: P1)

As a student or content owner, I need every exam, homework, unlock, password-reset, refresh, and logout operation to enforce ownership, access, and one-time session rules.

**Why this priority**: Missing access checks expose paid academic content and allow replay or incomplete logout behavior.

**Independent Test**: Exercise authorized and cross-tenant requests for lessons, exams, homework, unlocks, password reset, refresh rotation, and logout, confirming only valid requests succeed.

**Acceptance Scenarios**:

1. **Given** a teacher or assistant not assigned to a lesson or student, **When** manual unlock is requested, **Then** the request is denied with a forbidden response.
2. **Given** an exam attached through any supported content relationship or explicit exam grant, **When** a student starts it, **Then** access is granted only if the corresponding entitlement is active.
3. **Given** a student lacks lesson access, **When** homework submission is attempted, **Then** no submission or gamification event is created.
4. **Given** a password-reset token has already succeeded, **When** it is replayed, **Then** it is rejected.
5. **Given** concurrent refresh requests use the same token, **When** rotation occurs, **Then** exactly one successor session is created.
6. **Given** a user logs out, **When** the old refresh session is used, **Then** it is revoked and cannot restore authentication.

---

### User Story 3 - Preserve Financial and Academic Data Integrity (Priority: P1)

As the platform owner, I need concurrent balance, homework, notification, commitment, and video-tracking operations to produce one consistent result without duplicate or lost state.

**Why this priority**: Lost updates, duplicate rewards, false delivery success, and incorrect watch totals undermine financial and academic records.

**Independent Test**: Run concurrent and boundary-focused tests against realistic persistence and confirm balances, ledgers, submissions, warnings, notifications, and video watch counts remain consistent.

**Acceptance Scenarios**:

1. **Given** concurrent balance adjustments, **When** they complete, **Then** the final balance equals the ordered sum and every ledger row records the correct resulting balance.
2. **Given** duplicate concurrent homework submissions, **When** they race, **Then** exactly one submission and one reward outcome exists.
3. **Given** a video sync crosses multiple configured thresholds within the accepted reporting window, **When** tracking is saved, **Then** the accepted time, watch count, lock state, and remaining time agree.
4. **Given** an inactive student was already warned for the current occurrence, **When** the commitment sweep repeats or multiple workers run, **Then** no duplicate warning is created.
5. **Given** no real notification provider is configured, **When** a delivery job runs, **Then** it fails explicitly or remains pending rather than reporting a false delivery success.

---

### User Story 4 - Make Background Processing Recoverable (Priority: P1)

As an operator, I need queued AI and notification work to survive process crashes and transient failures with bounded retries, idempotent callbacks, and useful readiness signals.

**Why this priority**: The current destructive handoff can permanently lose expensive jobs and one transient failure can require manual recovery.

**Independent Test**: Interrupt processing between enqueue stages, force transient callback failures, repeat callbacks, and restart dependencies; verify jobs recover without duplicate terminal state.

**Acceptance Scenarios**:

1. **Given** a job is accepted by the backend, **When** the worker crashes before processing, **Then** the job remains recoverable and is eventually processed.
2. **Given** a retryable external failure, **When** processing fails, **Then** bounded exponential retries occur before dead-letter handling.
3. **Given** the same completion callback arrives more than once, **When** it is handled, **Then** the resulting state is applied once.
4. **Given** PostgreSQL or Redis is unavailable, **When** readiness is queried, **Then** the affected service is not reported ready.

---

### User Story 5 - Deploy Only Verified, Reproducible Releases (Priority: P1)

As an operator, I need production deployment to consume the exact tested revision, apply its migrations, expose only intended public services, and provide rollback evidence.

**Why this priority**: A failing revision or stale migration image can currently reach production, while internal data services may be reachable outside the private network.

**Independent Test**: Validate workflow dependencies and deployment configuration using a release candidate with a failing test, a new migration, and network-port inspection.

**Acceptance Scenarios**:

1. **Given** any required test or build fails, **When** the revision is pushed, **Then** production deployment does not start.
2. **Given** a revision contains a migration, **When** deployment runs, **Then** the migrator image built from that exact revision runs before the matching application images.
3. **Given** production Compose configuration, **When** host bindings are inspected, **Then** PostgreSQL, Redis, backend, worker administration, and application surfaces cannot bypass the intended reverse proxy.
4. **Given** an unhealthy frontend, worker, queue, or database, **When** deployment verification runs, **Then** deployment fails and the prior release remains recoverable.
5. **Given** a build is repeated from the same revision, **When** dependencies are resolved, **Then** pinned tools and images produce equivalent inputs.

---

### User Story 6 - Deliver Secure, Consistent Frontend Behavior (Priority: P2)

As a user, I need authentication refresh, rich content, profile completion, dialogs, chat, mobile calendars, and landing performance to behave consistently and accessibly.

**Why this priority**: Current inconsistencies can log users out incorrectly, execute stored markup, discard profile data, trap keyboard users outside dialogs, and degrade mobile or real-time use.

**Independent Test**: Run concurrent authentication requests, malicious rich-text rendering, profile persistence, keyboard-only dialog use, mobile viewport checks, and SignalR room switching.

**Acceptance Scenarios**:

1. **Given** multiple requests receive unauthorized responses together, **When** refresh succeeds, **Then** one refresh occurs and all eligible requests retry with the same current session.
2. **Given** stored rich text contains unsafe markup, **When** it is displayed, **Then** unsafe elements and attributes do not execute.
3. **Given** profile completion requires city and school, **When** submission succeeds, **Then** both values are persisted and visible after reload.
4. **Given** a keyboard user opens a dialog, **When** Tab and Escape are used, **Then** focus remains within the dialog, closes appropriately, and returns to the trigger.
5. **Given** a narrow viewport, **When** the social calendar is opened, **Then** content remains readable through an adaptive list or scrollable layout.
6. **Given** a user changes chat rooms, **When** subscriptions change, **Then** the existing real-time connection remains stable.

---

### User Story 7 - Establish Maintainable Quality Gates (Priority: P2)

As a maintainer, I need critical backend, frontend, worker, Docker, and security behavior covered by reliable automated checks and manageable module boundaries.

**Why this priority**: Existing build success does not cover critical session, queue, accessibility, mobile, and concurrency behavior, and oversized modules increase regression risk.

**Independent Test**: Run the documented verification suite from a clean checkout and confirm it catches intentionally introduced permission, queue, session, and deployment regressions.

**Acceptance Scenarios**:

1. **Given** the verification command set, **When** it runs from a clean environment, **Then** backend, frontend, worker, security scan, dependency scan, Docker configuration, and targeted end-to-end tests complete without warnings or failures.
2. **Given** a critical permission or session regression, **When** tests run, **Then** at least one focused regression test fails.
3. **Given** modified production modules, **When** quality review runs, **Then** responsibilities remain focused and no newly changed UI component exceeds the agreed maintainability boundary without documented justification.

### Edge Cases

- A user has multiple roles including Teacher and Admin; code generation follows explicit permission rather than role-name shortcuts.
- A teacher requests another teacher's code group by guessed identifier.
- A code-generation request races role or permission changes.
- An exam is attached directly to a lesson, attached to a video, standalone with an explicit grant, expired, inactive, or associated with inaccessible parent content.
- Balance adjustments include zero, negative, insufficient-funds, and simultaneous debit/credit cases.
- Refresh, reset, logout, and callback requests are duplicated or arrive out of order.
- A worker crashes after durable receipt but before processing or callback.
- Redis, PostgreSQL, the notification provider, or the AI provider fails temporarily or remains unavailable.
- Protected and public assets share similar names or nested paths.
- Deployment includes a breaking migration, failed readiness check, or partial image build.
- Unsafe rich text uses event attributes, script URLs, malformed tags, or nested encoded markup.
- Dialogs contain no enabled controls, and calendar entries contain long Arabic content.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Teacher surface, open teacher codes, confirm owned groups and code details are readable while all generation controls are absent.
- **Manual QA Role/Flow 2**: Admin with generation permission creates each supported code type within configured limits and reviews the audit event.
- **Manual QA Negative Check**: Teacher calls the generation endpoint directly and receives forbidden without data mutation; cross-teacher group and unlock attempts are denied.
- **Manual QA Negative Check**: Student without entitlement cannot start the exam or submit its homework.
- **Manual QA Session Check**: Trigger simultaneous unauthorized requests, refresh once, logout, and confirm the old refresh session cannot restore access.
- **Manual QA Accessibility Check**: Navigate modified dialogs by keyboard and inspect the social calendar at phone width.
- **Docker Acceptance**: Validate Compose, build exact release images, apply migrations from the matching migrator, start services, verify dependency-aware readiness, verify only nginx is publicly bound in production, and execute surface-separation checks.
- **External Dependencies**: Production credential rotation and Git-history rewriting require an approved operator runbook and backups; real notification and AI delivery checks require configured provider credentials.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST remove sensitive database dumps and production-like credential material from the active tracked repository and prevent equivalent files from being tracked again.
- **FR-002**: The system MUST provide an operator-reviewed incident runbook for credential rotation, token/code revocation, and Git-history cleanup without automatically performing destructive history rewriting.
- **FR-003**: Teachers MUST NOT create code groups or access codes of any type through UI, API, worker, or indirect command paths.
- **FR-004**: Teachers MUST be able to list and read only code groups and access codes assigned to their own teacher profile.
- **FR-005**: Code generation MUST require an explicit administrative permission and enforce server-side amount, count, length, target, and expiry constraints.
- **FR-006**: Manual lesson unlock MUST require explicit permission plus caller ownership or assignment to the target lesson and student, except for authorized platform administrators.
- **FR-007**: Starting an exam MUST validate access through every supported lesson, video, package, and explicit exam entitlement relationship.
- **FR-008**: Homework submission MUST validate access to the owning lesson before creating submissions or rewards.
- **FR-009**: Balance adjustments MUST be atomic and keep the balance, ledger, audit, and emitted events consistent under concurrency.
- **FR-010**: Homework submissions MUST be unique per student and homework and remain idempotent under concurrent requests.
- **FR-011**: Password-reset authorization MUST be single-use and invalidate existing refresh sessions after success.
- **FR-012**: Refresh-token rotation MUST atomically consume one token and issue at most one successor.
- **FR-013**: Logout MUST revoke the server-side refresh session, expire its cookie, and clear client authentication state.
- **FR-014**: Frontend refresh MUST use one shared in-flight operation and atomically update storage, application state, roles, permissions, and real-time authentication.
- **FR-015**: Rich HTML MUST be sanitized consistently before privileged or student rendering.
- **FR-016**: Profile completion MUST persist every field required by its UI or cease requiring fields that are not supported.
- **FR-017**: Modified dialogs MUST trap focus, set initial focus, support expected close behavior, make background content inert, and restore focus.
- **FR-018**: Modified calendar and landing media MUST provide responsive, optimized rendering without clipping or unnecessary layout delay.
- **FR-019**: Changing real-time chat subscriptions MUST NOT recreate the transport connection unless authentication or endpoint configuration changes.
- **FR-020**: Accepted video watch time, threshold transitions, lock state, and remaining time MUST remain internally consistent and pass boundary regression tests.
- **FR-021**: Backend-to-worker handoff MUST durably retain accepted jobs until BullMQ or an equivalent acknowledged queue owns them.
- **FR-022**: Retryable worker jobs MUST use bounded attempts, exponential backoff, idempotent callbacks, and explicit dead-letter visibility.
- **FR-023**: Commitment warnings MUST be uniquely keyed per student, reason, and occurrence window to prevent duplicate warnings across repeats or replicas.
- **FR-024**: Notification jobs MUST never report delivery unless a real provider accepted the request; missing providers MUST fail explicitly.
- **FR-025**: Backend and worker readiness MUST verify required dependencies and consumer initialization separately from liveness.
- **FR-026**: Production configuration MUST keep databases, caches, backend internals, worker administration, and application containers off public host bindings except through the approved reverse proxy.
- **FR-027**: Public and protected asset storage MUST be separated so protected files are served only after authorization.
- **FR-028**: Production deployment MUST depend on all required tests and builds, use immutable revision-matched images, apply the matching migrator before application rollout, verify every service, and retain a rollback path.
- **FR-029**: Migration-repair utilities MUST NOT infer successful schema application from partial source inspection or silently write migration history.
- **FR-030**: Build tools and deployable third-party images MUST be pinned to reviewed versions or digests.
- **FR-031**: Known high-severity production dependency vulnerabilities MUST be upgraded, overridden safely, or explicitly blocked from release.
- **FR-032**: Critical permission, session, persistence, queue, readiness, accessibility, responsive, and deployment paths MUST have automated regression coverage.
- **FR-033**: Verification commands and documentation MUST reflect the actual backend, frontend, worker, Docker, and end-to-end toolchain.

### Key Entities *(include if feature involves data)*

- **Role Permission**: An explicit capability assigned to administrative roles, including code generation and manual unlock authority.
- **Code Group**: A batch of access codes assigned to a teacher context; readable by its owner but creatable only by explicitly authorized administrators.
- **Access Entitlement**: The active relationship granting a student access to a package, term, section, lesson, video, or exam.
- **Refresh Session**: A server-managed, single-rotation session associated with a user, token, device fingerprint, expiry, and revocation state.
- **Password Reset Authorization**: A short-lived, single-use authorization associated with a user and unique reset identifier or version.
- **Durable Job**: A background request with stable identity, retry state, acknowledgement, terminal status, and idempotent callback behavior.
- **Warning Occurrence**: A uniquely identifiable commitment warning for one student, reason, and time window.
- **Protected Asset**: A stored file whose retrieval requires authorization and cannot be served by the public static host.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Repository security scans find zero tracked database dumps, refresh tokens, plaintext access codes, or production personal-data exports.
- **SC-002**: 100% of teacher code-generation attempts are denied, while 100% of owned code-group read scenarios continue to succeed.
- **SC-003**: Cross-teacher, unassigned-assistant, and unentitled-student authorization regression tests have zero unauthorized successes.
- **SC-004**: Concurrent financial and submission tests produce exactly one valid final state with no ledger mismatch or duplicate reward.
- **SC-005**: Accepted background jobs survive forced worker interruption and transient failures without silent loss or duplicate terminal updates.
- **SC-006**: A revision with any failing required gate has a 0% chance of initiating the production deployment workflow.
- **SC-007**: Production network inspection exposes only intended reverse-proxy ports and no direct PostgreSQL, Redis, backend, worker-admin, or application port.
- **SC-008**: Authentication refresh, logout, password-reset replay, stored-markup, keyboard-dialog, and mobile-calendar regression suites pass without failures.
- **SC-009**: Backend tests, frontend lint/build, worker tests/build, dependency scans, Docker configuration validation, and selected end-to-end checks complete with zero warnings and zero failures.
- **SC-010**: Operators can follow one documented release and incident procedure without relying on unreviewed repair scripts or inferred migration state.

## Assumptions

- Existing roles and permission storage remain the authority; new sensitive actions use explicit permission checks rather than broad role-name checks.
- Platform administrators may operate across teacher boundaries only when their permission explicitly allows the action.
- Teacher code pages remain available as read-only operational reports.
- Existing active production data is preserved; destructive Git-history rewrite and production credential rotation are operator actions requiring backups and approval.
- The existing public notification feature remains available, but jobs fail explicitly until a real provider is configured.
- Existing content and queue identifiers are reused to make retries and callbacks idempotent.
- Mobile support applies to student and shared administrative interactions touched by this remediation.
