# Implementation Plan: Phase 2 — Structured Learning and Academic Operations

**Branch**: `007-phase2-academic-ops` | **Date**: 2026-03-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/007-phase2-academic-ops/spec.md`

## Summary

This phase transforms the platform from a content delivery MVP into a robust learning operations system. It adds a Homework grading system (with MCQ, essay support, and Assistant Dashboard routing), an automated Student Commitment Engine (tracking attendance, missed homeworks, and triggering warnings), a Parent Reporting layer, Notification infrastructure, and a Gamification system (Points, Badges, Leaderboard, Streaks). The technical approach relies on extending standard Domain-Driven Design entities in C# and executing background evaluations via a Node.js worker hooked to Redis queues.

## Technical Context

**Language/Version**: C# (.NET 8.0/9.0), TypeScript, Node.js.
**Primary Dependencies**: Entity Framework Core, React Query, Zustand, BullMQ/ioredis, Tailwind CSS.
**Storage**: PostgreSQL, Redis.
**Testing**: xUnit, Jest/Playwright (e2e).
**Target Platform**: Linux App Services / Web Browsers.
**Project Type**: Full Stack Web App (Backend API, Frontend Next.js, Background NodeJS Worker).
**Performance Goals**: Commitment Engine batch updates must process entire student base under 5 minutes without affecting API. Notifications must queue within 100ms.
**Constraints**: Background jobs (SMS, nightly sweeps) MUST run in the Node worker, not native .NET hosted services.
**Scale/Scope**: Up to 10k users. Expected 5-10 concurrent assistant editors.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Add new modules `Homework`, `Gamification`, `AssistantOps` under their respective folders.
- **Provider Abstraction First**: SMS and structured Notification sending mapped via `NotificationProvider` interface. The implementation will push to a Redis queue.
- **Security & Access Control by Default**: Assistant grading capabilities walled via RBAC with specific roles: `AssistantReviewer`, `AssistantAcademic`.
- **Phased Delivery with MVP Discipline**: Scope is restricted purely to Phase 2 limits. No AI scoring or essay automated checks yet (reserved for Phase 4).
- **Academic Content Integrity**: Homework entity will be neatly slaved to the existing `Lesson` entity. Gamification will trigger based on the established `Progress` milestones.

## Project Structure

### Documentation (this feature)

```text
specs/007-phase2-academic-ops/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output 
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.Domain/
│   ├── Entities/Homework/
│   ├── Entities/Gamification/
│   └── Entities/Notifications/
├── src/NaderGorge.Application/
│   ├── UseCases/Homework/
│   ├── UseCases/Gamification/
│   └── UseCases/AssistantOps/
├── src/NaderGorge.Infrastructure/
│   └── Background/RedisJobEnqueuer.cs
└── src/NaderGorge.API/
    ├── Controllers/HomeworkController.cs
    ├── Controllers/AssistantController.cs
    └── Controllers/GamificationController.cs

frontend/
├── src/components/
│   ├── homework/
│   ├── assistant/
│   └── gamification/
└── src/app/
    ├── student/homework/
    ├── assistant/dashboard/
    └── parent-report/

worker/
├── src/
│   ├── jobs/commitment-engine.ts
│   └── jobs/notification-sender.ts
```

**Structure Decision**: A standard Next.js frontend + .NET Web API + Node.js Worker distributed layout, appending new domains correctly to Clean Architecture folders.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | Features perfectly align with Constitution. | N/A |
