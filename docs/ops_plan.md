# Operations Master Plan

**Last Updated**: 2026-06-09

---

## Active Plans

### Deploy Assistant Surface and Task Workflow (2026-06-09)
- [x] Rebuild shared frontend docker image `massar_frontend:local`.
- [x] Recreate and restart assistant container `massar_assistant` via docker compose.
- [x] Verify Nginx reverse proxy routing for `staff.massar-academy.net` to the assistant container.

### Deploy Default Role Permissions (2026-06-09)
- [x] Run EF Core DB migrations on the production server.
- [x] Rebuild and deploy backend container.

### Rebuild and Deploy Auto-Cycle Swiper Updates (2026-06-06)
- [x] Rebuild shared frontend docker image `massar_frontend:local` via `make build-frontend`.
- [x] Recreate and restart frontend surface containers (`landing`, `student`, `admin`) via `docker compose up -d`.

### Rebuild and Deploy 3D Swiper Stack & Kinetic Reveals (2026-06-06)
- [x] Rebuild shared frontend docker image `massar_frontend:local` via `make build-frontend`.
- [x] Recreate and restart frontend surface containers (`landing`, `student`, `admin`) via `docker compose up -d`.

### Rebuild and Deploy Frontend Overdrive Updates (2026-06-06)
- [x] Rebuild shared frontend docker image `massar_frontend:local` using `make build-frontend`.
- [x] Recreate and restart frontend surface containers (`landing`, `student`, `admin`) using `docker compose up -d`.

### Impeccable Skills Installation & Update Check (2026-06-05)
- [x] Run `npx impeccable skills install` to ensure skills are properly set up.
- [x] Run `npx impeccable skills update` to check for updates.

### Student Forgot Password Deployment (2026-06-04)
- [x] No database migrations required (uses existing `User` and `StudentProfile` schemas).
- [x] No new environment variables required (reuses existing JWT configuration).
- [x] Verify build and tests via:
  - Frontend: `npm run lint` and `npm run build`
  - Backend: `dotnet build` and `dotnet test`

### Watch Requests 500 Error Fix & Deployment Hardening (2026-06-04)
- [x] Diagnose 500 errors on `GET /api/admin/watch-requests` and `POST /api/student/video-session/{id}/request-extra`.
- [x] Identify root cause: The `ExtraWatchRequests` table is defined in the DbContext model snapshot but was never generated in C# EF Core migrations, making it completely missing in production.
- [x] Run `docker/create_missing_tables.sql` directly on the VPS database container via SSH to immediately create the missing tables and indexes.
- [x] Configure missing environment secrets (`AI_CALLBACK_SECRET`, `PARENT_REPORT_SIGNING_SECRET`, `WORKER_ADMIN_TOKEN`) in the VPS `.env` file and restart Docker Compose.
- [x] Resolve `WORKER_ADMIN_TOKEN` interpolation error by updating GitHub Actions deployment pipeline to read updated environment.
- [x] Create `scratch/fix_migrations_vps.py` to execute database migrations and container builds locally on the VPS using standard library `subprocess` (bypassing macOS Python 3.14.2 Paramiko sockets load-time deadlock).
- [x] Update `Makefile` to trigger `scratch/fix_migrations_vps.py` directly on the VPS via SSH.
- [x] Verify all 5 containers (backend, frontend, worker, db, redis) are completely healthy on the production server.

---

## History
- **2026-06-06**: Cleaned naming conflicts, rebuilt the frontend image, and deployed the student 3D Card auto-cycle swiper features successfully to Docker.
- **2026-06-06**: Rebuilt frontend image and deployed the draggable 3D Card Stack Swiper and kinetic reveals updates to Docker.
- **2026-06-06**: Rebuilt the shared frontend Docker image and recreated containers (landing, student, admin) to deploy the landing page Overdrive enhancements.
- **2026-06-05**: Checked impeccable skills installation via `npx impeccable skills install`.
- **2026-06-04**: Fixed watch requests 500 internal server error, updated production environment secrets, resolved deployment variable interpolation issue, and refactored deployment script to run on VPS side.
- **2026-06-04**: Initialized Ops master plan directory.
