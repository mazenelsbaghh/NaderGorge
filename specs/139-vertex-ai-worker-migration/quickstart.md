# Quickstart: Vertex AI Worker Migration

## Local deterministic verification

```bash
npm --prefix worker install
npm --prefix worker test
npm --prefix worker run build
docker compose config -q
docker compose build worker
```

Expected: worker tests pass, TypeScript compiles, compose configuration validates, and the worker image builds.

## Cloud prerequisites

1. Enable Vertex AI and Cloud Storage APIs in the configured project.
2. Provision a temporary bucket accessible to Vertex and the worker identity.
3. Configure a delete lifecycle rule with age one day for temporary analysis objects.
4. Grant least-privilege bucket metadata plus object create/read/delete permissions.
5. Provide ADC through workload identity, local ADC, or a read-only mounted service-account credential file.

## Runtime configuration

```dotenv
AI_PRIMARY_PROVIDER=vertex
GOOGLE_CLOUD_PROJECT=example-project
GOOGLE_CLOUD_LOCATION=global
AI_TEMP_GCS_BUCKET=example-ai-temp
AI_TEMP_GCS_PREFIX=ai-analysis
AI_TEXT_MODEL=gemini-2.5-flash
AI_IMAGE_MODEL=gemini-3-pro-image-preview
GEMINI_FALLBACK_API_KEY=replace-with-separate-fallback-key
```

Keep existing Redis, database, callback, subtitle, and worker-admin settings unchanged.

## Production values

```dotenv
AI_PRIMARY_PROVIDER=vertex
GOOGLE_CLOUD_PROJECT=project-d32eb428-fe1c-4551-b6d
GOOGLE_CLOUD_LOCATION=global
AI_TEMP_GCS_BUCKET=massar
AI_TEMP_GCS_PREFIX=ai-analysis
AI_TEXT_MODEL=gemini-2.5-flash
AI_IMAGE_MODEL=gemini-3-pro-image-preview
GOOGLE_APPLICATION_CREDENTIALS=/run/secrets/google-application-credentials.json
GOOGLE_APPLICATION_CREDENTIALS_HOST_PATH=./secrets/google-application-credentials.json
```

Copy the existing production `GEMINI_API_KEY` value to `GEMINI_FALLBACK_API_KEY` without printing it. Store the service-account JSON at `/var/www/nadergorge/secrets/google-application-credentials.json`, owned by root with mode `0600`.

## Live smoke

```bash
make up
docker compose ps
curl -f http://localhost:3001/health
curl -f http://localhost:3001/ready
```

Then run one existing chapter analysis, essay grading, batch mind-map, and single mind-map flow. Verify output compatibility and confirm no temporary object remains after each terminal state.

## Negative checks

- Remove `GOOGLE_CLOUD_PROJECT`: worker must stop before queue consumption and name the missing setting.
- Deny bucket metadata access: worker must stop with a sanitized storage-access diagnostic.
- Inject a structured Vertex 429: exactly one Developer API call occurs.
- Inject Vertex 400/401/403: no fallback call occurs.
- Cancel after GCS upload: object deletion runs and only one terminal job state/callback remains.
- Fail object deletion: AI result remains truthful and cleanup failure is operator-visible; lifecycle protects the orphan.

## Rollback

Before deployment, snapshot `.env`, `docker-compose.yml`, the current `massar_worker` image ID, and health state. If the recreated worker fails readiness, Vertex, GCS, or log-safety checks, restore those snapshots and recreate the previous worker. Setting `AI_PRIMARY_PROVIDER=developer` is reserved for an explicit operator emergency after infrastructure rollback; it is not the normal automatic rollback path.
