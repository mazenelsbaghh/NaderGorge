# Feature Specification: Vertex AI Worker Migration

**Feature Branch**: `139-vertex-ai-worker-migration`
**Created**: 2026-06-18
**Status**: Ready for production completion
**Input**: Migrate every Gemini-backed worker operation to a primary cloud AI provider, use temporary managed object storage for long audio, and fall back to the existing developer API only when the primary provider quota is exhausted.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Video Chapters Reliably (Priority: P1)

As an authorized staff member, I can start video analysis and receive the same transcript and ordered chapter results while the platform uses the primary cloud AI allowance and removes temporary audio after processing.

**Why this priority**: Video chapter generation is the workflow currently blocked by the existing provider billing and file-upload constraints.

**Independent Test**: Start analysis for a lesson video containing speech, allow the job to complete, and verify that subtitles and ordered chapters are persisted while no temporary audio remains after the terminal job state.

**Acceptance Scenarios**:

1. **Given** a supported lesson video and valid primary-provider configuration, **When** an authorized staff member starts analysis, **Then** the job completes with a transcript and ordered chapters compatible with the existing lesson experience.
2. **Given** an analysis job that completes successfully, **When** cleanup runs, **Then** its temporary audio is removed without deleting the generated transcript or chapters.
3. **Given** an analysis job that fails or is cancelled, **When** the job reaches its terminal state, **Then** its temporary audio is removed and the existing failure or cancellation state is preserved.

---

### User Story 2 - Run All Worker AI Features Through One Primary Provider (Priority: P1)

As a platform operator, I can route chapter generation, essay evaluation, and mind-map generation through one primary provider so that all supported worker AI usage follows the same billing and operational controls.

**Why this priority**: Partial migration would leave production workflows dependent on depleted or separately billed credentials.

**Independent Test**: Run one chapter job, one essay evaluation, and one mind-map generation with fallback disabled by available quota; verify that each completes through the primary provider and keeps its existing output contract.

**Acceptance Scenarios**:

1. **Given** valid primary-provider configuration and available quota, **When** an essay evaluation runs, **Then** the existing structured evaluation result and callback behavior are preserved.
2. **Given** valid primary-provider configuration and available quota, **When** a mind-map request runs, **Then** the generated image is saved and reported through the existing workflow.
3. **Given** valid primary-provider configuration and available quota, **When** any supported AI job runs, **Then** it does not consume the fallback provider.

---

### User Story 3 - Fall Back Only on Quota Exhaustion (Priority: P2)

As a platform operator, I can keep AI jobs running when the primary quota is exhausted by using separately configured fallback credentials, without masking configuration, permission, validation, or code defects.

**Why this priority**: Controlled fallback improves availability while preserving clear operational signals and predictable billing.

**Independent Test**: Simulate a primary quota-exhaustion response and verify one fallback attempt succeeds; then simulate a permission error and verify the job fails without fallback.

**Acceptance Scenarios**:

1. **Given** the primary provider reports quota exhaustion and valid fallback credentials exist, **When** a supported AI operation is attempted, **Then** the operation is retried through the fallback provider once.
2. **Given** the primary provider reports a permission, authentication, malformed-request, safety, or unsupported-operation error, **When** a supported AI operation is attempted, **Then** the job fails clearly without invoking fallback.
3. **Given** quota exhaustion and missing, invalid, or exhausted fallback credentials, **When** fallback is attempted, **Then** the job fails with a diagnostic that distinguishes primary quota exhaustion from fallback failure.

---

### User Story 4 - Detect Unsafe Configuration Before Work Starts (Priority: P2)

As a platform operator, I receive an early, actionable startup failure when required provider or temporary-storage settings are absent or inaccessible, rather than discovering the problem after a long-running job begins.

**Why this priority**: Early validation prevents stuck jobs, leaked temporary data, and ambiguous production failures.

**Independent Test**: Start the worker with each required setting missing or inaccessible and verify startup fails before queue processing with a setting-specific diagnostic.

**Acceptance Scenarios**:

1. **Given** a missing project, location, bucket name, or primary credential, **When** the worker starts, **Then** it rejects the configuration before accepting AI work.
2. **Given** a configured bucket that cannot be accessed with the worker identity, **When** the worker starts, **Then** it fails with a storage-access diagnostic.
3. **Given** valid settings and least-privilege access, **When** the worker starts, **Then** it becomes ready without exposing credentials or sensitive object references in logs.
4. **Given** a production deployment with externally supplied credentials, **When** the new worker fails startup or readiness verification, **Then** the previous healthy worker configuration is restored without committing credentials to source control.

### Edge Cases

