# Implementation Plan: Phase 2.5 Admin CMS

**Branch**: `009-admin-academic-cms` | **Date**: 2026-03-26 | **Spec**: [specs/009-admin-academic-cms/spec.md](specs/009-admin-academic-cms/spec.md)
**Input**: Feature specification from `/specs/009-admin-academic-cms/spec.md`

## Summary

This subphase serves as a UI and basic API completion of the Phase 2 operations. It exposes existing capabilities (Identity Roles, Lesson Homework links, Parent Report routing) explicitly via the Next.js Admin dashboard, allowing Nader George's team to construct academic experiences purely visually without backend scripts.

## Technical Context

**Language/Version**: TypeScript 5, C# .NET 8
**Primary Dependencies**: Next.js App Router, Shadcn/UI Component Library, Lucide Icons, ASP.NET Identity Framework.
**Storage**: Existing PostgreSQL with EF Core.
**Testing**: Playwright for frontend verification (Extending `admin-content.spec.ts` if needed).
**Target Platform**: Web Browsers (Chrome / Safari primarily).
**Project Type**: Full Stack Platform.
**Performance Goals**: N/A, typical web administration operations.
**Constraints**: Needs stable role assignment. Admin pages are inherently protected.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
- **I. Modular Clean Architecture**: Checked. Will append UI updates naturally within the `AdminController` boundary and content folders.
- **III. Security & Access Control**: Admin modifications to users strictly bounded by `Role("Admin")`.
- **IV. Phased Delivery**: Represents a clear increment connecting features laid out previously.
- **VI. UX Simplicity**: Copying the parental view ensures a non-technical staff member can act gracefully.

## Project Structure

### Documentation (this feature)

```text
specs/009-admin-academic-cms/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.API/
│   ├── Controllers/AdminController.cs
│   └── Controllers/ContentController.cs
└── src/NaderGorge.Application/
    ├── UseCases/Admin/UpdateUserRoleCommand.cs
    └── UseCases/Admin/Content/AttachHomeworkCommand.cs

frontend/
├── src/app/admin/users/page.tsx
├── src/app/admin/content/packages/[packageId]/sections/[sectionId]/lessons/[lessonId]/page.tsx
└── src/components/admin/
    ├── UserRoleDropdown.tsx
    ├── CopyParentLinkButton.tsx
    └── HomeworkTabEditor.tsx
```

**Structure Decision**: The Next.js project is already structured perfectly. New logic falls squarely into existing nested layouts or components.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A       | Clean UI additions | Using Seeder only isn't sustainable for Nader's team. |
