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
- [ ] Run `python3 scratch/fix_migrations_production.py --skip-build` to connect to production VPS and create the missing tables.

---

## History
- Initialized Ops master plan directory.
