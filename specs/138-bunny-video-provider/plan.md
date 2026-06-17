# Implementation Plan: Bunny Video Provider

**Branch**: `138-bunny-video-provider` | **Date**: 2026-06-17 | **Spec**: [/specs/138-bunny-video-provider/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/138-bunny-video-provider/spec.md)
**Input**: Feature specification from `/specs/138-bunny-video-provider/spec.md`

## Summary

Add Bunny Stream as a third lesson video provider beside the existing YouTube and VK providers. The implementation keeps existing provider data intact, adds teacher/admin Bunny upload flows, stores Bunny-specific video asset metadata, exposes protected Bunny playback through the existing video-session route, and adds admin-only monthly usage/cost snapshots with storage, bandwidth, and preserved pricing rates.

### Technical Approach

1. Extend the existing `IVideoProvider` pattern with a `BunnyVideoProvider` and update create/edit validation so `youtube`, `vk`, and `bunny` are all accepted.
2. Store Bunny-specific upload, ownership, processing, and cost data in separate entities rather than bloating `LessonVideo` for non-Bunny videos.
3. Use Bunny Stream's server-authenticated APIs from the backend only. The frontend receives resumable TUS upload credentials signed by the API, never the Bunny API key.
4. Add admin-only cost report queries over monthly `BunnyUsageSnapshot` rows. Snapshots preserve the pricing rates used at calculation time.
5. Keep AI analysis routed through the existing video AI workflow, but block/queue Bunny videos until Bunny processing is ready and direct media access is available.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend, TypeScript 5.x / Next.js 16.2.1 / React 19 frontend, Node.js v20+ worker for existing AI video jobs  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, Entity Framework Core 9, PostgreSQL provider, Next.js App Router API routes, Axios service layer, Tailwind CSS, Bunny Stream HTTP API, TUS resumable upload client  
**Storage**: PostgreSQL for `LessonVideo`, Bunny asset metadata, monthly usage/cost snapshots, and platform pricing settings; Bunny Stream for uploaded video media  
**Testing**: Backend unit/integration tests where available, frontend lint/type checks, targeted Playwright/admin flow tests when the local app is runnable, Bunny client mocked for automated tests  
**Target Platform**: Dockerized Linux web platform with browser admin/teacher/student surfaces  
**Project Type**: Web application with backend API, frontend SPA/App Router routes, and background worker integrations  
**Performance Goals**: Provider selection remains instant; upload progress updates without blocking form navigation; monthly cost reports query from local snapshots without live Bunny calls in page render  
**Constraints**: Bunny credentials never ship to frontend bundles; teachers never receive cost/storage/bandwidth fields; existing YouTube/VK content remains unchanged; large uploads must support resumable behavior  
**Scale/Scope**: One Bunny Stream library in first release, many teachers/courses/lessons, monthly cost windows, admin-triggered or scheduled usage sync

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- Layer impact documented across backend API, application commands/queries, domain entities, infrastructure provider/client, EF migrations, frontend admin/teacher/student UI, Next video embed route, existing AI worker boundary, PostgreSQL, and Docker/env configuration.
- Provider abstraction remains modular: Bunny is added through a dedicated provider/client and does not replace YouTube/VK logic.
- Security gate: Bunny Stream API key remains backend-only; upload signatures are short-lived; admin-only cost endpoints require admin authorization; teacher DTOs omit cost fields.
- Data integrity gate: Bunny asset links validate teacher/package/lesson ownership; snapshots preserve historical rates and reporting periods.
- Automated tests required for provider validation, upload authorization, cost aggregation permissions, and legacy YouTube/VK compatibility.
- Docker gate commands: `docker compose config -q`, migration application in API container, API/frontend/worker/PostgreSQL/Redis health checks.
- Manual QA required for admin, teacher, student, and negative permission flows before release.

## Project Structure

### Documentation (this feature)

```text
specs/138-bunny-video-provider/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── endpoints.md
└── tasks.md
```

