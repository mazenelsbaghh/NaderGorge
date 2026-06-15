# Quickstart: Comprehensive Audit Remediation

## Preconditions

- PostgreSQL and Redis available through the repository's Docker configuration.
- Development secrets meet existing minimum-strength validation.
- Production history rewrite and credential rotation are performed only from the incident runbook with backups.

## Fast Verification

```bash
node scripts/verify-no-sensitive-tracked-files.mjs
dotnet test backend/NaderGorge.sln --no-restore
(cd worker && npm test)
(cd frontend && npm run lint && npm run build)
docker compose config -q
```

## Permission Acceptance

1. Authenticate as Teacher.
2. Open `/teacher/codes`; confirm owned groups and details are readable.
3. Confirm no generation control is rendered.
4. Call the removed teacher generation endpoint directly; confirm no data mutation.
5. Authenticate as an administrator without `codes.manage`; confirm admin generation is forbidden.
6. Grant `codes.manage`; confirm valid admin generation succeeds and invalid balance/count input fails.

## Academic Access Acceptance

1. Attempt cross-teacher manual unlock and confirm `403`.
2. Start lesson-linked, video-linked, and explicitly granted exams with and without entitlement.
3. Submit homework with and without lesson access.
4. Run concurrent homework and balance regression tests against PostgreSQL.

## Session Acceptance

1. Trigger parallel unauthorized requests and confirm one refresh request.
2. Confirm Zustand, persisted auth, and SignalR receive the successor token/user.
3. Logout and confirm refresh replay fails.
4. Complete password reset and confirm the same reset token cannot be reused.

## Worker Acceptance

1. Enqueue each job type through the backend stream producer.
2. Stop the worker after stream receipt and before acknowledgement.
3. Restart and confirm the pending entry is reclaimed and one BullMQ job exists.
4. Force a retryable callback failure and observe bounded exponential retry.
5. Run the commitment scheduler twice and confirm one occurrence warning.
6. Remove notification provider configuration and confirm the job fails rather than reporting delivery.

## Docker and Release Acceptance

```bash
docker compose config -q
node scripts/verify-surface-separation.mjs
docker compose ps
curl --fail http://127.0.0.1:5245/api/health
curl --fail http://127.0.0.1:5245/api/health/ready
curl --fail http://127.0.0.1:3001/health
curl --fail http://127.0.0.1:3001/ready
```

Verify that production host bindings expose nginx only and that protected asset paths are unavailable from the public assets host.

## Incident Runbook Boundary

Code changes remove active tracked sensitive files and prevent recurrence. Before release, an authorized operator must:

1. Back up repository and production data.
2. Revoke exported refresh tokens and access codes.
3. Rotate exposed user/admin credentials and service secrets.
4. Rewrite Git history using an approved tool and coordinate force-updates to every clone.
5. Re-run repository scanning against the rewritten remote history.
