# Quickstart: Real-time Speed Remaining Completion

This guide explains how to set up and run the application after implementing the remaining real-time speed completion features.

## 1. Apply Database Migration

Since we introduced the `WebVitalsMetrics` table, a database migration is required:

```bash
cd backend
# Scaffold the migration
dotnet ef migrations add AddWebVitalsMetricsTable --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API

# Apply the migration locally
dotnet ef database update --project src/NaderGorge.API
```

---

## 2. Frontend Configuration & Running

Ensure the Next.js frontend has `@next/bundle-analyzer` installed. Run the development server:

```bash
cd frontend
npm install
npm run dev
```

To run a production build and generate a bundle analysis:

```bash
cd frontend
npm run analyze
```

---

## 3. Verify Real-time SignalR Events

Ensure that Redis is running (used as the backplane for outbox event notifications):

```bash
docker compose up -d redis
```

Start both the backend and worker:

```bash
# Terminal 1 - Backend
cd backend
dotnet run --project src/NaderGorge.API

# Terminal 2 - Node Worker
cd worker
npm run dev
```
