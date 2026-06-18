# Research: Vertex AI Worker Migration

## Decision 1: Use one SDK with two independently configured clients

- **Decision**: Keep `@google/genai` 1.47.0. Create a Vertex client with `vertexai: true`, `project`, and `location`, plus a separate Gemini Developer API client with `GEMINI_FALLBACK_API_KEY`.
- **Rationale**: The installed SDK explicitly supports both endpoints and current worker request/response shapes. Independent construction prevents accidental reuse of the fallback key as Vertex credentials.
- **Alternatives considered**: `@google-cloud/vertexai` was rejected because the repository constitution standardizes `@google/genai`, and maintaining two generation SDKs would duplicate response adapters.

## Decision 2: Use ADC for Vertex and GCS

- **Decision**: Use Application Default Credentials for both services. Accept an optional `GOOGLE_APPLICATION_CREDENTIALS` path through the standard Google auth chain but do not parse service-account JSON in application code.
- **Rationale**: ADC supports local developer credentials, mounted service accounts, and workload identity without embedding secrets.
- **Alternatives considered**: API-key Vertex express mode was rejected because project/location and bucket IAM are explicit requirements; raw service-account JSON environment variables were rejected due to leakage and parsing risk.

## Decision 3: GCS URI for Vertex audio, File API for fallback audio

- **Decision**: Upload prepared MP3 once to GCS, pass `fileData: {fileUri: "gs://...", mimeType: "audio/mpeg"}` to both Vertex chapter calls, and delete the object in `finally`. If fallback is needed, separately upload the local MP3 via the Developer File API and delete it in its own `finally`.
- **Rationale**: The installed SDK explicitly rejects `ai.files.upload` in Vertex mode and directs callers to GCS. The Developer API cannot consume the Vertex object's private GCS reference under the current contract.
- **Alternatives considered**: Base64 inline audio was rejected for long lessons and memory overhead. Signed URLs were rejected because they expand secret-bearing log/error surface.

## Decision 4: Structured quota classification

- **Decision**: Treat HTTP 429 or a structured API status of `RESOURCE_EXHAUSTED` as fallback-eligible. Do not match arbitrary error-message text. Execute at most one fallback call per operation attempt.
- **Rationale**: Vertex documents 429 as resource exhaustion, while permission/authentication/validation failures use distinct statuses. Message matching is unstable and can accidentally route implementation errors.
- **Alternatives considered**: Fallback on all 5xx was rejected because capacity failures under provisioned throughput can be represented differently and the approved business rule permits fallback only for explicit exhausted quota.

## Decision 5: Provider routing boundary

- **Decision**: Centralize routing in a generic gateway used by chapter transcription, chapter JSON generation, essay grading, and mind-map generation. Keep output parsing and persistence outside the routing callback.
- **Rationale**: Only remote generation failures should be considered for fallback. JSON parsing, local file I/O, and webhook failures must remain truthful downstream failures.
- **Alternatives considered**: Wrapping entire jobs was rejected because it could duplicate callbacks and persisted results.

## Decision 6: Temporary object identity and cleanup

- **Decision**: Object names use a configured prefix plus a sanitized opaque job correlation and `crypto.randomUUID()`. Store the returned generation and delete that generation in `finally`.
- **Rationale**: Collision resistance and generation matching avoid deleting another retry's object. Names exclude user, lesson title, teacher, and credential data.
- **Alternatives considered**: Reusing `<lessonVideoId>.mp3` was rejected because concurrent retries can overwrite and race cleanup.

## Decision 7: Lifecycle is a safety net, not synchronous cleanup

- **Decision**: Require an externally provisioned GCS lifecycle delete rule with age one day for the configured prefix/bucket, while application cleanup remains synchronous best effort in every terminal path.
- **Rationale**: Cloud Storage lifecycle processing is asynchronous and may lag after the age condition is met, so it cannot define job completion. It is only an orphan safeguard.
- **Alternatives considered**: Creating/updating lifecycle rules from the worker was rejected because it requires bucket-admin privileges and violates least privilege.

## Decision 8: Startup validation before queue consumption

