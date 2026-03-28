# Implementation Plan: Nested Content Profiles

**Branch**: `018-nested-content-profiles` | **Date**: 2026-03-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/018-nested-content-profiles/spec.md`

## Summary

The goal is to enable administrators to drill down into the content hierarchy. Specifically, clicking on a Term opens a dedicated Term Profile page where Sections can be added/managed. Clicking on a Section opens a dedicated Section Profile page where Lessons can be added/managed.

## Technical Context

**Language/Version**: TypeScript (Next.js 15), C# (.NET 8)
**Primary Dependencies**: React, Tailwind, MediatR, Entity Framework Core
**Storage**: PostgreSQL
**Project Type**: web-service (Backend) and web-application (Frontend)
**Constraints**: Follow "Editorial Scholar" UI system with `AdminShellChrome`. Must unwrap `params` in Next.js 15.

## Constitution Check

*GATE: Passed. Complies with Phase 3 content hierarchy rules and uses Glass/Gradient/Manrope Admin UI principles without direct database access in the domain.*

## Project Structure

### Documentation (this feature)

```text
specs/018-nested-content-profiles/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # API contracts mapping
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code

```text
backend/
├── src/NaderGorge.Application/
│   ├── Features/Content/Queries/GetTermByIdQuery.cs
│   └── Features/Content/Queries/GetSectionByIdQuery.cs
├── src/NaderGorge.API/
│   └── Controllers/AdminController.cs

frontend/
├── src/services/admin-service.ts
├── src/components/admin/
│   ├── TermListManager.tsx (Modify)
│   ├── SectionListManager.tsx (New)
│   ├── AddSectionForm.tsx (New)
│   ├── LessonListManager.tsx (New)
│   ├── AddLessonForm.tsx (New)
│   └── index.ts (Modify)
└── src/app/admin/content/
    ├── terms/[id]/page.tsx (New)
    └── sections/[id]/page.tsx (New)
```

**Structure Decision**: Web application spanning the `.NET API` and `Next.js` frontend, extending existing modules.

## Complexity Tracking

| Violation | Why Needed | Alternative Rejected Because |
|-----------|------------|--------------------------------|
| None | N/A | Feature strictly adheres to existing patterns. |
