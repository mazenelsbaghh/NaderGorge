# Data Model: Vertex AI Worker Migration

No PostgreSQL schema or persistent domain entity changes are required. The following runtime records are internal worker models.

## AIProviderConfig

| Field | Type | Rules |
|---|---|---|
| `primaryProvider` | `'vertex' \| 'developer'` | Defaults to `vertex` |
| `project` | `string` | Required and non-empty for Vertex |
| `location` | `string` | Required and non-empty for Vertex |
| `temporaryBucket` | `string` | Bare bucket name; required for Vertex chapter analysis |
| `temporaryPrefix` | `string` | Normalized, no leading slash or `..`; default `ai-analysis` |
| `textModel` | `string` | Non-empty; default preserves existing model |
| `imageModel` | `string` | Non-empty; default preserves existing model |
| `fallbackApiKey` | `string?` | Optional; never logged; required only for successful fallback |

## TemporaryAnalysisObject

| Field | Type | Rules |
|---|---|---|
| `bucket` | `string` | From validated config |
| `objectName` | `string` | `<prefix>/<opaque-correlation>/<uuid>.mp3` |
| `generation` | `string?` | Captured from upload response for safe deletion |
| `uri` | `string` | `gs://<bucket>/<objectName>`; never logged in full |
| `mimeType` | `'audio/mpeg'` | Fixed for prepared MP3 |

Lifecycle: `local-prepared → uploading → available → cleanup-pending → deleted`. Upload failure may transition directly to cleanup-pending if a generation exists. Cleanup failure transitions to `orphan-protected`, relying on the bucket lifecycle.

## AIOperationAttempt

| Field | Type | Rules |
|---|---|---|
| `operation` | `'transcription' \| 'chapters' \| 'essay' \| 'mindmap'` | Required |
| `provider` | `'vertex' \| 'developer'` | Required |
| `fallbackUsed` | `boolean` | False for primary; true only once |
| `outcome` | `'success' \| 'quota-exhausted' \| 'failed'` | Sanitized category |
| `httpStatus` | `number?` | May be logged; no response body |

State transition: `primary-pending → primary-success`; or `primary-pending → quota-exhausted → fallback-pending → fallback-success/fallback-failed`; all other failures transition directly to `failed`.

## Invariants

- One operation execution can create at most one fallback attempt.
- Parsing, persistence, callback, cancellation, and local I/O failures are not provider-quota failures.
- Temporary object identity contains no personal or academic content.
- Cleanup runs after all primary/fallback outcomes and does not alter a truthful successful AI result.
- Existing BullMQ job data and backend callback DTOs remain unchanged.

## ProductionCredentialBinding

| Field | Type | Rules |
|---|---|---|
| `hostPath` | `string` | `/var/www/nadergorge/secrets/google-application-credentials.json`; outside Git; mode `0600` |
| `containerPath` | `string` | `/run/secrets/google-application-credentials.json`; read-only mount |
| `principal` | `string` | Service-account email; may be logged only when explicitly auditing IAM, never with key material |
| `vertexProject` | `string` | `project-d32eb428-fe1c-4551-b6d` |
| `bucket` | `string` | `massar` |

Lifecycle: `local-credential → securely-transferred → mounted-read-only → startup-validated → active`. Rotation replaces the host file atomically and recreates only the worker. Deployment failure restores the prior environment/Compose/image state; credential material is not copied into rollback artifacts.
