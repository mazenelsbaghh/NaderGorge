# Research: Uploads, Assets, Worker, and Infrastructure Security

## Decision: Centralize upload validation around file signatures, extension normalization, and safe storage names

**Rationale**: `AdminController.UploadResourceFile` currently trusts `file.ContentType` and stores `Guid_originalName` directly under `wwwroot/uploads/resources`. `AdminSalesTemplatesController.UploadBackgroundImage` checks `ContentType` and extension before writing to public uploads. Content image flows already use ImageSharp conversion to WebP, which is safer but still needs tests for spoofing and null `WebRootPath` handling. A small backend upload guard avoids duplicated ad hoc MIME checks and lets tests cover spoofed payloads.

**Alternatives considered**:
- Keep per-controller validation: rejected because it duplicates unsafe checks and misses resource uploads.
- Add antivirus scanning: deferred because Phase 3 requires magic bytes/extension validation; malware scanning is a larger infrastructure addition.
- Move all public image assets out of `wwwroot`: rejected for now because public content images are intentional and compatibility matters.

## Decision: Treat lesson resource uploads as protected downloads, not broad public static files

**Rationale**: `PublicController.DownloadResource` already supports signed token download and `X-Accel-Redirect`, but uploaded resources are placed under `wwwroot/uploads/resources` and are directly reachable through static files/Nginx assets. Phase 3 requires either moving untrusted resources outside `wwwroot` or serving through a controller with `Content-Disposition: attachment`. The least disruptive safe path is: store newly uploaded lesson resources under a non-public application data root or protected subdirectory, store returned URLs/paths as logical protected resource paths, and keep `PublicController.DownloadResource` as the only supported access route. Existing public paths can still be resolved for compatibility if signed download is used.

**Alternatives considered**:
- Keep direct `/uploads/resources/*` and rely on obscurity: rejected because direct public URLs violate the spec.
- Convert all resources to images/WebP: rejected because resources include PDF, Word, Excel, ZIP.
- Require immediate DB migration for every historical URL: deferred; implementation can support both legacy and new path resolution while blocking unauthenticated public access for new uploads.

## Decision: Keep content images public but ensure browser-interpretable unsafe files are converted or rejected

**Rationale**: Package/term/section/question/teacher images are intended public visual assets. `ContentImageStorage` converts images to `.webp` with random names and strips metadata. The plan should preserve this compatibility and only add signature validation/tests where missing. SVG should remain disallowed for user-uploaded images because it is browser-interpretable.

**Alternatives considered**:
- Make all images signed: rejected because it breaks public content presentation and is not necessary for approved public images.
- Allow SVG with sanitization: rejected because sanitization complexity is high and unnecessary.

## Decision: Harden live-support attachments as private attachment downloads

**Rationale**: `LiveSupportAttachmentStorage` stores outside `wwwroot`, which is good. It currently preserves `ContentType` from the client and returns `File(item.Content, item.ContentType, item.FileName)`. Phase 3 requires validation and safe disposition. The storage/service should detect safe type/extension, normalize filename, and return attachment disposition for risky file classes. Tests should cover HTML renamed as image and path traversal names.

**Alternatives considered**:
- Block all attachments except images: rejected unless implementation discovers no business need for documents; live support naturally needs screenshots/documents.
- Serve inline with original content type: rejected because browser-interpretable content can execute or render unexpectedly.

## Decision: Disable or profile-gate Bull Board in production, and enforce worker admin audit/rate-limit

**Rationale**: `worker/src/index.ts` always mounts Bull Board at `/ui` behind `WORKER_ADMIN_TOKEN`, and compose publishes the worker port on localhost. Token-only access is insufficient for production-like surfaces. Add `WORKER_ADMIN_ENABLED` defaulting to false in production, keep enabled in local/E2E when explicit, add an admin middleware with fixed-time token check, per-IP/user token rate limiting, and redacted audit logs. Avoid building a full backend-admin SSO bridge in this phase unless required by tests.

**Alternatives considered**:
- Remove Bull Board completely: rejected because local operations still use it.
- Build full backend JWT auth inside worker: deferred because it couples worker to backend auth and is not required to close Phase 3 if production disables or VPN-gates the UI.

## Decision: Refactor queue ingestion into testable functions and stop deleting existing BullMQ jobs during ordinary ingestion

**Rationale**: Current stream ingestion removes any existing job and clears cancellation markers before `Queue.add`. This violates idempotency and can revive cancelled work. Implement a job ingestion module that computes the BullMQ job id, checks existing job state, skips completed/active/waiting/delayed/cancelled ordinary duplicates, preserves cancellation markers, and only clears cancellation on explicit retry endpoint. The existing retry endpoint already clears cancellation for failed jobs; tests should lock this behavior.

