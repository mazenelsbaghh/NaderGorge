# Implementation Plan: Student Birthday Greetings & Video Exam Progression

**Branch**: `066-birthday-and-locked-videos` | **Date**: 2026-06-01 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/066-birthday-and-locked-videos/spec.md)
**Input**: Feature specification from `/specs/066-birthday-and-locked-videos/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

This plan outlines the implementation of:
1. A standalone daily script inside the Node.js worker project that queries the database for students celebrating their birthday, inserts an in-app `NotificationEvent`, and sends a WhatsApp message using the Evolution API.
2. A sequential video progression lock where videos are returned sorted by `Order` ascending, and any video following an unpassed video exam is marked locked (`IsLocked = true`). The custom player (`SecureVideoPlayer.tsx`) and the carousel navigation are updated to visually display the lock and direct the student to the exam.

## Technical Context

**Language/Version**: TypeScript 5.x / React 19 / Next.js 16.2.1 (Frontend), C# 13 / .NET 9.0 (Backend), Node.js v20+ (Worker)  
**Primary Dependencies**: `pg` (Node PostgreSQL driver), `axios`, `@tailwindcss/postcss`, `lucide-react`, `framer-motion` (Frontend); `MediatR`, `EF Core 9.0` (Backend)  
**Storage**: PostgreSQL 16  
**Testing**: Playwright (E2E), .NET xUnit/testing framework  
**Target Platform**: Docker-compose (Linux Containers)  
**Project Type**: Multi-tier Web Application + Background Worker  
**Performance Goals**: API response time < 500ms (p95), birthday script completion < 2 minutes  
**Constraints**: Egypt timezone (`Africa/Cairo`) for date calculations, Evolution API compatibility  
**Scale/Scope**: Scales to tens of thousands of active students and daily cron execution  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Enforced. Backend changes are isolated to the `GetLessonDetailQuery` in the Application layer, and domain entities are reused. Frontend changes are isolated to `SecureVideoPlayer.tsx` and `LessonCarousel.tsx`.
- **Provider Abstraction First**: Enforced. WhatsApp Evolution API is called via HttpClient patterns or Node fetch abstraction.
- **Security & Access Control by Default**: Enforced. Students can only see videos they have active access to, and progression locks cannot be bypassed client-side.
- **Academic Content Integrity**: Enforced. Sequential learning progression is locked programmatically by exam completion status.

## Project Structure

### Documentation (this feature)

```text
specs/066-birthday-and-locked-videos/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
backend/
└── src/
    ├── NaderGorge.Application/
    │   └── Features/Content/Queries/
    │       └── GetLessonDetailQuery.cs
    └── NaderGorge.Domain/
        └── Entities/
            └── Notifications/
                └── NotificationEvent.cs

frontend/
└── src/
    ├── app/student/packages/[packageId]/lessons/[lessonId]/components/
    │   └── LessonCarousel.tsx
    ├── components/video/
    │   └── SecureVideoPlayer.tsx
    └── services/
        └── content-service.ts

worker/
└── src/
    └── scripts/
        └── birthday-congratulator.ts
```

**Structure Decision**: Web application and worker. The daily birthday congratulator script will be added in `worker/src/scripts/birthday-congratulator.ts`, and frontend/backend components will be modified.

## Complexity Tracking

> **No Constitution Check violations identified.**