- **Decision**: Parse required settings synchronously, then call bucket metadata access before creating BullMQ workers. Vertex authentication/model access is validated without a billable generation call; model invocation errors remain job diagnostics.
- **Rationale**: Bucket access can be checked cheaply. A generation probe can incur cost, quota, and model-output variability.
- **Alternatives considered**: Lazy validation in the first job was rejected by FR-007/FR-008.

## Decision 9: Models and region are configuration

- **Decision**: Default text model to `gemini-2.5-flash` and image model to the existing `gemini-3-pro-image-preview`, with environment overrides. Validate non-empty names but let Vertex return a clear unsupported-region/model error without fallback.
- **Rationale**: Model availability varies by region and changes over time. Configuration avoids code edits while preserving current behavior.
- **Alternatives considered**: Hard-coding a new model was rejected because it silently changes academic output behavior.

## Decision 10: Test seams use dependency injection

- **Decision**: Export pure configuration/error functions and construct gateway/storage with injectable clients. Tests use fakes rather than patching the SDK's global fetch.
- **Rationale**: Deterministic tests can verify provider calls, cleanup, and fallback count without credentials or network access.
- **Alternatives considered**: Live cloud tests in the default suite were rejected for cost, flakiness, and secret dependency; a manual live smoke remains in quickstart.

## Decision 11: Mount production ADC as a read-only file

- **Decision**: Keep the service-account JSON at `/var/www/nadergorge/secrets/google-application-credentials.json`, mode `0600`, and bind-mount it read-only at `/run/secrets/google-application-credentials.json`. The environment variable contains only the in-container path.
- **Rationale**: The worker uses the standard ADC chain, the secret stays outside Git and Docker build context, and rotation requires only replacing the host file and recreating the worker.
- **Alternatives considered**: Baking JSON into the image and storing raw JSON in `.env` were rejected for leakage risk. Docker Swarm secrets were rejected because this host uses plain Docker Compose rather than Swarm.

## Decision 12: Use bucket-scoped object access plus metadata read

- **Decision**: Grant bucket-scoped `roles/storage.objectUser` for create/read/delete/list operations and the narrowest available bucket metadata read permission needed by startup `getMetadata()`. Do not grant project-wide Storage Admin to the worker identity.
- **Rationale**: Google documents Storage Object User as providing create/read/update/delete object access, while `storage.buckets.get` is separately required to read lifecycle metadata. Bucket scope limits exposure to `massar`.
- **Alternatives considered**: Project-wide Storage Admin was rejected as excessive. Object Creator alone was rejected because it cannot read metadata or delete cleanup objects.

## Decision 13: Separate deployment rollback state from provider fallback

- **Decision**: Snapshot the current `.env`, Compose file, worker image ID, and health state before deployment. On any startup, health, Vertex, or GCS gate failure, restore the prior worker runtime. The Developer API key remains an operation-level quota fallback, not the primary deployment rollback mechanism.
- **Rationale**: This preserves service availability without masking configuration or IAM errors and avoids switching all traffic to a billed fallback because deployment wiring failed.
- **Alternatives considered**: Leaving a failed worker for manual diagnosis was rejected due to production availability risk. Automatically making Developer API primary was rejected because it changes billing and the approved provider policy.

## Sources and repository evidence

- Installed SDK: `worker/node_modules/@google/genai/README.md` documents Vertex initialization; SDK implementation rejects Vertex File API uploads and accepts `gs://` file URIs.
- Worker call sites: `worker/src/services/geminiService.ts`, `worker/src/jobs/analyzeVideoChapters.ts`, `worker/src/jobs/evaluateEssay.ts`, `worker/src/jobs/generateChapterMindmaps.ts`.
- Google Cloud: Vertex AI returns HTTP 429 for resource exhaustion; Cloud Storage lifecycle actions are asynchronous; `roles/storage.objectUser` supplies object create/read/delete capabilities, while startup bucket metadata inspection requires `storage.buckets.get`.
- Existing contracts: `worker/src/worker-flows.test.ts`, callback code in the three job processors, and `docker-compose.yml` worker environment.