**Alternatives considered**:
- Use BullMQ duplicate job rejection alone: insufficient because the current code removes existing jobs first and cancellation markers are separate Redis keys.
- Always create new unique job IDs: rejected because backend jobId is the intended idempotency key and admin status lookup depends on stable IDs.

## Decision: Add Redis stream recovery using `XAUTOCLAIM`/`XCLAIM` before new reads

**Rationale**: Current loop reads backlog with `XREADGROUP ... 0`, which only processes messages pending for this same consumer and does not claim messages owned by dead consumers. Add a recovery function that periodically runs `XAUTOCLAIM job-stream worker-group <consumer> <minIdleMs> 0 COUNT <n>` and processes claimed messages through the same ingestion function. Tests can use a fake Redis interface or a local mocked command recorder.

**Alternatives considered**:
- Rely on BullMQ stalled job handling: insufficient because messages stuck before BullMQ add remain pending in Redis stream.
- Use `XPENDING` + `XCLAIM`: acceptable fallback, but `XAUTOCLAIM` is simpler on Redis 7 and this project uses Redis 7.

## Decision: Introduce a bounded fetch/exec helper for worker external calls

**Rationale**: `liveSupportCallbackClient` already has 10s timeout. Many other calls use raw `fetch` without timeout: Cobalt API, file download, AI progress callbacks, webhooks, notification sender, birthday script. `geminiService` uses a 60-minute global dispatcher, directly conflicting with Phase 3's long-running timeout requirement. Add `worker/src/services/workerFetch.ts` with bounded timeout, max response bytes where relevant, retry classification, and redacted error messages. Add `execFileWithTimeout` for `yt-dlp`/`ffmpeg` where applicable. Use operation-specific defaults: short callbacks (10s), downloads/provider discovery (30-120s), AI model generation bounded by configurable env (default much lower than 60 minutes but high enough for long lessons, e.g. 10 minutes).

**Alternatives considered**:
- Keep 60-minute timeout for Gemini: rejected because hung calls can pin workers too long.
- Apply one global tiny timeout: rejected because audio/video processing legitimately needs longer than callbacks.

## Decision: Extend AI/download failure classification without exposing raw provider output

**Rationale**: `aiErrors.ts` already classifies AI provider status categories. Extend categories to include timeout, network, download, conversion, callback, and cancellation. Ensure job failure reports use sanitized category/remediation strings instead of raw stderr, URLs, prompts, or provider payloads.

**Alternatives considered**:
- Surface raw errors to admins: rejected due to token/URL/prompt leakage risk.
- Hide all details: rejected because admins need remediation categories.

## Decision: Convert worker logging to structured redacted helpers in production-sensitive paths

**Rationale**: `logging.ts` has redaction helpers but many worker files still use raw `console.log/error` with URLs, job IDs, stderr, or payload-adjacent content. Use `logQueueEvent` and add severity helpers (`info/warn/error`) so production paths emit structured redacted metadata. Keep limited console output in tests/scripts if not production paths.

**Alternatives considered**:
- Add a third-party logger: rejected as unnecessary for Phase 3.
- Replace every console call in the whole worker: deferred; target changed Phase 3 surfaces and high-risk paths.

## Decision: Harden E2E destructive guard with environment, token, and database-name allowlist

**Rationale**: `E2eOnlyAttribute` already requires E2e environment and `X-E2E-Token`; `UsesE2eDatabase` only checks whether the connection string contains `e2e` or `test`. Strengthen by parsing the database name and requiring explicit suffix/prefix such as `_e2e`, `e2e_`, `_test`, or `test_`, with optional `E2E_DATABASE_NAME_ALLOWLIST`. Add tests for production-like names containing misleading text.

**Alternatives considered**:
- Keep substring check: rejected because names such as `latest-prod` can match `test`.
- Require a dedicated env var only: useful but less safe than also checking DB name.

## Decision: Docker/Nginx hardening through explicit production-safe defaults and documented local overrides

**Rationale**: `docker-compose.yml` currently publishes worker port, uses Redis without auth command, healthchecks worker `/health`, and Nginx wildcard CORS on `/secured-assets` and assets domain. Update healthcheck to `/ready`, add non-root worker user, add Redis `requirepass`/AOF/maxmemory policy, make worker port profile/local-only or disabled by default for production-like compose, and restrict protected asset CORS to configured Massar origins. Add TLS termination documentation or 443 config; based on current Nginx file, direct TLS config is absent, so documentation plus required `X-Forwarded-Proto`/HSTS contract is acceptable if production terminates TLS externally.

**Alternatives considered**:
- Force checked-in 443 certificates: rejected because cert storage/renewal is environment-specific.
- Leave Redis unauthenticated because ports are localhost-bound: rejected; container-network access still matters and Phase 3 explicitly requires Redis auth/persistence hardening.
