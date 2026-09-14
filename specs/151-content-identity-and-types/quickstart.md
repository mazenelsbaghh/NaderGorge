# Quickstart: Content Identity and Types

## Preconditions

- Docker and Docker Compose are available.
- Repository `.env` contains the existing required secrets.
- No external provider credential is required to test type management or code display.
- Preserve unrelated dirty worktree changes; do not reset them.

## Apply And Build

```bash
docker compose config -q
make up
make migrate
dotnet build backend/NaderGorge.sln
cd frontend && npm run build
```

## Focused Automated Checks

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter ContentIdentityAndVideoTypesTests
cd frontend && npx playwright test tests/e2e/admin-content.spec.ts --project=chromium
```

Then run the full backend test project and frontend lint before close-out.

## Health Checks

```bash
make ps
curl -f http://localhost:5245/api/health
curl -f http://localhost:3001/ui
curl -f http://localhost:8738
```

## Manual Admin Flow (`pending` Until Performed)

1. Sign in with the built-in Admin role.
2. Open `http://admin.localhost:8738/admin/content/video-types` using the configured local domain/port combination.
3. Create `حل أسئلة`, rename it, change its order, deactivate it, and reactivate it.
4. Open a lesson profile, create a video with that type, and record the displayed `VID-...` code.
5. Edit the video title and type; verify its code remains identical.
6. Deactivate the assigned type; verify the existing video still displays it and new-video choices omit it.
7. Attempt to delete the assigned type; verify deletion is blocked with deactivation guidance.
8. Open the lesson and exam profiles and verify their `LES-...` and `EXM-...` codes.

## Manual Negative Flow (`pending` Until Performed)

1. Sign in as a non-admin user who has `content.manage`.
2. Verify active types can be loaded in an otherwise permitted video form.
3. Navigate directly to `/admin/content/video-types` and verify access is denied.
4. Call a catalog mutation endpoint and verify `403` with no persisted change.
5. Try submitting no type and an inactive replacement type; verify clear validation and no video mutation.

## Migration Verification

Run database checks after migration:

```sql
SELECT COUNT(*) FROM lessons WHERE "InternalCode" IS NULL OR "InternalCode" = '';
SELECT COUNT(*) FROM lesson_videos WHERE "InternalCode" IS NULL OR "InternalCode" = '' OR "VideoTypeId" IS NULL;
SELECT COUNT(*) FROM exams WHERE "InternalCode" IS NULL OR "InternalCode" = '';
SELECT "InternalCode", COUNT(*) FROM (
  SELECT "InternalCode" FROM lessons
  UNION ALL SELECT "InternalCode" FROM lesson_videos
  UNION ALL SELECT "InternalCode" FROM exams
) codes GROUP BY "InternalCode" HAVING COUNT(*) > 1;
```

Expected result: all null/empty counts are `0`; duplicate query returns no rows.

## Rollback Note

The migration `Down` path removes the feature schema. For production rollback after catalog edits, restore the pre-migration backup because dropping the catalog cannot reconstruct edits made after deployment.
