# Quickstart: Platform Verification Hygiene and Phase 1 Closure

## 1. Baseline

```bash
git status --short
docker compose config -q
```

Record unrelated dirty files before implementation. Do not revert existing user changes.

## 2. Root Verification

```bash
make verify
```

Expected:

- backend restore/build/test command sequence is documented and runnable.
- frontend lint/build pass.
- worker build passes.
- Docker compose config validates.

## 3. Phase 1 Browser Smoke

Start backend in E2E mode with aligned E2E domains:

```bash
ASPNETCORE_ENVIRONMENT=E2e \
CookieSettings__Domain=.lvh.me \
Cors__AllowedOrigins=http://app.lvh.me:3000,http://admin.lvh.me:3000,http://staff.lvh.me:3000,http://teacher.lvh.me:3000 \
dotnet run --project backend/src/NaderGorge.API/NaderGorge.API.csproj --urls http://0.0.0.0:5245
```

Run Playwright:

```bash
cd frontend
NEXT_PUBLIC_API_URL=http://api.lvh.me:5245/api \
NEXT_PUBLIC_BACKEND_URL=http://api.lvh.me:5245 \
npx playwright test tests/e2e/auth.spec.ts tests/e2e/admin-users.spec.ts tests/e2e/parent-report.spec.ts --project=chromium -g "Phase 1|Parent report"
```

Expected:

- Playwright starts or reuses a frontend server on port 3000.
- refresh-cookie hydration works on the same local site family.
- admin direct-route denial is observable.
- parent report invalid/expired token checks pass.

## 4. Hygiene Check

```bash
git status --short
git ls-files frontend/playwright-report frontend/test-results
```

Expected:

- Generated Playwright report/result files are ignored or removed from tracking.
- Newly generated reports do not appear as source changes.

## 5. Deploy Safety Check

```bash
make help
rg -n "sshpass|git add \\.|git commit|git merge|git push prod|72\\.62\\.27\\.189|MazenElsbagh" Makefile
```

Expected:

- No hardcoded SSH password remains.
- Deploy targets do not stage/commit/merge arbitrary local changes.
- Production operations require explicit key-based SSH/remote configuration.
