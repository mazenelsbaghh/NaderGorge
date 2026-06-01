# Implementation Plan: Lesson Comments Moderation

**Branch**: `057-comments-moderation` | **Date**: 2026-04-08 | **Spec**: [spec.md](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/057-comments-moderation/spec.md)
**Input**: Feature specification from `/specs/057-comments-moderation/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add a moderated lesson-comments flow where students can post comments directly beneath the lesson video area, while teachers review pending comments from the existing lesson management experience before anything becomes publicly visible. The design extends the current lesson detail and lesson cockpit flows with a dedicated lesson-comment aggregate, student-facing read/create endpoints, and teacher moderation endpoints scoped to the lesson.

## Technical Context

**Language/Version**: C# 13 / .NET 9, TypeScript 5.x / Next.js 16.2.1 / React 19  
**Primary Dependencies**: MediatR, Entity Framework Core 9, Next.js App Router, React Query-compatible service layer, Tailwind CSS, existing shared admin components  
**Storage**: PostgreSQL (new lesson comments table plus moderation metadata)  
**Testing**: xUnit backend tests, API contract tests, frontend integration or Playwright flow coverage for student submission and teacher moderation  
**Target Platform**: Web application for student and admin/teacher dashboards  
**Project Type**: Full-stack web application  
**Performance Goals**: Comment list and moderation actions should complete within the platform's standard CRUD expectations (<500ms p95 for normal operations)  
**Constraints**: Pending comments must never leak into the public lesson page; authorization must respect student access rules and teacher/admin moderation rights; UI additions must reuse existing admin shared components and existing lesson page composition  
**Scale/Scope**: Per-lesson discussion threads for active courses, expected to handle hundreds of comments per lesson and thousands across an academic term without changing the core lesson/video flow

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. Feature can be split cleanly across Domain entity, Application commands/queries, API endpoints, frontend services, and lesson/admin UI surfaces.
- **III. Security & Access Control by Default**: Pass with explicit guardrails. Student submission/read endpoints must enforce authenticated lesson access; moderation endpoints must require `Admin` or `Teacher`; moderation actions should emit audit logs because they are state-changing.
- **V. Academic Content Integrity**: Pass. Comments are bounded to teacher-owned lesson context and do not introduce open-ended AI or off-platform discussion surfaces.
- **VI. Single-Flow Registration & UX Simplicity**: Pass. Student interaction remains inside the existing lesson page; no new navigation branch is required.
- **VIII. Premium, Journal-Led UI Cohesion**: Pass. Student comments slot into the current lesson viewer beneath video content; admin moderation should use shared admin shell and table/stat patterns instead of bespoke dashboard markup.
- **Development Workflow & Quality Gates**: Pass. Feature requires API contracts, tests, and likely a migration; all are captured in planned artifacts.

**Post-Design Re-check**: Pass. The proposed design stays within existing lesson and admin content surfaces, introduces no constitution violations, and keeps permissions and moderation decisions explicit.

## Project Structure

### Documentation (this feature)

```text
specs/057-comments-moderation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── api.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   └── Entities/
│   ├── NaderGorge.Application/
│   │   ├── Features/Content/
│   │   │   └── Queries/
│   │   ├── Features/Admin/
│   │   │   ├── Commands/
│   │   │   └── Queries/
│   │   └── Common/
│   ├── NaderGorge.Infrastructure/
│   │   └── Data/
│   └── NaderGorge.API/
│       └── Controllers/
└── tests/

frontend/
├── src/
│   ├── app/
│   │   ├── student/lessons/[lessonId]/
│   │   └── admin/content/lessons/[id]/
│   ├── components/
│   │   ├── content/
│   │   └── admin/
│   └── services/
└── tests/
```

**Structure Decision**: Keep the feature inside the existing full-stack lesson architecture. Student-facing comment rendering belongs in the lesson viewer path, while moderation belongs in the existing lesson cockpit path under admin content, with backend application logic split into student content queries/commands and admin moderation queries/commands.

## Complexity Tracking

No constitution exceptions are currently required.
