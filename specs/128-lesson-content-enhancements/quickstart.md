# Quickstart: 128 — Lesson Content Enhancements

## Prerequisites
- .NET 9 SDK
- Node.js 20+
- PostgreSQL running (local or Docker)
- Redis running (local or Docker)

## Build & Verify

```bash
# Backend
cd backend && dotnet build

# Create migration (if IsActive column not yet added)
cd backend && dotnet ef migrations add AddIsActiveToLessonVideo \
  --project src/NaderGorge.Infrastructure \
  --startup-project src/NaderGorge.API

# Apply migration
cd backend && dotnet ef database update \
  --project src/NaderGorge.Infrastructure \
  --startup-project src/NaderGorge.API

# Frontend
cd frontend && npm run build

# Run locally
make dev
```

## Testing Checklist

1. **Video Edit**: Go to lesson profile → Videos tab → click Edit → verify form is pre-filled → change title → save → verify
2. **Video Toggle**: Click eye icon → verify video dims → refresh student view → verify video hidden
3. **Resource Upload**: Go to Resources tab → switch to "رفع ملف" → upload a PDF → verify it appears
4. **Exam Profile**: Go to Exam tab → click "عرض البروفايل" → verify stats page loads → check per-question stats
5. **Auto-Save**: Start creating exam → add 2 questions → refresh page → verify questions persisted
