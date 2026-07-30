# Quickstart Verification: مركز حسابات المدرسين والمالية

## Cutover safety

The `20260724185632_AddTeacherFinanceCenter` migration is additive: it creates
the finance-center tables and adds nullable snapshot references to legacy
allocations. It does not backfill legacy events, map historical payouts to
allocations, or change historic money values. Do not mark legacy balances as
settled automatically. Reconcile them later as audited opening items after a
finance-owner review; the implementation for that reconciliation remains
pending.

## Automated verification

Run the following from the repository root:

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Teacher"
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~TeacherFinanceCenter"
dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj -c Release --no-restore
(cd frontend && npm run lint && npm run build)
docker compose config -q
```

The first command is the requested pre-change baseline command. It was not
captured before finance writes began, so a result run now must be recorded as a
current regression result, not a baseline.

## Docker and live-environment verification

After the automated checks pass and only in an approved environment with the
required runtime secrets, run:

```bash
make up
make migrate
make ps
```

Then confirm the backend health endpoint and the admin surface are healthy.
Live Bunny verification additionally requires valid `BUNNY_STREAM_LIBRARY_ID`
and `BUNNY_STREAM_API_KEY`; without them, verify only the fixture-based
actual/estimated/missing scenarios and record live Bunny as unavailable.

## Manual QA

1. As Admin, create a teacher with a 30% default agreement and a 60 EGP fixed lesson agreement. Fund a student and purchase the lesson. Confirm exactly one 60 EGP allocation.
2. Apply a discount once for platform, once for teacher, and once split. Confirm each snapshot and platform/teacher share match the selected burden.
3. Create one delivery-triggered code group, confirm delivery, then activate a code. Confirm one delivery allocation only. Repeat for an activation-triggered group and confirm one allocation per successful activation.
4. Sell a shared package with mixed allocations. Confirm loss acknowledgement is required only when the calculated platform remainder is negative.
5. Create, review, approve and pay a settlement. Confirm its lines cannot be selected again. Create a partial reversal and choose debt or next-settlement deduction.
6. Sync Bunny usage with valid credentials. Confirm USD cost and actual/estimated/missing provenance roll up to video, lesson, package and teacher without entering EGP totals.
7. Attempt every finance-center API/route as a non-admin user and confirm the request is forbidden.

## Verification record (2026-07-24)

| Check | Result | Evidence / limitation |
| --- | --- | --- |
| Pre-change teacher-test baseline | Not available | It was not captured before financial writes began; do not infer a baseline from a later run. |
| Finance feature tests | Passed | 38/38 passed on 2026-07-25 using the focused Finance/teacher-accounting filter. |
| Release API build | Passed | `dotnet build ... -c Release --no-restore` completed with 0 errors. |
| Frontend lint/build | Passed | `npm run lint` and `npm run build` completed successfully. |
| `docker compose config -q` | Passed | Executed successfully on 2026-07-24. |
| `make up`, `make migrate`, `make ps`, health checks | Not run | Intentionally excluded from this documentation-only pass. |
| Live Bunny sync | Not run | Requires valid Bunny credentials and a running approved environment. |
| Manual QA | Not run | Requires an authenticated admin/non-admin environment and representative data. |
