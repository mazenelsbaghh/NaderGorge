# Operations Master Plan

**Last Updated**: 2026-06-15

---

## Active Plans

### Comprehensive Audit Remediation (2026-06-15)
- [x] Clean up tracked SQL dumps and temporary database backups from the Git active tree and `.gitignore` them.
- [x] Create CI security script `verify-no-sensitive-tracked-files.mjs` to block commit of secrets or dumps.
- [x] Re-architect worker loops to consume from Redis Stream `job-stream` under worker group consumer, retry jobs up to 5 times with exponential backoff, and acknowledge them on success.
- [x] Expose worker liveness and database/Redis/queue readiness endpoint `/ready`.
- [x] Isolate Docker internal network host ports to `127.0.0.1` for PG, Redis, worker, backend, and frontend containers.
- [x] Pin all Docker base images and tool inputs to exact patch versions.
- [x] Upgrade `MessagePack` transitive dependency to secure version `2.5.301` inside C# backend.
- [x] Add package overrides to `package.json` in frontend and worker to lock dependencies.

### Real-time Platform Speed & Sync (2026-06-11)
- [x] Apply EF Core migrations for the `OutboxEvents` table on the database.
- [x] Configure SignalR WebSockets transport and connection lifetimes in the API and frontend containers.
- [x] Verify Redis container has active configuration for persistent caching and rate-limiting.
- [x] Validate production builds:
  - C# Backend: `dotnet build` passes with 0 warnings/errors.
  - Next.js Frontend: `next build` passes and creates optimized production bundles.
  - Node.js Worker: `tsc -p tsconfig.json` compiles successfully.

### Performance Audit Remediation (2026-06-11)
- [x] Configure Brotli response compression and output caching configurations in C# backend.
- [x] Clean and optimize Turbopack dev cache (`rm -rf frontend/.next`).
- [x] Document static-route validations and timing benchmarks for endpoints in local env.


### Telegram Bot Audio Extraction Integration (2026-06-11)
- [x] Install `telegram` (GramJS) npm package in the worker.
- [x] Add Telegram configuration variables (`TELEGRAM_API_ID`, `TELEGRAM_API_HASH`, `TELEGRAM_STRING_SESSION`, `TELEGRAM_DOWNLOADER_BOT`) to `.env.example`, `.env`, and the `worker` service in `docker-compose.yml`.
- [x] Create `worker/src/scripts/telegram-login.ts` CLI script to login and output `TELEGRAM_STRING_SESSION`.
- [x] Update `worker/src/utils/audioExtractor.ts` to use the GramJS Telegram client:
  - If Telegram session/API keys are provided, connect to Telegram.
  - Send YouTube URL to the target bot (e.g. `@utubebot`, `@YTAudioBot`, `@u_download_bot`).
  - Listen for the bot's response containing the audio document/media file.
  - Download the file and save it as MP3, with fallback to yt-dlp / Cobalt.
- [x] Verify local tests and run deployment.

### Deploy Domain and Docker Isolation Finalization (2026-06-09)
- [x] Consolidate `Cors__AllowedOrigins` and `NEXT_PUBLIC_APP_DOMAIN` in `docker-compose.yml`.
- [x] Configure 301 redirects and consolidate subdomains in `docker/nginx/massar.conf`.
- [x] Add automated static and runtime tests to `scripts/verify-surface-separation.mjs`.

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
- **2026-06-15**: Completed security and operations audit remediation: isolated host bindings, pinned Docker images, updated MessagePack/npm dependencies, and switched queue pipeline to Redis Streams.
- **2026-06-11**: Integrated Telegram bot audio extraction client in the worker to download YouTube audios via Telegram bots.
- **2026-06-06**: Cleaned naming conflicts, rebuilt the frontend image, and deployed the student 3D Card auto-cycle swiper features successfully to Docker.
- **2026-06-06**: Rebuilt frontend image and deployed the draggable 3D Card Stack Swiper and kinetic reveals updates to Docker.
- **2026-06-06**: Rebuilt the shared frontend Docker image and recreated containers (landing, student, admin) to deploy the landing page Overdrive enhancements.
- **2026-06-05**: Checked impeccable skills installation via `npx impeccable skills install`.
- **2026-06-04**: Fixed watch requests 500 internal server error, updated production environment secrets, resolved deployment variable interpolation issue, and refactored deployment script to run on VPS side.
- - Initialized Ops master plan directory.
