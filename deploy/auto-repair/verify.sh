#!/usr/bin/env bash
set -euo pipefail
cp -a /opt/cache/npm /tmp/repair-npm-cache
export npm_config_cache=/tmp/repair-npm-cache
export AiMediaRelay__Secret="$(node -e "process.stdout.write(require('crypto').randomBytes(32).toString('hex'))")"
node /opt/repair/fonts.mjs --serve &
repair_font_pid=$!
repair_api_pid=''
cleanup() {
  kill "$repair_font_pid" 2>/dev/null || true
  if [ -n "$repair_api_pid" ]; then kill "$repair_api_pid" 2>/dev/null || true; fi
}
trap cleanup EXIT
export NEXT_FONT_GOOGLE_MOCKED_RESPONSES=/opt/cache/fonts/responses.cjs
# Dependency audit/restore happens while building the image. Runtime verification
# uses only that cache and fails if a dependency is missing.
printf '<configuration><packageSources><clear /></packageSources></configuration>\n' >/tmp/repair-nuget.config
dotnet restore backend/NaderGorge.sln --configfile /tmp/repair-nuget.config -p:NuGetAudit=false
dotnet build backend/NaderGorge.sln --no-restore
export ConnectionStrings__Redis=redis:6379
export Redis__ConnectionString=redis:6379
# Several integration fixtures reset their database. Assemblies must not race
# against the AutoRepair lifecycle test's independently named repair_test DB.
# The legacy timestamp switch must be set before Npgsql caches its first model.
# Run the strict migration/lifecycle fixture in a fresh process, never suppress its guard.
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~AutoRepairPostgresTests' --logger 'trx;LogFileName=repair-postgres.trx'
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-build \
  --filter 'FullyQualifiedName!~AutoRepairPostgresTests' --logger 'trx;LogFileName=repair.trx'
export ConnectionStrings__DefaultConnection='Host=127.0.0.1;Database=massar_live_support_query_budget_disposable_repair;Username=postgres'
export MASSAR_LEARNING_TEST_CONNECTION='Host=127.0.0.1;Database=massar_learning_test;Username=postgres'
export LIVE_SUPPORT_QUERY_BUDGET_DATABASE_AUTHORIZATION=DELETE-DISPOSABLE-LIVE-SUPPORT-QUERY-BUDGET-DATABASE
dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --no-build --logger 'trx;LogFileName=repair.trx'
node - <<'JS'
const fs = require('fs');
for (const file of [
  'backend/tests/NaderGorge.Application.Tests/TestResults/repair-postgres.trx',
  'backend/tests/NaderGorge.Application.Tests/TestResults/repair.trx',
  'backend/tests/NaderGorge.Integration.Tests/TestResults/repair.trx',
]) {
  const counters = fs.readFileSync(file, 'utf8').match(/<Counters\b([^>]+)\/>/);
  if (!counters) throw new Error('Missing backend test counters: ' + file);
  const counts = Object.fromEntries([...counters[1].matchAll(/(\w+)="(\d+)"/g)].map(([, key, value]) => [key, Number(value)]));
  if (!counts.total || counts.executed !== counts.total || counts.passed !== counts.total) {
    throw new Error('Backend gate requires executed, passing tests without skips: ' + file);
  }
}
JS
npm --prefix frontend ci --offline --ignore-scripts
npm --prefix frontend run lint
export NEXT_PUBLIC_API_URL=http://api.lvh.me:5245/api
export NEXT_PUBLIC_BACKEND_URL=http://api.lvh.me:5245
npm --prefix frontend run build
npm --prefix worker ci --offline --ignore-scripts
npm --prefix worker test

# Real browser requests use only the disposable PostgreSQL network namespace.
export ASPNETCORE_ENVIRONMENT=E2e
export ConnectionStrings__DefaultConnection="$AUTO_REPAIR_TEST_DB"
export ConnectionStrings__Redis=redis:6379
export Redis__ConnectionString=redis:6379
export CookieSettings__Domain=.lvh.me
dotnet run --no-build --no-launch-profile --project backend/src/NaderGorge.API/NaderGorge.API.csproj \
  --urls http://0.0.0.0:5245 >/tmp/repair-e2e-api.log 2>&1 &
repair_api_pid=$!
ready=false
for _ in $(seq 1 60); do
  if curl --fail --silent http://api.lvh.me:5245/api/health/ready >/dev/null; then ready=true; break; fi
  sleep 1
done
if [ "$ready" != true ]; then tail -n 30 /tmp/repair-e2e-api.log; exit 1; fi
export CI=1 PLAYWRIGHT_USE_PRODUCTION_BUILD=1 PLAYWRIGHT_JSON_OUTPUT_FILE=/tmp/repair-browser.json
cd frontend
./node_modules/.bin/playwright test tests/e2e/auth.spec.ts tests/e2e/admin-users.spec.ts \
  tests/e2e/parent-report.spec.ts tests/e2e/lesson-context-menu-guard.spec.ts \
  --project=chromium -g 'Phase 1|Parent report|lesson video context-menu guard' --reporter=json
node - <<'JS'
const report = require('/tmp/repair-browser.json');
if (!report.stats.expected || report.stats.skipped || report.stats.unexpected || report.stats.flaky) {
  throw new Error('Browser gate requires executed, passing tests without skips or flaky retries');
}
JS
