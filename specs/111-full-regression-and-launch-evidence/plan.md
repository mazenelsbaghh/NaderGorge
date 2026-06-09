# Technical Plan: Full Regression and Launch Evidence

Verify that the entire Nader Gorge platform is fully stable, isolated, warning-free, and ready for deployment.

## Technical Tasks to Execute

### 1. Build Verification & Code Compilations
- **C# Backend**: Run `dotnet build backend/NaderGorge.sln` to confirm no C# compiler errors or warnings remain.
- **NodeJS Worker**: Run `npm run build` in `worker/` directory to confirm clean TypeScript compilation.
- **NextJS Frontend**: Run `npm run lint` and `npm run build` in `frontend/` directory to ensure zero ESLint/TypeScript/NextJS compiler issues.

### 2. Test Suite Execution (Regression)
- **C# Tests**: Run `dotnet test backend/NaderGorge.sln --no-build` to execute all backend unit/integration tests (81 tests).
- **Python E2E Tests**: Run `.venv/bin/python -m pytest tests/` to verify endpoint flows, permissions boundaries, and database seeds (37 tests).
- **Frontend Playwright E2E Tests**: Run `npx playwright test` (or `npm run test:e2e -- --project=chromium`) inside `frontend/` directory to confirm frontend UI/routing features.
  > [!NOTE]
  > We will ensure the local dev server or Docker containers are active before running Playwright E2E tests, matching the Playwright config webServer configuration.

### 3. Static Configurations & Docker Config
- **Endpoint Integrity**: Run `node scripts/generate-endpoint-inventory.mjs --check` to ensure the generated inventory JSON/MD files match the actual C#/JS contracts.
- **Nginx Reverse Proxy static separation**: Run `node scripts/verify-surface-separation.mjs --static-only` to check subdomain blocks and catch-all permalinks.
- **Docker Compose validation**: Run `docker compose config -q` to verify compose syntax.

### 4. Docker Cold-Start
Execute a clean cold-start sequence to confirm orchestration stability:
1. Stop all containers: `make down`
2. Rebuild backend/frontend/nginx/worker clean from scratch: `docker compose build --no-cache`
3. Bring services up: `make up`
4. Apply migrations: `make migrate`
5. Print container status: `make ps`
6. Run full isolation tests: `node scripts/verify-surface-separation.mjs`

### 5. Backup, Restore & Rollback Strategy
Document backup/restore scripts and rollback procedures to ensure safe VPS updates.
- **Backup Database**: pg_dump matching the container setup.
- **Rollback**: revert to git tag/commit and restart docker containers.
