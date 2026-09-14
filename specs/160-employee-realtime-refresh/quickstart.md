# Quickstart: Employee Workflows and Realtime Refresh

## Baseline

```bash
python3 .agents/skills/speckit-all/scripts/extract_test_commands.py --spec-dir specs/160-employee-realtime-refresh
make verify
cd frontend && npm run lint && npm run typecheck && npm run build
```

## Focused backend checks

```bash
dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AuthSession|FullyQualifiedName~Employee|FullyQualifiedName~HR|FullyQualifiedName~StaffRealtime|FullyQualifiedName~Outbox"
```

## Focused frontend and contract checks

```bash
cd frontend
npm run lint
npm run typecheck
npm run check:platform-events
node scripts/check-no-unallowlisted-reloads.mjs
```

## Browser verification

Use the same-site `.lvh.me` setup from `docs/verification-contract.md`, then:

```bash
CI=1 PLAYWRIGHT_BASE_URL=http://app.lvh.me:3000 npx playwright test tests/e2e/employee-realtime-refresh.spec.ts --project=chromium
make verify-e2e
```

The E2E spec must cover: create/update employee in session A; list/lookup update in session A; cross-session employee/HR update; permission revocation and safe redirect; duplicate event; reconnect; failed mutation rollback; and conflict while editing.

## Docker gate

```bash
docker compose config -q
make up
make ps
curl -f http://localhost:5245/api/health
curl -f http://localhost:3001/ready
```

Run `make migrate` only when the implementation adds a migration. Record all unavailable secrets/services and do not mark the phase complete without evidence.
