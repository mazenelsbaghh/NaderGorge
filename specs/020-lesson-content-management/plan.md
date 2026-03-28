# Implementation Plan: Lesson Content Management

**Branch**: `020-lesson-content-management` | **Date**: 2026-03-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/020-lesson-content-management/spec.md`

## Summary

Build a dedicated "Lesson Cockpit" interface and matching API endpoints to manage educational resources linked to a specific lesson, including adding videos, attaching files/documents (URLs), creating homework assignments, and assigning a pre-existing exam.

## Technical Context

**Language/Version**: C# 12 / .NET 8, TypeScript 5.x
**Primary Dependencies**: EF Core, MediatR, Next.js, React, Tailwind CSS
**Storage**: PostgreSQL (Existing Tables: `Lesson`, `LessonVideo`, `LessonResource`, `Homework`)
**Testing**: xUnit, Jest/Vitest
**Target Platform**: Linux Server (Backend) / Vercel or similar (Frontend)
**Project Type**: Full-stack application (Web API + React Frontend)
**Performance Goals**: < 200ms latency for adding content to lessons
**Constraints**: Follow current project design conventions ("Editorial Scholar"). Leverage abstractions instead of hardcoded integrations where possible.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Handled. Commands belong to the `Admin` or `Homework` boundaries as appropriate.
- **Provider Abstraction First**: Handled. Video is using `Provider` string representation. Files rely on URLs to be abstraction-friendly.
- **Security & Access Control**: Admin attributes and policy checks will protect the new creation commands.
- **Academic Content Integrity**: Maintains hierarchical order: Package -> Term -> Section -> Lesson -> [Content items].
- **Observability**: Standard Mediator behavior pipelines will log interactions.
- **Premium Editorial Design System**: The new `app/admin/content/lessons/[id]/page.tsx` must strictly use existing layout components, "glassmorphism", "Manrope" typography, and no hard-bordered panels.

## Project Structure

### Documentation (this feature)

```text
specs/020-lesson-content-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/api.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.Application/
│   ├── Features/Admin/Commands/CreateLessonResourceCommand.cs
│   ├── Features/Admin/Commands/LinkLessonExamCommand.cs
│   └── Features/Homework/Commands/CreateHomeworkCommand.cs
├── src/NaderGorge.API/
│   └── Controllers/
│       ├── AdminController.cs
│       └── HomeworkController.cs

frontend/
├── src/
│   ├── app/admin/content/lessons/[id]/page.tsx
│   ├── services/
│   │   ├── admin-service.ts
│   │   └── homework-service.ts
│   └── components/admin/
│       ├── LessonVideoList.tsx
│       ├── LessonResourceList.tsx
│       ├── LessonHomeworkList.tsx
│       └── AddLessonResourceForm.tsx
```

**Structure Decision**: The frontend component system expands with new isolated management components that interact via React Query (or similar fetch workflows) to orchestrate data within the new Cockpit view. The backend adds specialized CQRS command records and handlers.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| File Upload Skipping | Quick iterations lack a proper Object Store implementation for dev. | External link URLs used temporarily. Real uploads require major infra provisioning. |
