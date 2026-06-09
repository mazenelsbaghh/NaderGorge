# Quickstart & Rollback Guide: Phase 12

This guide outlines the commands and procedures to run the regression suite, perform launch drills, and execute rollback procedures for the Massar Platform expansion.

---

## 1. Regression Suite Verification

To run all automated verification checks:

### Backend Build and Unit Tests
```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-build
```

### Frontend Typecheck, Lint, and Build
```bash
cd frontend
npm run lint
npm run build
cd ..
```

### Worker Compilation
```bash
cd worker
npm run build
cd ..
```

### Python E2E Integration Tests
```bash
python3 -m pip install -r tests/requirements.txt
python3 -m pytest
```

### Static and Runtime Verification Scripts
```bash
node scripts/generate-endpoint-inventory.mjs --check
docker compose config -q
node scripts/verify-surface-separation.mjs
```

---

## 2. Docker Launch & Database Drills

### Cold Start Launch Drill
1. Spin down and clean up:
   ```bash
   make down
   # OPTIONAL: Remove database volumes to test clean schema migrations
   docker volume rm nader-gorge_pgdata || true
   ```
2. Build container images without cache:
   ```bash
   docker compose build --no-cache
   ```
3. Boot the stack:
   ```bash
   make up
   ```
4. Verify all services are healthy and running:
   ```bash
   make ps
   ```

### Database Seeder Verification
Confirm database seeders execute correctly:
```bash
docker compose exec backend dotnet run --project src/NaderGorge.API/NaderGorge.API.csproj --seed
```

### Database Backup & Restore Drill
1. **Backup**: Run a PostgreSQL pg_dump within the DB container to create a backup file:
   ```bash
   docker exec -t massar_db pg_dump -U postgres -d nadergorge_db > backup.sql
   ```
2. **Restore**: Re-import the backup file into a clean database:
   ```bash
   # Terminate active connections and drop/create the DB
   docker exec -it massar_db psql -U postgres -c "DROP DATABASE nadergorge_db WITH (FORCE);"
   docker exec -it massar_db psql -U postgres -c "CREATE DATABASE nadergorge_db;"
   # Restore schema and data
   docker exec -i massar_db psql -U postgres -d nadergorge_db < backup.sql
   ```

---

## 3. Rollback Procedures

### EF Core Database Migration Rollback
If a database migration causes errors in production, roll back to a specific stable migration:

1. Identify the list of applied migrations:
   ```bash
   docker compose exec backend dotnet ef migrations list --project src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj --startup-project src/NaderGorge.API/NaderGorge.API.csproj
   ```
2. Roll back database schema to a specific migration target (e.g., `AddHREntities`):
   ```bash
   docker compose exec backend dotnet ef database update AddHREntities --project src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj --startup-project src/NaderGorge.API/NaderGorge.API.csproj
   ```
3. Remove the migration definition from the code (if needed):
   ```bash
   docker compose exec backend dotnet ef migrations remove --project src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj --startup-project src/NaderGorge.API/NaderGorge.API.csproj
   ```

### Application / Environment Rollback
To revert Docker deployments or environment overrides:
1. Revert environment flags in `.env` or Docker Compose variables.
2. Pull/checkout the last stable git tag:
   ```bash
   git checkout tags/v1.0.0-stable
   ```
3. Re-execute the container build and startup:
   ```bash
   make down && make up
   ```
