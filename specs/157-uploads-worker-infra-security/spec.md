# Feature Specification: Uploads, Assets, Worker, and Infrastructure Security

**Feature Branch**: `157-uploads-worker-infra-security`  
**Created**: 2026-06-30  
**Status**: Draft  
**Input**: User description: "Phase 3: Uploads, Assets, Worker, and Infrastructure Security from docs/full-platform-defects-remediation-phases-2026-06-29.md, full P1/P2 scope."

## Clarifications

### Session 2026-06-30

- Q: هل نطاق التنفيذ هو Phase 3 كاملة أم P1 فقط؟ → A: Phase 3 كاملة كما في الملف، وتشمل كل بنود P1 وP2 الخاصة بالuploads/assets/worker/infra.
- Q: هل نحافظ على روابط الملفات القديمة أم نغيرها لصالح حماية أقوى؟ → A: اعمل الصح؛ نحافظ على public assets الآمنة قدر الإمكان، لكن protected/private assets تنتقل لمسارات آمنة حتى لو تغيرت الروابط.
- Q: هل توجد أسئلة specification-level حرجة متبقية بعد إنشاء spec؟ → A: لا توجد أسئلة حرجة؛ التفاصيل التقنية مؤجلة لمرحلة التخطيط والفحص.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Safe Upload And Asset Access (Priority: P1)

As a platform operator, I need uploaded files and protected assets to be validated and served safely so that spoofed files, browser-interpretable payloads, and private media cannot be accessed as public static content.

**Why this priority**: File upload and asset exposure risks can directly leak private content or allow unsafe files to be served to browsers.

**Independent Test**: Can be tested by uploading spoofed and allowed files, then attempting public and authenticated access to the resulting asset URLs.

**Acceptance Scenarios**:

1. **Given** a file whose extension or declared type is allowed but whose real bytes are unsafe, **When** an operator uploads it, **Then** the system rejects it with a clear validation failure and stores nothing public.
2. **Given** an allowed uploaded file that is not meant to be public, **When** an unauthenticated browser requests it from a public static path or assets domain, **Then** the request is denied or returns no private content.
3. **Given** an allowed private upload is requested by an authorized user, **When** the user downloads it through the supported route, **Then** it is delivered with safe disposition and the original unsafe filename cannot control browser execution.

---

### User Story 2 - Worker Admin And Job Ingestion Safety (Priority: P1)

As an operations owner, I need worker admin surfaces and job ingestion to be locked down and idempotent so that production queues cannot be inspected or mutated by weak access paths and duplicate or cancelled jobs cannot be revived accidentally.

**Why this priority**: Worker admin endpoints, Bull Board, and queue ingestion have high operational blast radius.

**Independent Test**: Can be tested by checking production-like worker config, unauthorized admin access, duplicate job ingestion, cancellation preservation, and audit/rate-limit behavior.

**Acceptance Scenarios**:

1. **Given** a production-like worker configuration, **When** someone opens Bull Board or worker admin endpoints without valid authorization, **Then** the surface is disabled or denied and the attempt is auditable.
2. **Given** a duplicate queue message for an existing completed job, **When** the worker ingests the message, **Then** it does not remove or recreate the completed job.
3. **Given** a cancelled job has a cancellation marker, **When** duplicate ingestion occurs, **Then** the cancellation marker remains unless an explicit authorized admin retry is performed.

---

### User Story 3 - Worker Readiness, Recovery, And External Failure Control (Priority: P1)

As a release owner, I need worker readiness, Redis recovery, and external calls to fail predictably so that containers do not look healthy before they can process jobs and long-running provider calls do not hang critical processing.

**Why this priority**: False readiness, stuck stream messages, and unbounded external calls can silently stop background processing.

**Independent Test**: Can be tested by simulating dead Redis stream consumers, hung providers, failed callbacks, and Docker healthcheck evaluation.

**Acceptance Scenarios**:

