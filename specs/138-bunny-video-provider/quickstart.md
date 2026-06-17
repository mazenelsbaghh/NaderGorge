# Quickstart: Bunny Video Provider

## Environment

Set backend-only Bunny Stream configuration:

```bash
BunnyStream__LibraryId=123
BunnyStream__ApiKey=replace-with-stream-library-api-key
BunnyStream__TusUploadExpiryMinutes=180
```

Optional frontend/server runtime configuration for the Next embed route if the backend does not embed library ID in the encrypted video material:

```bash
BUNNY_STREAM_LIBRARY_ID=123
```

Do not expose the Bunny API key through `NEXT_PUBLIC_*`.

## Database

Create and apply EF migration for:
- `BunnyVideoAssets`
- `BunnyUsageSnapshots`
- Bunny pricing setting seed/defaults if settings are seeded through migration

Expected migration command:

```bash
dotnet ef migrations add AddBunnyVideoProvider --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API
dotnet ef database update --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API
```

Use the repository's Docker migration workflow if different.

## Local Verification

1. Start infrastructure:

```bash
docker compose config -q
docker compose ps
```

2. Run automated checks:

```bash
dotnet test backend/NaderGorge.sln
npm --prefix frontend run lint
npm --prefix frontend run typecheck
```

3. Verify legacy providers:
- Create/edit YouTube lesson video.
- Create/edit VK lesson video.
- Open student playback for both.

4. Verify Bunny local upload:
- Login as teacher.
- Open an owned lesson.
- Select Bunny upload.
- Upload a local video file.
- Confirm progress is visible and retry/resume state is available.
- Confirm no cost fields appear in teacher UI or API responses.

5. Verify Bunny admin upload:
- Login as admin.
- Select teacher, package, lesson.
- Upload a file through Bunny TUS.
- Add a remote URL and request Bunny fetch.
- Confirm resulting videos are attributed to selected teacher.

6. Verify cost snapshots:
- Ensure Bunny pricing settings are present:
  - Storage USD/GB default `0.01`
  - Bandwidth USD/GB default `0.005`
- Run monthly usage sync for the current month.
- Open admin cost report with monthly filter.
- Confirm video, teacher, package, and platform totals.
- Change pricing settings and create a new snapshot period; confirm old snapshot rates did not change.

7. Verify AI behavior:
- Request AI analysis while Bunny video is processing; expect clear processing state.
- Request AI analysis after Bunny video is ready and media access exists; expect normal AI job path or clear unsupported state.

## Release Checks

- Bunny API key absent from frontend bundle and network responses.
- Teachers cannot call admin Bunny cost endpoints.
- YouTube/VK records are not migrated or rewritten.
- Failed Bunny upload/fetch does not leave broken playable student video.
- Monthly report dates use UTC period boundaries and UI labels the selected month clearly.
