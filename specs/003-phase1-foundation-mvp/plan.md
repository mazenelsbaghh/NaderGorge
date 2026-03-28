# Implementation Plan: Phase 1 — Foundation and MVP Launch

**Branch**: `003-phase1-foundation-mvp` | **Date**: 2026-03-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-phase1-foundation-mvp/spec.md`

## Summary

Phase 1 delivers the first production-ready MVP of the Nader George Educational Platform. It encompasses: phone-based authentication with strict device limits, a two-step student registration flow, a hierarchical content management system (Program → Package → Content Section → Lesson), an access-code engine with bulk generation and redemption, a basic MCQ exam system with Teacher-controlled hard gating, video streaming via a provider abstraction (YouTube initially) with hard-lock watch limits and admin override, a student dashboard, and an admin panel for CRUD operations across all entities. The backend follows .NET Clean Architecture with CQRS (MediatR), the frontend uses Next.js with TypeScript, and the system relies on PostgreSQL, Redis, and a BullMQ Node.js worker.

## Technical Context

**Language/Version**: C# (.NET 8), TypeScript 5.x (strict mode)
**Primary Dependencies**: Next.js 14 (App Router), .NET Web API, Entity Framework Core, MediatR, React Query (TanStack Query v5), Zustand, Shadcn/UI, Tailwind CSS, Framer Motion, BullMQ
**Storage**: PostgreSQL 16, Redis 7
**Testing**: xUnit (.NET), Jest + React Testing Library (Frontend), Playwright (E2E)
**Target Platform**: Web (Server-rendered + Client hydration), Desktop & Mobile browsers
**Project Type**: Full-stack web application (SPA + REST API)
**Performance Goals**: API < 500ms p95, Video page < 3s TTI, Code redemption < 2s, 1,000 concurrent video watchers
**Constraints**: JWT auth, strict device limits per account, hard exam gating, hard video watch limits with admin override
**Scale/Scope**: ~50 screens, 15+ database tables, 4 roles, 13 modules

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Modular Clean Architecture | ✅ PASS | Backend structured as API → Application → Domain → Infrastructure. Frontend follows Pages → Components → Services → Hooks. Each module (Auth, Users, Content, Exams, Codes, Tracking, Audit) is self-contained. |
| II. Provider Abstraction First | ✅ PASS | `IVideoProvider` for YouTube, `INotificationProvider` for SMS/WhatsApp (mocked in Phase 1), all behind interfaces. Code redemption separated from code type logic. |
| III. Security & Access Control | ✅ PASS | JWT + refresh tokens, RBAC on every API endpoint, audit logging for all state changes, device tracking with strict limits, rate limiting on auth and code routes. |
| IV. Phased Delivery with MVP Discipline | ✅ PASS | Phase 1 scope is bounded: no Homework, no Parent portal, no AI, no Gamification. Schema fields for future features are allowed but logic is NOT implemented. |
| V. Academic Content Integrity | ✅ PASS | Content hierarchy preserved (Program > Package > Section > Lesson). MCQ question-bank with basic classification. Exam gating controlled by Teachers. |
| VI. Two-Step Registration & UX Simplicity | ✅ PASS | Two-step flow explicitly documented. Dashboard surfaces resume-study, packages, exams, progress, codes. 3-click rule respected. |
| VII. Observability & Operational Readiness | ✅ PASS | Structured logging with correlation IDs, health check endpoints, environment separation, EF Core migrations, consistent error response format. |

**Gate Result: ALL PASS** — No violations detected.

## Project Structure

### Documentation (this feature)

```text
specs/003-phase1-foundation-mvp/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (API contracts)
├── checklists/          # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/              # Entities, Value Objects, Interfaces, Enums
│   ├── NaderGorge.Application/         # CQRS Handlers, DTOs, Validators, Services
│   ├── NaderGorge.Infrastructure/      # EF Core, Redis, Providers, Repositories
│   └── NaderGorge.API/                 # Controllers, Middleware, Filters, Startup
├── tests/
│   ├── NaderGorge.UnitTests/
│   ├── NaderGorge.IntegrationTests/
│   └── NaderGorge.ContractTests/
└── NaderGorge.sln

frontend/
├── src/
│   ├── app/                            # Next.js App Router pages & layouts
│   │   ├── (public)/                   # Public site (landing, about, faq, auth)
│   │   ├── student/                    # Student portal (dashboard, packages, lessons, exams)
│   │   ├── admin/                      # Admin panel (users, content, codes, questions)
│   │   └── teacher/                    # Teacher panel (content builder, question bank)
│   ├── components/                     # Reusable UI components (Shadcn-based)
│   │   ├── ui/                         # Base Shadcn primitives
│   │   ├── forms/                      # Registration, login, code activation forms
│   │   ├── content/                    # Video player, lesson viewer, exam renderer
│   │   └── layout/                     # Navigation, sidebar, breadcrumbs, footer
│   ├── services/                       # API client layer (axios/fetch wrappers)
│   ├── hooks/                          # Custom React hooks
│   ├── stores/                         # Zustand stores
│   ├── lib/                            # Utilities, constants, type definitions
│   └── styles/                         # Global Tailwind config, CSS overrides
├── public/                             # Static assets
└── tests/
    ├── components/                     # Component tests (Jest + RTL)
    └── e2e/                            # Playwright E2E tests

worker/
├── src/
│   ├── jobs/                           # Job processors (code-generation, notifications)
│   ├── queues/                         # Queue definitions
│   └── index.ts                        # BullMQ worker entry point
├── package.json
└── tsconfig.json

docker/
├── docker-compose.yml                  # Dev environment: PostgreSQL, Redis
├── docker-compose.staging.yml
└── Dockerfiles/
```

**Structure Decision**: Web Application structure selected (frontend + backend + worker). This aligns with the constitution's mandatory stack (Next.js frontend, .NET backend, BullMQ worker) and supports independent deployment of each service.

## Complexity Tracking

> No violations detected. This section is intentionally empty.
