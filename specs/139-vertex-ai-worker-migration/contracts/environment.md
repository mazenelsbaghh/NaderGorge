# Worker Environment Contract

| Variable | Required | Default | Purpose |
|---|---:|---|---|
| `AI_PRIMARY_PROVIDER` | No | `vertex` | `vertex` or `developer` |
| `GOOGLE_CLOUD_PROJECT` | Vertex | — | Vertex project ID |
| `GOOGLE_CLOUD_LOCATION` | Vertex | — | Vertex region or supported global location |
| `AI_TEMP_GCS_BUCKET` | Vertex chapters | — | Pre-provisioned temporary audio bucket |
| `AI_TEMP_GCS_PREFIX` | No | `ai-analysis` | Opaque object prefix |
| `AI_TEXT_MODEL` | No | `gemini-2.5-flash` | Transcription, chapters, essays |
| `AI_IMAGE_MODEL` | No | `gemini-3-pro-image-preview` | Mind maps |
| `GEMINI_FALLBACK_API_KEY` | No | — | Separate Developer API fallback credential |
| `GEMINI_API_KEY` | Developer primary compatibility only | — | Legacy alias; not used as Vertex auth |
| `GOOGLE_APPLICATION_CREDENTIALS` | Environment-dependent | ADC chain | Optional mounted credential path |
| `GOOGLE_APPLICATION_CREDENTIALS_HOST_PATH` | Production Compose | `./secrets/google-application-credentials.json` | Host-only source for the read-only credential bind mount |

Startup behavior:

- Reject unknown provider values.
- In Vertex mode, reject missing project, location, or bucket before creating queue workers.
- Verify bucket metadata access and surface a sanitized capability-specific failure.
- Do not require a fallback key for primary-only operation.
- In developer-primary mode, require `GEMINI_API_KEY` or `GEMINI_FALLBACK_API_KEY`; GCS is not required.
- Production Compose passes only `GEMINI_FALLBACK_API_KEY`; developer-primary rollback uses that same explicit fallback credential and does not expose the legacy alias to the container.
- Never print variable values for credential-bearing settings.
- In production, set `GOOGLE_APPLICATION_CREDENTIALS=/run/secrets/google-application-credentials.json`; never set it to the host path.
- The host credential file MUST be outside Git, mode `0600`, and mounted read-only into only the worker service.

Infrastructure requirement: the bucket has a delete lifecycle rule with age one day and grants the worker identity only the bucket metadata/object operations required for validation, upload, read by Vertex, and generation-aware delete.
