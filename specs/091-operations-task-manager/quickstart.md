# Operations Task Manager: Quickstart Guide

This guide describes how to run and test the operations task manager feature.

## 1. Apply Database Migrations

Ensure PostgreSQL is running, then generate and apply the migrations:

```bash
# From workspace root:
cd backend
dotnet ef migrations add AddOperationsTaskEntities --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API
dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API
```

## 2. Run Tests

To verify domain and business logic constraints:

```bash
# Backend unit tests:
cd backend
dotnet test
```

To run Playwright E2E tests:

```bash
# Frontend E2E tests:
cd frontend
npm run test:e2e
```
