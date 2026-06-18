# Implementation Plan: Vertex AI Worker Migration

**Branch**: `139-vertex-ai-worker-migration` | **Date**: 2026-06-18 | **Spec**: `specs/139-vertex-ai-worker-migration/spec.md`
**Input**: Feature specification from `specs/139-vertex-ai-worker-migration/spec.md`

## Summary

Replace the worker's singleton Gemini Developer API client with a provider-neutral AI gateway. Vertex AI is the default provider for chapter transcription/analysis, essay grading, and mind-map generation. Chapter audio is uploaded to a pre-provisioned GCS bucket and referenced by `gs://` URI; the object is deleted in `finally`. A separately configured Gemini Developer API client is invoked at most once and only for a structured Vertex HTTP 429/`RESOURCE_EXHAUSTED` failure. Existing BullMQ job data, progress stages, callback payloads, SRT files, and mind-map URLs remain unchanged.

## Technical Context

**Language/Version**: TypeScript 5.9.3 strict mode on Node.js 20
**Primary Dependencies**: `@google/genai` 1.47.0, `@google-cloud/storage`, BullMQ 5.71.1, Express 5.2.1, undici 7.24.6
**Storage**: GCS temporary object bucket; existing local `.tmp`, subtitle, and mind-map files; Redis job state; no database schema change
**Testing**: Node built-in test runner through `npm --prefix worker test`; TypeScript build through `npm --prefix worker run build`
**Target Platform**: Linux Node.js worker in Docker and native developer execution
**Project Type**: Background worker/web service
**Performance Goals**: Preserve existing 60-minute AI timeout and two-call audio analysis; no extra media upload on the primary path
**Constraints**: One fallback maximum per AI operation attempt; fallback only for structured quota exhaustion; cleanup on success/failure/cancellation; no secrets, signed URLs, student answers, or object URIs in logs
**Scale/Scope**: Three AI workflows, four BullMQ queues unchanged, one worker service, no UI/backend/database changes; production completion targets `/var/www/nadergorge` and bucket `massar`

## Constitution Check

- **Modular architecture / provider abstraction**: PASS. New configuration, storage, error classification, and provider gateway modules isolate external APIs from job processors.
- **Academic integrity**: PASS. Existing prompts, models, output validation, callback payloads, and persistence behavior are preserved.
- **Security**: PASS. ADC/service-account auth is used for Vertex/GCS; fallback API key is separate; logs use provider/category metadata only.
- **Observability**: PASS. Startup validation names the missing capability, provider attempts are logged without content, and cleanup failures remain visible.
- **Layer impact**: Worker and Docker/environment documentation only. Backend, frontend, and PostgreSQL are unchanged.
- **Automated tests**: Unit tests cover configuration, quota classification, fallback limits, GCS cleanup, provider selection, and regression contracts; existing worker tests remain required.
- **Manual QA**: Representative chapter, essay, batch mind-map, single mind-map, cancellation, quota fallback, and non-quota rejection flows.
- **Docker gate**: `docker compose config -q`, worker image build, stack start when secrets/ADC are available, worker `/health` and `/ready` checks. No migration command because schema is unchanged.
- **Phase gate**: Implementation cannot close with failing worker tests/build, compose validation, or unresolved feature-introduced warnings.

## Project Structure

### Documentation (this feature)

```text
specs/139-vertex-ai-worker-migration/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── ai-provider.md
│   └── environment.md
└── tasks.md
```

### Source Code (repository root)

```text
worker/
├── package.json
├── .env.example
└── src/
    ├── index.ts
    ├── services/
    │   ├── aiConfig.ts
    │   ├── aiErrors.ts
    │   ├── aiProvider.ts
    │   ├── temporaryAudioStorage.ts
    │   └── geminiService.ts
    ├── jobs/
    │   ├── analyzeVideoChapters.ts
    │   ├── evaluateEssay.ts
    │   └── generateChapterMindmaps.ts
    └── services/*.test.ts

docker-compose.yml
AGENTS.md
```

**Structure Decision**: Keep public functions in `geminiService.ts` for import compatibility, but move client construction, provider routing, configuration validation, error classification, and GCS object lifecycle into focused worker service modules. Job processors retain queue/callback ownership.

## Implementation Scope

1. Add `@google-cloud/storage` and environment variables for primary provider, Vertex project/location, GCS bucket/prefix, model overrides, and separate fallback key.
2. Parse and validate AI configuration without reading secrets into logs. Vertex is the default; developer-only mode remains available for local compatibility.
3. Construct independent `GoogleGenAI` clients: Vertex with `{vertexai:true, project, location}` and developer fallback with `{apiKey}`.
4. Implement `runWithProviderFallback(operation, vertexCall, fallbackCall)`. It retries once only when the primary error has HTTP status 429 or structured status `RESOURCE_EXHAUSTED`; callback/storage/parsing errors never enter this router.
5. Implement temporary audio storage using GCS opaque object names `prefix/<job-correlation>/<uuid>.mp3`, upload with `audio/mpeg`, and generation-aware deletion in `finally`.
6. Vertex chapter calls receive `fileData.fileUri=gs://bucket/object`; fallback uploads the local file through Developer File API and deletes that remote upload in `finally`.
7. Essay and mind-map calls use the same gateway but do not use GCS. Teacher photos remain inline and first in the mind-map parts array.
8. Validate configuration before BullMQ workers start. Check required values synchronously; verify bucket metadata access asynchronously before calling `startWorker()`.
9. Preserve response parsing, progress values, cancellation checks, callback URLs/bodies, SRT output, and mind-map file output.
10. Bind-mount the production service-account JSON from an operator-controlled host path to `/run/secrets/google-application-credentials.json` read-only; set `GOOGLE_APPLICATION_CREDENTIALS` to that container path and ignore `secrets/` in Git.
11. Configure production with Vertex project `project-d32eb428-fe1c-4551-b6d`, location `global`, bucket `massar`, prefix `ai-analysis`, and the existing Developer API key copied to the fallback-only variable.
12. Grant the service account `roles/storage.objectUser` on bucket `massar` plus bucket-metadata read permission, verify the one-day delete lifecycle, and retain the already granted `roles/aiplatform.user` on the Vertex project.

