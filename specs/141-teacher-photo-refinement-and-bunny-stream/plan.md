# Implementation Plan: Teacher Photo Refinement and Bunny Stream

**Branch**: `141-teacher-photo-refinement-and-bunny-stream` | **Date**: 2026-06-20 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/141-teacher-photo-refinement-and-bunny-stream/spec.md)

## Summary

This plan addresses:
1. Fetching and showing the active AI reference photo when editing a teacher or using `/admin/ai-monitor`, ensuring uploads target the selected teacher.
2. Enabling the worker to extract audio tracks from Bunny Stream videos using `yt-dlp` with the correct Referer headers.

---

## Technical Context

- **Language/Version**: C# (.NET 9) Backend, TypeScript (Next.js 16.2.1 / React 19) Frontend, Node.js (v20) Worker.
- **Primary Dependencies**: Entity Framework Core 9.0, MediatR, `@google/genai`, `yt-dlp`, `fluent-ffmpeg`, BullMQ.
- **Storage**: PostgreSQL (TeacherPhotos, LessonVideos, BunnyVideoAssets), Local Storage (`.tmp` folder inside worker/backend).
- **Testing**: pytest (Python), dotnet test (C#), Playwright (E2E).
- **Target Platform**: Docker-compose runtime (Mac/Linux).
- **Project Type**: Web application (Frontend + Backend + Worker).

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- Layer impact across backend, frontend, worker, database, and Docker.
- Automated tests required for the phase's critical paths.
- Manual QA flows required from the product owner.
- Docker gate commands (`docker compose config -q`, `make up`, `make migrate`).
- Explicit decision that the next phase cannot start until failed gates are fixed.

---

## Project Structure

### Documentation (this feature)

```text
specs/141-teacher-photo-refinement-and-bunny-stream/
├── plan.md              # This file
├── research.md          # Design decisions and alternatives
├── data-model.md        # Entity relations and traversal paths
├── quickstart.md        # Execution and manual verification steps
├── contracts/
│   └── active-photo-api.md  # Request/response DTO contracts
└── tasks.md             # Task checklist (Phase 4)
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.API/
│   ├── Controllers/AdminController.cs
│   └── Controllers/TeacherController.cs
├── src/NaderGorge.Application/
│   └── Features/Admin/Queries/AdminTeacherQueries.cs
└── src/NaderGorge.Infrastructure/
    └── Providers/BunnyVideoProvider.cs

frontend/
├── src/
│   ├── app/admin/ai-monitor/AIMonitorPageClient.tsx
│   ├── app/admin/teachers/AdminTeachersPageClient.tsx
│   ├── components/admin/AdminTeacherPhotoUpload.tsx
│   └── services/
│       ├── admin-service.ts
│       └── teacher-service.ts

worker/
├── src/
│   └── utils/audioExtractor.ts

docker-compose.yml
```

**Structure Decision**: Standard multi-tier repository structure containing frontend web app, C# backend API, and Node.js worker service.

---

## Phase Closure & Verification Plan

### Automated Tests Required
- Run backend queries unit tests:
  ```bash
  dotnet test backend/tests/NaderGorge.Application.Tests
  ```
- Run Python video API integration tests (ensure no regression):
  ```bash
  .venv/bin/python -m pytest tests/test_video.py -q
  ```

### Docker Gate Required
- `docker compose config -q`
- `make up` (to rebuild worker and backend containers)
- `docker compose ps` (check all services are running and healthy)

### Manual QA Required
- Log in to the Admin Panel.
- Edit a teacher and verify their AI reference photo preview is fetched and displayed.
- Go to AI Monitor, select a teacher, verify their AI photo preview loads, and upload a new photo. Check that the DB saves it for that teacher.
- Trigger AI Analysis on a Bunny Stream video, inspect worker logs, and ensure audio is downloaded and converted successfully.

### End-of-Phase Report Format
- Implemented scope, commands run, test results, Docker status, manual QA checklist, and final readiness.