### Source Code

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/ContentEntities.cs
│   │   └── Interfaces/IVideoProvider.cs
│   ├── NaderGorge.Application/
│   │   ├── Common/Settings/
│   │   ├── Features/Admin/Commands/
│   │   ├── Features/Admin/Queries/
│   │   ├── Features/Student/Commands/
│   │   └── Interfaces/
│   ├── NaderGorge.Infrastructure/
│   │   ├── Data/AppDbContext.cs
│   │   ├── Migrations/
│   │   ├── Providers/BunnyVideoProvider.cs
│   │   └── Services/BunnyStreamClient.cs
│   └── NaderGorge.API/
│       ├── Controllers/AdminController.cs
│       └── Program.cs
└── tests/

frontend/
├── src/
│   ├── app/api/video/embed/route.ts
│   ├── components/admin/
│   ├── components/teacher/
│   └── services/admin-service.ts
└── tests/

worker/
└── existing video AI job paths, changed only if Bunny media access requires provider-specific job metadata
```

**Structure Decision**: Web application with separate backend, frontend, and existing worker boundaries. Bunny API integration belongs in backend infrastructure; frontend only handles provider selection, upload progress, and admin reporting UI.

## Phase 0 Research Decisions

See `/specs/138-bunny-video-provider/research.md`.

## Phase 1 Design Decisions

See `/specs/138-bunny-video-provider/data-model.md`, `/specs/138-bunny-video-provider/contracts/endpoints.md`, and `/specs/138-bunny-video-provider/quickstart.md`.

## Phase Closure & Verification Plan

**Automated Tests Required**:

```bash
dotnet test backend/NaderGorge.sln
npm --prefix frontend run lint
npm --prefix frontend run typecheck
npm --prefix frontend test -- --runInBand
```

Targeted tests must cover:
- Accepting `bunny` while preserving YouTube/VK create/edit behavior.
- Teacher upload authorization and admin teacher/package/lesson selection validation.
- Bunny TUS signature response does not include API keys.
- URL fetch handles success/failure without creating broken playable videos.
- Admin-only cost report endpoints and absence of cost fields in teacher responses.
- Monthly snapshot aggregation by video, teacher, package, and platform.

**Docker Gate Required**:

```bash
docker compose config -q
docker compose ps
```

If schema migrations are created, run the project's migration/apply command in the Docker environment before final verification.

**Manual QA Required**:

1. Admin edits an existing YouTube video and existing VK video; both remain playable and provider labels persist.
2. Admin selects Bunny in add/edit video UI and verifies Bunny-specific upload/link controls appear.
3. Teacher uploads a local Bunny video to an owned lesson with progress/resume behavior and no cost fields in teacher UI.
4. Admin uploads local file and remote URL for a selected teacher/package/lesson; attribution belongs to selected teacher.
5. Admin opens monthly Bunny reports and filters by month, teacher, and package; totals match snapshot rows.
6. Teacher attempts cost report route/API and receives denied/omitted cost data.
7. Student opens a Bunny lesson video and playback loads through the existing protected embed experience.
8. AI analysis is requested for Bunny video in processing and ready states; processing state blocks cleanly and ready state follows supported workflow.

**End-of-Phase Report Format**:

- Implemented scope
- Source and migration files changed
- Commands run and results
- Docker result
- Manual QA status
- Bunny live validation status or mocked-client limitation
- Residual risks

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Separate `BunnyVideoAsset` and `BunnyUsageSnapshot` entities | Bunny has upload status, library IDs, usage, pricing, ownership, and sync metadata that do not apply to YouTube/VK | Adding nullable columns to `LessonVideo` would pollute legacy provider rows and make cost reporting harder to secure |
| Backend Bunny client plus signed TUS flow | Large teacher uploads need resumable direct transfer without exposing the Bunny API key | Backend proxy upload would be slower, memory/timeout-prone, and less reliable for large educational videos |
| Monthly snapshots | Historical reports must keep the rates and usage values used at the time | Recomputing from current settings would rewrite history after pricing changes |