## Phase 0: Research Output

`research.md` records the resolved SDK, authentication, GCS media, fallback classification, cleanup, lifecycle, validation, model, and test-seam decisions. All planning questions are resolved.

## Phase 1: Design Output

`data-model.md` defines the runtime configuration, temporary-object, and operation-attempt records. `contracts/` defines provider and environment boundaries. `quickstart.md` defines deterministic, Docker, cloud, negative, and rollback verification.

## Interface and Persistence Impact

- No public HTTP, backend callback, Redis payload, BullMQ job, or database contract changes.
- New internal contracts are documented in `contracts/ai-provider.md` and `contracts/environment.md`.
- GCS objects are transient operational state only; no database record is added.
- Bucket lifecycle remains infrastructure-owned. Startup verifies the bucket is accessible and warns/fails when the configured lifecycle does not provide the required delete safety net where metadata permits inspection.

## Failure Modes and Controls

- Missing primary config: fail startup before queue creation.
- ADC/Vertex auth or bucket access failure: fail startup with capability-specific sanitized error.
- Vertex 429/`RESOURCE_EXHAUSTED`: invoke developer fallback once if configured; otherwise throw a diagnostic retaining both provider categories.
- Vertex 400/401/403/404/5xx, safety, malformed output, or implementation error: fail without fallback.
- Upload failure: no AI call; cleanup any known generation and fail truthfully.
- Cancellation after upload: processor throws; service `finally` deletes the temporary object.
- GCS delete failure: report cleanup diagnostic without hiding the primary AI/cancellation outcome; lifecycle is the orphan safety net.
- Developer upload/call failure: delete File API upload when created and throw combined primary/fallback diagnostic.
- Callback or local persistence failure: never classified as provider quota and never triggers fallback.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `npm --prefix worker test`: configuration, structured quota classification, one-shot fallback, no fallback for permission/validation/internal errors, temporary-object cleanup, all three workflows, and existing cancellation/callback regression tests.
- `npm --prefix worker run build`: strict TypeScript compile.
- `docker compose config -q`: environment and compose validity.

**Docker Gate Required**:

- `docker compose config -q`
- `docker compose build worker`
- With valid ADC/cloud secrets: `make up`, `docker compose ps`, `curl -f http://localhost:3001/health`, `curl -f http://localhost:3001/ready`.
- `make migrate` is explicitly not applicable because no schema changes exist.

**Manual QA Required**:

- Admin starts video analysis: existing Arabic progress, SRT, and chapters complete.
- Existing essay submission receives compatible score/feedback without exposing the model answer.
- Batch and single mind-map generation save images and call their existing callbacks.
- Active analysis cancellation deletes temporary local/GCS media and produces one terminal state.
- Simulated Vertex quota exhaustion performs one fallback; permission and malformed-request failures perform none.
- Inspect logs and bucket objects for secret/content leakage and orphan cleanup.

**End-of-Phase Report Format**: Scope delivered; changed files; exact commands/results; Docker result or external-secret blocker; manual QA checklist; fixed failures; remaining risks; go/no-go.

## Rollout Notes

1. On bucket `massar`, configure a one-day Delete lifecycle and grant only object create/read/delete/list plus bucket metadata read to `ais-gemini-key-ce12231e2f164c7@448008668843.iam.gserviceaccount.com`.
2. Store the credential at `/var/www/nadergorge/secrets/google-application-credentials.json` with mode `0600`; bind it to `/run/secrets/google-application-credentials.json:ro`. Never copy it into the image or repository.
3. Back up the production `.env`, current worker image ID, and current Compose file before changing runtime inputs.
4. Deploy with `AI_PRIMARY_PROVIDER=vertex`, project `project-d32eb428-fe1c-4551-b6d`, location `global`, bucket `massar`, and the existing Developer API key mapped to `GEMINI_FALLBACK_API_KEY`.
5. Rebuild and recreate only `worker`; require Docker health, `/health`, `/ready`, Vertex generation, GCS upload/delete, and secret-log inspection before declaring success.
6. If any gate fails, restore `.env`/Compose, retag or rebuild the previous worker revision, recreate `worker`, and verify the previous health state. Developer-primary mode is an emergency runtime rollback only; automatic fallback rules remain unchanged.

## Production Completion Risks

- The local Git branch is `136-audio-upload-restrictions` while the active Spec Kit directory is explicitly pinned to `specs/139-vertex-ai-worker-migration`; deployment must use the reviewed feature diff and must not infer scope from the branch name.
- The production working directory has no `.git` metadata; the `prod` remote and deployment hook own code synchronization. Secret transfer is a separate SCP operation to an ignored host directory.
- The credential service account belongs to a different credential project. Cross-project access is valid only after the target Vertex project and `massar` bucket policies explicitly grant it access.
- The current service account can call Vertex after `roles/aiplatform.user` was granted but cannot yet read bucket metadata; production rollout is blocked until bucket IAM and lifecycle checks pass.

## Complexity Tracking

No constitution violations or additional system layers are required.
