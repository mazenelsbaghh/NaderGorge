# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add inline exam creation to the Lesson Cockpit, enabling admins to create exams with MCQ/Essay questions and attach them flexibly to either whole lessons or specific videos, centralizing the educational content flow.

## Technical Context

**Language/Version**: TypeScript / C# 12 (.NET 8)
**Primary Dependencies**: Next.js + React Query + Tailwind / ASP.NET Core API + EF Core + MediatR
**Storage**: PostgreSQL via EF Core
**Testing**: xUnit + Moq (Backend) / Playwright (Frontend E2E)
**Target Platform**: Web Browsers
**Project Type**: Fullstack Web Application
**Constraint**: Must adhere to Phase 1 Architecture (Modular Clean Architecture) and 'Editorial Scholar' design system.
**Scope**: Inline components in existing Cockpit, plus API endpoints to create Exams, Questions, Options, and linking logic.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modular Clean Architecture**: Handled via MediatR Commands in `NaderGorge.Application`.
- [x] **V. Academic Content Integrity**: Enforces strict link between content (Exams) and the curriculum hierarchy (Lesson or LessonVideo).
- [x] **VIII. Premium Editorial Design System**: Will use existing `AdminShellChrome`, `AdminTab`, glassmorphism styles, and Manrope fonts.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
```text
backend/src/NaderGorge.Domain/
├── Entities/ExamEntities.cs
└── Entities/ContentEntities.cs

backend/src/NaderGorge.Application/Features/Admin/Commands/
└── AdminExamCommands.cs

backend/src/NaderGorge.API/Controllers/
└── AdminController.cs

frontend/src/app/admin/content/lessons/[id]/
└── page.tsx

frontend/src/components/admin/
├── InlineExamEditor.tsx
└── QuestionEditor.tsx
```

**Structure Decision**: The feature naturally extends the existing Next.js / .NET Web API structure. Backend changes are confined to the Admin features in the Application layer, and domain entity expansions. Frontend adds new editor components inside the `admin` folder.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations. The feature strictly aligns with phase expectations (Phase 2 expanding content ops).
