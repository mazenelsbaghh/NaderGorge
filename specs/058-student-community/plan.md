# Implementation Plan: Student Community

**Branch**: `058-student-community` | **Date**: 2026-04-08 | **Spec**: [spec.md](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/058-student-community/spec.md)
**Input**: Feature specification from `/specs/058-student-community/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add a moderated student community feed where students submit posts for admin approval, then other students can like and comment on approved posts. The implementation should follow the same full-stack patterns already used for moderated lesson comments: explicit moderation states in the backend, dedicated student/admin API contracts, PostgreSQL persistence, and new student/admin UI surfaces that remain consistent with the platform's current design system and access-control rules.

## Technical Context

**Language/Version**: C# 13 / .NET 9, TypeScript 5.x / Next.js 16.2.1 / React 19  
**Primary Dependencies**: MediatR, Entity Framework Core 9, Next.js App Router, React Query-compatible service layer, Tailwind CSS, existing shared admin components  
**Storage**: PostgreSQL (new community posts, post comments, and post likes tables plus moderation metadata)  
**Testing**: xUnit backend tests, API contract tests, frontend integration or Playwright coverage for post submission, moderation, like, and comment flows  
**Target Platform**: Web application for student and admin dashboards  
**Project Type**: Full-stack web application  
**Performance Goals**: Community feed reads and engagement actions should stay within the platform CRUD expectation of <500ms p95 for normal operations  
**Constraints**: Pending or rejected posts must never leak into the public feed; likes/comments must attach only to approved posts; admin moderation must follow role-based access and audit logging rules; new UI should reuse current student layout patterns and shared admin shell components  
**Scale/Scope**: Initial release covers one shared student community feed, moderation queue, flat post comments, and unique likes, expected to support thousands of posts and engagement records per term without introducing groups, attachments, or threaded replies

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. The feature can be implemented as a dedicated backend/community slice with clear domain entities, application handlers, API controllers, services, and isolated frontend components/pages.
- **III. Security & Access Control by Default**: Pass with explicit guardrails. Student endpoints require authenticated student access; moderation endpoints require `Admin` or whichever existing elevated role owns community review; all state-changing actions must validate input and create audit logs where moderation status changes.
- **IV. Phased Delivery with MVP Discipline**: Pass. The scope is intentionally limited to post submission, approval/rejection, likes, and flat comments. Notifications, editing, deletion, attachments, and private groups remain out of scope.
- **VI. Single-Flow Registration & UX Simplicity**: Pass. Community is an additive student surface, not a new registration or profile branch, and the experience remains simple: submit, await review, then engage after approval.
- **VIII. Premium Editorial Design System**: Pass. Student feed UI should align with current student page composition, and admin moderation must reuse shared admin chrome/components instead of bespoke tables.
- **Development Workflow & Quality Gates**: Pass. The feature requires new persistence, contract documentation, and test coverage, all captured by the plan artifacts.

**Post-Design Re-check**: Pass. The proposed design keeps moderation explicit, preserves clean module boundaries, and avoids scope creep or UI fragmentation.

## Project Structure

### Documentation (this feature)

```text
specs/058-student-community/
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
│   │   ├── Entities/
│   │   └── Enums/
│   ├── NaderGorge.Application/
│   │   ├── Features/Community/
│   │   │   ├── Commands/
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
│   │   ├── student/community/
│   │   └── admin/community/
│   ├── components/
│   │   ├── student/
│   │   ├── admin/
│   │   └── ui/
│   └── services/
└── tests/
```

**Structure Decision**: Keep Community as a dedicated full-stack feature rather than bolting it onto lesson pages. Student feed and interactions live under a new student route, admin moderation lives under a focused admin route or tab, and backend behavior is split between public student community operations and privileged moderation handlers.

## Complexity Tracking

No constitution exceptions are currently required.