- Quota exhaustion occurs after temporary audio has already been uploaded to primary storage.
- A process terminates between upload and normal cleanup; the storage lifecycle removes the orphan within 24 hours.
- Cancellation is requested during upload, remote processing, fallback, or cleanup.
- Cleanup initially fails because of a transient storage error; the job result remains accurate and the orphan-safety policy still applies.
- The fallback provider cannot consume the primary provider's temporary object reference and requires its own upload mechanism.
- A generated response is empty, truncated, malformed, or violates the existing chapter or essay schema.
- Multiple retries for the same job must not create unbounded duplicate temporary objects or duplicate callbacks.
- Mind-map generation succeeds but saving or callback delivery fails; provider routing must not misclassify that downstream failure as quota exhaustion.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: An authorized staff member starts lesson analysis from the existing administration flow and observes the normal progress, completion, transcript, and chapter output.
- **Manual QA Role/Flow 2**: Submit an essay answer and request a chapter mind map; verify existing user-visible outputs remain compatible.
- **Manual QA Negative Check**: Cause a non-quota provider error and verify no fallback occurs and the existing job UI reports a clear failure rather than a false success.
- **Docker Acceptance**: Start the full compose stack with valid secrets and storage configuration; require healthy worker, backend, queue, and database services, then complete representative chapter, essay, and mind-map jobs.
- **External Dependencies**: Primary cloud AI access, a pre-provisioned temporary object bucket with a 24-hour lifecycle, least-privilege worker identity, and a separately funded or free-quota fallback developer credential are required for full live validation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The worker MUST use the configured primary provider by default for every supported AI operation.
- **FR-002**: Supported operations MUST include video transcription and chapter analysis, essay evaluation, and mind-map generation.
- **FR-003**: Video analysis MUST place prepared audio in pre-provisioned temporary object storage before primary-provider processing.
- **FR-004**: The worker MUST use a collision-resistant, job-correlated temporary object name that does not expose student, teacher, lesson, or credential data.
- **FR-005**: Temporary audio MUST be removed after success, failure, or cancellation, without changing the truthful terminal state of the job.
- **FR-006**: Temporary storage MUST enforce deletion of orphaned analysis objects within 24 hours as a cleanup safety net.
- **FR-007**: The worker MUST validate required primary-provider and temporary-storage configuration before accepting AI jobs.
- **FR-008**: Startup validation MUST distinguish missing configuration from inaccessible project, model, or storage resources.
- **FR-009**: Existing progress updates, cancellation behavior, callbacks, persisted outputs, and public response contracts MUST remain compatible.
- **FR-010**: The worker MUST classify provider errors before deciding whether fallback is allowed.
- **FR-011**: Automatic fallback MUST occur only when the primary provider explicitly reports exhausted request or token quota.
- **FR-012**: Permission, authentication, validation, safety, unsupported-operation, malformed-request, and internal implementation errors MUST NOT trigger fallback.
- **FR-013**: Fallback MUST use separately configured developer-provider credentials and the developer provider's supported file-upload mechanism when an operation contains media.
- **FR-014**: Each AI operation MUST perform at most one automatic provider fallback per execution attempt.
- **FR-015**: If fallback is unavailable or fails, the recorded diagnostic MUST preserve both the primary quota failure and the fallback failure without exposing secrets.
- **FR-016**: Retried and cancelled jobs MUST not create unbounded duplicate objects, duplicate persisted results, or duplicate terminal callbacks.
- **FR-017**: Provider and temporary-storage logs MUST exclude API keys, authorization material, sensitive signed references, raw student answers, and unnecessary personal data.
- **FR-018**: Generated transcripts, chapter structures, essay evaluations, and mind-map assets MUST satisfy the existing validation and persistence rules before a job is reported successful.
- **FR-019**: Cleanup failure MUST be observable to operators while preserving an otherwise successful AI result; the 24-hour lifecycle MUST remain the final deletion safeguard.
- **FR-020**: Operators MUST be able to select the primary provider through configuration, with the cloud provider as the default and the developer provider retained for controlled fallback and local compatibility.
- **FR-021**: Production credentials MUST be supplied outside source control, restricted to the worker runtime, and excluded from build artifacts and logs.
- **FR-022**: Production rollout MUST preserve or restore the last healthy worker when startup, readiness, primary-provider, or temporary-storage verification fails.

### Key Entities

- **AI Provider Configuration**: The selected primary provider, project and region context, model selections, credentials, fallback availability, and startup validation status.
- **Temporary Analysis Object**: A short-lived prepared audio object associated with one job, including its opaque object identity, creation time, and cleanup state.
- **AI Operation Attempt**: One provider invocation for chapters, essays, or mind maps, including provider choice, fallback eligibility, outcome category, and non-sensitive diagnostic context.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of successful chapter, essay, and mind-map jobs use the primary provider when its quota is available.
- **SC-002**: 100% of quota-exhaustion simulations invoke no more than one fallback attempt, while 100% of non-quota error simulations invoke none.
- **SC-003**: 100% of normal success, failure, and cancellation tests remove their temporary audio before the worker reports cleanup complete.
- **SC-004**: No temporary analysis object remains longer than 24 hours in orphan-cleanup validation.
- **SC-005**: Representative chapter, essay, and mind-map outputs pass the existing consumers' validation without contract changes.
- **SC-006**: Missing required configuration is reported within 5 seconds of worker startup, before queue processing begins, and identifies the affected setting or capability; inaccessible cloud resources fail within the configured startup-validation timeout.
- **SC-007**: Automated retry and cancellation tests produce one truthful terminal result and no duplicate persisted output or terminal callback.
- **SC-008**: Security inspection of worker logs from success and failure scenarios finds zero API keys, authorization values, or sensitive temporary references.

## Assumptions

- The temporary object bucket is provisioned outside the worker and has a lifecycle rule that removes matching objects after 24 hours.
- The worker identity receives only the permissions required to inspect the configured bucket and create, read, and delete its temporary objects.
- Primary-provider quota exhaustion is distinguishable from other provider errors using structured status information rather than message text alone.
- A separate developer-provider key is supplied when automatic fallback is expected to succeed; absence of that key does not prevent primary-only operation.
- Existing user permissions, UI flows, database schema, queue topology, and callback contracts remain unchanged unless planning proves a minimal compatibility change is required.
- The approved production target uses the existing `massar` temporary bucket, the configured cloud project and global location, and the existing developer-provider credential only as quota-exhaustion fallback.
- A failed production rollout is automatically rolled back to the last healthy worker; this operational safeguard does not broaden product behavior.
