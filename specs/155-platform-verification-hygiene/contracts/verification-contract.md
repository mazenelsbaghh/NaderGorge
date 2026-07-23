# Contract: Root Verification

## Root Command

`make verify`

## Required Behavior

The command must run these checks in order:

1. `dotnet restore backend/NaderGorge.sln`
2. `dotnet build backend/NaderGorge.sln --no-restore`
3. backend tests or documented focused subset
4. `cd frontend && npm run lint`
5. `cd frontend && npm run build`
6. `cd worker && npm run build`
7. `docker compose config -q`

## Optional/Environment-Gated Behavior

E2E browser smoke may be a separate `make verify-e2e` target because it requires a running backend and E2E seed endpoint. If E2E is included in a root command, the target must start or require the correct web server port explicitly.

## Failure Contract

- Missing scripts are not acceptable unless documented and replaced by an exact command.
- Generated artifacts must not be produced as tracked source changes.
- Full Docker startup may be skipped if required secrets are unavailable, but `docker compose config -q` must remain runnable.
