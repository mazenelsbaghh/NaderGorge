# Operations Master Plan

**Last Updated**: 2026-06-04

---

## Active Plans

### Student Forgot Password Deployment (2026-06-04)
- [x] No database migrations required (uses existing `User` and `StudentProfile` schemas).
- [x] No new environment variables required (reuses existing JWT configuration).
- [x] Verify build and tests via:
  - Frontend: `npm run lint` and `npm run build`
  - Backend: `dotnet build` and `dotnet test`

### Watch Requests 500 Error Fix (2026-06-04)
- [x] Diagnose 500 errors on `GET /api/admin/watch-requests` and `POST /api/student/video-session/{id}/request-extra`.
- [x] Identify root cause: The `ExtraWatchRequests` table is defined in the DbContext model snapshot but was never generated in C# EF Core migrations, making it completely missing in production.
- [x] Update `scratch/fix_migrations_production.py` to automatically run `docker/create_missing_tables.sql` on SSH connection.
- [x] Run `docker/create_missing_tables.sql` directly on the VPS database container via SSH to immediately create the missing tables and indexes.
- [x] Configure missing environment secrets (`AI_CALLBACK_SECRET`, `PARENT_REPORT_SIGNING_SECRET`, `WORKER_ADMIN_TOKEN`) in the VPS `.env` file and restart Docker Compose.

---

## History
- Initialized Ops master plan directory.
