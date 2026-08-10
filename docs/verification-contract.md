# Platform Verification Contract

Last updated: 2026-06-30

This document is the current verification contract for Phase 0 remediation and the remaining Phase 1 auth/session browser gates.

## Root Gates

Run from the repository root:

```bash
make verify
```

Equivalent expanded commands:

```bash
dotnet restore backend/NaderGorge.sln
dotnet build backend/NaderGorge.sln --no-restore
dotnet test backend/NaderGorge.sln --no-build
cd frontend && npm run check:recharge-match-diagnosis && npm run lint && npm run build
cd worker && npm run build
docker compose config -q
```

If full backend solution tests require unavailable local services, run the focused backend subset for the active feature and record the exact blocker in `achievements.md`.

## Frontend Scripts

- `npm run lint`: ESLint gate.
- `npm run build`: Next.js production build and TypeScript compile gate.
- `npm run typecheck`: standalone TypeScript compile check.
- `npm run check:recharge-match-diagnosis`: focused Node test for recharge-match diagnosis copy and status presentation.
- `npm test`: Playwright browser test suite.
- `npm run test:e2e`: Playwright browser test suite alias.

## Phase 1 E2E Auth/Session Gate

The browser auth flow must use one same-site local domain family. `localhost` and `app.localhost` do not share the backend refresh cookie reliably.

Start backend in E2E mode:

```bash
ASPNETCORE_ENVIRONMENT=E2e \
CookieSettings__Domain=.lvh.me \
Cors__AllowedOrigins=http://app.lvh.me:3000,http://admin.lvh.me:3000,http://staff.lvh.me:3000,http://teacher.lvh.me:3000 \
dotnet run --project backend/src/NaderGorge.API/NaderGorge.API.csproj --urls http://0.0.0.0:5245
```

Run the focused browser smoke:

```bash
make verify-e2e
```

Expected:

- Playwright starts Next on `http://app.lvh.me:3000`.
- Browser API calls use `http://api.lvh.me:5245/api`.
- Refresh cookies can be issued for `.lvh.me` and sent back to `/api/auth/refresh`.
- Parent report invalid/expired token checks pass.
- Staff/assistant direct admin URLs deny access without exposing protected content.

## Employee/realtime/live-support contract gates

The focused Playwright files are:

```bash
cd frontend
npx playwright test tests/e2e/employee-realtime-refresh.spec.ts \
  tests/e2e/realtime-reconciliation.spec.ts \
  tests/e2e/live-support.spec.ts \
  tests/e2e/signalr-events.spec.ts
```

These tests use the real E2E API at `E2E_API_URL` (default `http://api.lvh.me:5245/api`) and seed through `/api/e2e/seed`; they do not treat intercepted responses as backend evidence. A test that cannot reach the E2E backend or its documented seed calls `test.skip` with the blocker. The `live support client boundary contracts (synthetic HTTP only)` group intentionally exercises malformed/late client-boundary responses and must be reported separately from real backend/SignalR results.

## Generated Artifacts

Generated outputs must not be tracked as source:

- `frontend/playwright-report/`
- `frontend/test-results/`
- `.next/`
- `worker/dist/`
- Python caches (`__pycache__/`, `*.pyc`, `.pytest_cache/`)
- Mobile build/cache outputs (`**/.gradle/`, `**/build/`, `**/gradle-*.zip`)

Use:

```bash
git ls-files frontend/playwright-report frontend/test-results worker/dist
git check-ignore frontend/playwright-report/index.html frontend/test-results/.last-run.json
```

## Deploy Safety

`make deploy` intentionally refuses to run. It no longer stages, commits, merges, or pushes arbitrary dirty worktree changes.

Production targets require explicit key-based SSH:

```bash
PROD_SSH_HOST=root@example.com make deploy-production
PROD_SSH_HOST=root@example.com make migrate-production
PROD_SSH_HOST=root@example.com make logs-production
```

## Secret Rotation Note

The previous Makefile contained a hardcoded production SSH password. Removing it from source does not rotate the credential. The credential owner must rotate the exposed password and replace production access with managed SSH keys or CI/CD secrets.

## Docker Gate

Always run:

```bash
docker compose config -q
```

Full `docker compose up` requires local secret values for required app secrets such as `API_CALLBACK_SECRET`, `AI_CALLBACK_SECRET`, `WORKER_ADMIN_TOKEN`, and `PARENT_REPORT_SIGNING_SECRET`.

## Production TLS and Protected Assets Contract

The checked-in Nginx proxy may run behind an external TLS terminator. In that deployment shape, the external terminator must enforce HTTPS, pass `X-Forwarded-Proto`, preserve `Host`, and forward only trusted Massar origins. If Nginx is used as the direct public ingress, add a production-specific 443 server block with mounted certificate paths before enabling public traffic.

Protected assets must be served only through backend authorization or signed download routes that emit `X-Accel-Redirect` to the internal `/secured-assets/` location. The public `assets.massar-academy.net` host must not expose `/protected/` or `/uploads/resources/` directly.
