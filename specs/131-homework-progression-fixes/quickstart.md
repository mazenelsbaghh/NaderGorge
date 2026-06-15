# Quickstart Guide: Homework & Progression Fixes

## 1. Running the Platform Locally

Ensure you have Docker and make tools installed, then spin up the infrastructure and services:

```bash
# Start Docker environment
make up

# Apply migrations (none expected, but good practice)
make migrate
```

## 2. Compiling the Code

Verify that the changes do not break the backend build or frontend linter:

```bash
# Backend build check
cd backend && dotnet build

# Frontend build check
cd frontend && npm run build
```

## 3. Running Automated Tests

Run the E2E tests to verify locking and page behaviors:

```bash
# Run Playwright tests
cd frontend && npx playwright test tests/e2e/homework-system.spec.ts
```
