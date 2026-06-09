# Implementation Plan: Media Production Pipeline and Social Planner

**Branch**: `095-media-production-social-planner` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/095-media-production-social-planner/spec.md`

## Summary

This feature implements the **Media Production Pipeline** and **Social Planner** to manage content preparation, filming, editing, and publishing operations.
- **Backend**: Add `MediaProductionPipeline` and `SocialMediaPlan` entities. Map them in `IAppDbContext` and `AppDbContext`. Implement CQRS commands/queries for task tracking, pipeline transitions, and metrics. Integrate with the existing approval pipeline system (Phase 3) so transitioning to the `Review` stage automatically creates an approval request.
- **Frontend**: Add `/admin/media` page (for managers to assign, schedule, approve, and view reports) and operations dashboard elements for assistants/editors. Build glassmorphic pipeline board cards and timeline components.

## Technical Context

**Language/Version**: C# 13 (.NET 9), TypeScript 5.x (React 19 / Next.js 16.2)  
**Primary Dependencies**: MediatR, Entity Framework Core 9.0, Framer Motion, Lucide React, Axios  
**Storage**: PostgreSQL (for persistent entities), Redis (already used for BullMQ and locking)  
**Testing**: xUnit for C# backend tests  
**Target Platform**: Linux server (Docker) / Web browsers  
**Project Type**: Web Application  
**Performance Goals**: Dashboard KPI aggregations in under 2 seconds; API responses under 200ms  
**Constraints**: Enforce strict role-based and permission boundaries (`media.manage` permission required for updates)  
**Scale/Scope**: ~100 daily production items, 5-10 editors  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Core changes in Domain (Entities), Infrastructure (DB context and migrations), Application (MediatR CQRS), API (Controllers), and Frontend (Service layer and views).
- **Automated tests**: Backend unit tests will cover stage transition constraints (cannot transition to Published without approval, PublishedAt updates) and approval linkage.
- **Manual QA**: Product owner will manually test creating a pipeline item, assigning an editor, submitting for review, approving/rejecting via approvals dashboard, and checking the KPI reports.
- **Docker Gate**: Ensure containerized environment builds cleanly and migrations run correctly.

## Project Structure

### Documentation (this feature)

```text
specs/095-media-production-social-planner/
├── plan.md              # This file
├── research.md          # Research on stages, permissions, and approval linkage
├── data-model.md        # Database schema definitions
├── quickstart.md        # Quick guide for running and verifying the feature
└── contracts/
    └── endpoints.md     # API endpoints design
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   ├── MediaProductionPipeline.cs
│   │   │   └── SocialMediaPlan.cs
│   │   └── Enums/
│   │       ├── MediaStage.cs
│   │       ├── SocialPlatform.cs
│   │       └── SocialPlanStatus.cs
│   ├── NaderGorge.Application/
│   │   └── Features/
│   │       └── Admin/
│   │           └── Media/
│   │               ├── Commands/
│   │               │   ├── CreateMediaPipelineCommand.cs
│   │               │   ├── UpdateMediaPipelineCommand.cs
│   │               │   └── CreateSocialPlanCommand.cs
│   │               └── Queries/
│   │                   ├── GetMediaPipelinesQuery.cs
│   │                   ├── GetSocialPlansQuery.cs
│   │                   └── GetMediaKpisQuery.cs
│   └── NaderGorge.API/
│       └── Controllers/
│           └── AdminMediaController.cs
└── tests/
    └── NaderGorge.Application.Tests/
        └── Media/
            └── MediaPipelineTests.cs

frontend/
├── src/
│   ├── app/
│   │   └── admin/
│   │       └── media/
│   │           └── page.tsx
│   ├── components/
│   │   └── media/
│   │       ├── MediaPipelineBoard.tsx
│   │       ├── MediaKpiDashboard.tsx
│   │       └── SocialPlannerView.tsx
│   └── services/
│       └── media-service.ts
```

**Structure Decision**: The backend and frontend directories mapped above follow the project standard multi-project Next.js and .NET solution structure.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Backend test command: `dotnet test NaderGorge.sln --filter Category=Media`
- Critical paths covered: pipeline creation, stage transitions block without approval, auto-creation of approval requests.

**Docker Gate Required**:
- `make up` and `make migrate`
- Verify backend API is reachable at `http://localhost:5245/api/admin/media`

**Manual QA Required**:
1. Log in as Admin -> Go to `/admin/media` -> Create a video pipeline item and assign to an editor.
2. Log in as Editor (Assistant) -> Go to task panel -> Set stage to `Review`.
3. Log in as Supervisor -> Go to approvals -> Approve the item (assert status is now `Approved`).
4. Log in as Manager -> Publish the item (assert `PublishedAt` is populated).

**End-of-Phase Report Format**:
Summarize implemented scope, test results showing all tests green, ESLint and next build results, and overall confirmation of readiness.