1. **Given** a Redis stream message is pending for a dead consumer, **When** a worker starts recovery, **Then** it claims the pending message and processes or acknowledges it exactly once.
2. **Given** a provider or callback hangs, **When** the configured timeout elapses, **Then** the job fails with a known failure classification and remediation visible to administrators.
3. **Given** the worker container starts before all dependencies and processors are ready, **When** Docker evaluates the healthcheck, **Then** liveness may pass but readiness remains unhealthy until the worker is genuinely ready.

---

### User Story 4 - Production Infrastructure Hardening (Priority: P2)

As a production operator, I need Redis, Nginx, TLS assumptions, and worker container privileges to be hardened so that checked-in deployment paths do not default to unsafe production behavior.

**Why this priority**: Infrastructure defaults can expose data or increase compromise impact even when application behavior is correct.

**Independent Test**: Can be tested by rendering compose config, checking worker runtime user, checking Redis auth/persistence/maxmemory settings, and verifying Nginx asset/TLS behavior or documented termination.

**Acceptance Scenarios**:

1. **Given** production compose variants are rendered, **When** Redis and worker settings are inspected, **Then** Redis requires protected configuration and the worker does not run as root.
2. **Given** protected media requests are proxied through Nginx, **When** origins and paths are evaluated, **Then** CORS is limited to Massar origins and public assets are separated from protected assets.
3. **Given** production traffic terminates TLS outside the checked-in Nginx proxy, **When** an operator reads the deployment contract, **Then** the termination responsibility and required headers are explicit; otherwise a checked-in 443 path exists.

### Edge Cases

- MIME spoofing where extension, declared type, and magic bytes disagree.
- Filenames containing path traversal, executable extensions, HTML/SVG/script-capable content, control characters, duplicate names, or misleading double extensions.
- Private files that already exist under broad static roots before the remediation.
- Browser requests with no auth, wrong role, expired signed URL, or invalid origin.
- Bull Board enabled accidentally in production-like configuration.
- Repeated queue messages after a job completed, failed, or was cancelled.
- Redis pending messages owned by consumers that no longer exist.
- Provider calls that hang, reset, return partial content, or repeatedly fail.
- E2E destructive controller pointed at a non-test database.
- Logs containing URLs, tokens, operational details, or provider payload fragments.
- Docker/compose variants with missing Redis password, missing persistence, or root worker user.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin uploads allowed and rejected resource files; allowed public files remain accessible only through expected public paths and private files require authorization or safe download.
- **Manual QA Negative Check**: Unauthenticated browser direct access to protected assets and worker `/ui` is denied in production-like configuration.
- **Docker Acceptance**: `docker compose config -q`; worker healthcheck uses `/ready`; worker runtime user is not root; production-like compose does not publish worker admin port; Redis hardening settings are present.
- **External Dependencies**: AI/download providers, Redis, PostgreSQL, and any real production TLS terminator are needed for full operational validation. When real providers are unavailable, tests must use deterministic hung/failing provider doubles.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST validate uploaded resources using normalized extension, declared type, and file signature bytes before accepting storage.
- **FR-002**: The system MUST normalize stored filenames and extensions so user-provided names cannot create path traversal, executable/browser-interpretable public content, or unsafe response headers.
- **FR-003**: The system MUST serve untrusted or protected uploaded files outside broad public static exposure, or deliver them through authorized download routes with safe content disposition.
- **FR-004**: The system MUST separate public assets from protected assets in storage and proxy rules.
- **FR-005**: The system MUST protect private media using authenticated or signed access and deny direct unauthenticated public asset-domain access.
- **FR-006**: The system MUST restrict protected-media CORS to approved Massar origins and avoid wildcard CORS on protected assets.
- **FR-007**: The worker MUST disable Bull Board and equivalent admin UI surfaces in production by default unless they are behind approved admin authentication or private network controls.
- **FR-008**: Worker admin endpoints MUST enforce authorization, rate limiting, and audit logging for denied and successful sensitive actions.
- **FR-009**: Production-like compose configurations MUST NOT publish worker admin ports by default.
- **FR-010**: The E2E destructive controller MUST fail fast unless the target database name or configured guard clearly identifies a test/E2E database.
- **FR-011**: Redis stream consumers MUST recover pending jobs from dead consumers using a claim/reclaim mechanism and avoid duplicate processing side effects.
- **FR-012**: Queue ingestion MUST treat the incoming job identifier as an idempotency key and MUST NOT remove existing completed, active, or cancelled queue jobs during ordinary ingestion.
- **FR-013**: Cancellation markers MUST remain intact during duplicate ingestion and may be cleared only during an explicit authorized admin retry path.
- **FR-014**: Worker container healthchecks MUST use readiness for dependency and processor readiness while keeping liveness checks separate.
- **FR-015**: All long-running worker external fetch, provider, and callback operations MUST use a bounded timeout and a consistent retry/failure-classification policy.
- **FR-016**: AI/download/provider failures MUST be classified into operator-visible remediation categories without exposing sensitive provider details.
- **FR-017**: Worker logs MUST be structured and redact tokens, credentials, sensitive URLs, payload fragments, and unnecessary operational details.
- **FR-018**: Redis compose variants intended for production or production-like operation MUST include authentication, persistence, and bounded memory policy controls.
- **FR-019**: The worker container image MUST run the application process as a non-root user.
- **FR-020**: Checked-in Nginx/deployment configuration MUST either define HTTPS/TLS termination for direct production use or explicitly document external TLS termination and required forwarding/security headers.

