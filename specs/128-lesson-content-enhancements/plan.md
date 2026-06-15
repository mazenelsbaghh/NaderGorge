# Implementation Plan: 128 — Lesson Content Enhancements

**Branch**: `128-lesson-content-enhancements` | **Date**: 2026-06-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/128-lesson-content-enhancements/spec.md`

## Summary

5 enhancements to the admin lesson content management page: (A) Replace `window.prompt` video editing with inline form, (B) Add `IsActive` toggle for video student-visibility, (C) Add file upload alongside URL for resources, (D) Create detailed exam profile page with per-question stats, (E) Add auto-save during exam creation.

## Technical Context

**Language/Version**: C# 13 (.NET 9.0), TypeScript 5.x (Next.js 16.2.1 / React 19)  
**Primary Dependencies**: MediatR 12.4.1, EF Core 9.0, Axios 1.13.6, Tailwind CSS 4, lucide-react  
**Storage**: PostgreSQL 16 (EF Core migration for `IsActive` column), local file system (`wwwroot/uploads/resources/`)  
**Testing**: `dotnet build`, `npm run build`, Playwright E2E  
**Target Platform**: Web (admin dashboard)  
**Project Type**: Full-stack web application (API + SPA)  
**Constraints**: Follows existing admin design system tokens (`--admin-*` CSS variables), RTL-first Arabic UI  
**Scale/Scope**: 5 sub-features across 10-15 files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**:
  - ✅ Backend: 1 new entity field (`IsActive`), 1 new command, 1 new endpoint, 1 modified query, 1 upload endpoint
  - ✅ Frontend: 4 modified components, 1 new page, 2 new service methods
  - ✅ Worker: No changes
  - ✅ Database: 1 migration (`AddIsActiveToLessonVideo`)
  - ✅ Docker: No changes (static files via wwwroot already served)
- **Automated tests**: `dotnet build` + `npm run build` (build verification), existing Playwright suite
- **Manual QA**:
  - Admin: Edit video, toggle visibility, upload resource, view exam profile, auto-save exam
  - Student: Verify inactive videos are hidden
- **Docker gate**: `docker compose config -q`, `make up`, `make migrate` (for IsActive migration)

## Project Structure

### Documentation (this feature)

```text
specs/128-lesson-content-enhancements/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── api-contracts.md
└── tasks.md             # Phase 2 output (via /speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/Entities/ContentEntities.cs        # MODIFY: IsActive field
│   ├── NaderGorge.Application/Features/Admin/Commands/
│   │   └── ToggleVideoActiveCommand.cs                      # NEW
│   ├── NaderGorge.Application/Features/Admin/Queries/
│   │   └── GetExamDashboardQuery.cs                         # MODIFY: per-question stats
│   └── NaderGorge.API/Controllers/AdminController.cs        # MODIFY: 2 new endpoints
└── tests/

frontend/
├── src/
│   ├── components/admin/
│   │   ├── LessonVideoList.tsx                              # MODIFY: inline edit + toggle
│   │   ├── AddResourceForm.tsx                              # MODIFY: file upload tab
│   │   └── AttachedExamViewer.tsx                            # MODIFY: profile link button
│   ├── app/admin/content/exams/[id]/
│   │   ├── page.tsx                                         # NEW: exam profile page
│   │   └── ExamProfilePageClient.tsx                        # NEW: exam profile client
│   └── services/admin-service.ts                            # MODIFY: 2 new methods
└── tests/
```

**Structure Decision**: Standard web application layout with backend/frontend split, consistent with existing project structure.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `dotnet build` — backend compiles with 0 errors
- `npm run build` — frontend compiles with 0 errors  
- Existing Playwright E2E suite — no regressions

**Docker Gate Required**:
- `docker compose config -q` — valid compose
- `make up` — all services start
- `make migrate` — IsActive migration applies cleanly

**Manual QA Required**:
| Role | URL/Surface | Action | Expected |
|------|------------|--------|----------|
| Admin | Lesson → Videos tab | Click Edit on video | Inline form with pre-filled data |
| Admin | Lesson → Videos tab | Click Eye icon | Video dims, toggles active state |
| Student | Lesson page | View lesson content | Inactive videos hidden |
| Admin | Lesson → Resources tab | Upload PDF file | File uploaded, appears in list |
| Admin | Lesson → Exam tab | Click "عرض البروفايل" | Exam profile page loads with stats |
| Admin | Exam profile page | Edit question | Question updated |
| Admin | Create exam | Add questions | Auto-saves after each question |

**End-of-Phase Report Format**: implemented scope, commands run, test results, Docker result, manual QA checklist, risks, go/no-go for next phase.
