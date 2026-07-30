# Quickstart & Verification: Teacher Profile & Content Visibility

## Local setup

```bash
docker compose config -q
make up
```

Apply the generated EF migration using the repository's migration service/command before starting the API against PostgreSQL.

## Focused backend verification

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Teacher"
dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj -c Release
```

The focused tests must cover: Admin update persistence, duplicate phone rejection, password write-only behavior, non-Admin denial, independent states, public/student filtering, previous-purchaser denial, restore, audit, and idempotency.

## Frontend verification

```bash
cd frontend
npm run lint
npm run typecheck
npm run build
```

Manual Admin flow: open `/admin/teachers`, edit a teacher, set optional new password, save, reload, hide/show teacher, hide/show content, and verify the status badges and confirmation/error states.

## API/public verification

With a visitor, a student who purchased teacher content, and an Admin session:

1. Verify the teacher and content are visible before the mutation.
2. Hide the teacher only; verify teacher discovery is absent while independent content state remains unchanged according to the approved rules.
3. Show the teacher and hide content; verify visitor/student lists, course/package projections, community/related surfaces, and direct content access do not reveal or open it.
4. Verify the previous purchase/grant rows remain in Admin history and the student is denied while content is hidden.
5. Show content; verify the same student can access it again without a new purchase.
6. Check the audit log contains the Admin actor and old/new visibility values, with no password hash/value.

## Docker/runtime gate

```bash
docker compose ps
curl -fsS http://127.0.0.1:5245/api/health
curl -fsS http://127.0.0.1:3001/ready
```

Expected result: database, Redis, backend, worker, and all frontend surfaces are healthy; backend and worker health responses report healthy dependencies.