### Key Entities

- **Uploaded Asset**: A file submitted by an admin, participant, worker process, or content workflow; includes original filename, normalized stored name, declared type, detected type, public/private classification, and safe delivery behavior.
- **Protected Asset Access**: An authorized or signed request for private media or attachments; includes requester identity, origin, expiration, and disposition behavior.
- **Worker Admin Surface**: Bull Board and any worker HTTP endpoint capable of exposing operational state or mutating queues.
- **Queue Job Ingestion Event**: A Redis stream message or queue request with a stable job identifier, job type, payload, cancellation state, and processing outcome.
- **Worker Readiness State**: A runtime signal that dependencies, queue processors, provider configuration, and callback checks are ready for work.
- **External Provider Failure**: A classified failure from downloads, AI provider calls, callbacks, or other external IO with operator-safe remediation.
- **Production Compose Security Setting**: Runtime configuration for Redis, worker user, exposed ports, persistence, memory policy, and TLS/proxy assumptions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of spoofed upload samples in the feature test set are rejected before public storage.
- **SC-002**: 100% of private/protected asset direct public requests in the feature test set return no private content.
- **SC-003**: Unauthorized worker admin and Bull Board requests are denied in production-like configuration and generate an auditable signal.
- **SC-004**: Duplicate completed or cancelled job ingestion test cases produce no job recreation and preserve cancellation state.
- **SC-005**: Redis pending-message recovery test cases process reclaimed messages no more than once.
- **SC-006**: Hung callback test cases fail within 10 seconds and hung provider/download test cases fail within the configured bounded timeout while exposing a known remediation category.
- **SC-007**: Docker/compose checks confirm readiness healthchecks, non-root worker runtime, Redis hardening controls, and no default production worker admin port exposure.
- **SC-008**: Full feature verification completes with backend, worker, Docker, and applicable frontend checks documented in the run evidence in under 30 minutes on a prepared local development environment.

## Assumptions

- Public marketing/content assets should remain compatible where they are already safe and intentionally public.
- Protected/private uploads may move to new authenticated, signed, or attachment-only paths if keeping old direct public links would be unsafe.
- Existing authentication and role systems remain the source of truth for protected asset and admin-worker access.
- Existing Redis/BullMQ infrastructure remains in use; this feature hardens ingestion, recovery, and cancellation behavior rather than replacing the queue system.
- TLS may terminate at an external production proxy if the checked-in deployment contract states that requirement clearly.
