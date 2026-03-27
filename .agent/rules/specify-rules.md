# nader gorge Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-27

## Active Technologies
- C# (.NET 8), TypeScript 5.x (strict mode) + Next.js 14 (App Router), .NET Web API, Entity Framework Core, MediatR, React Query (TanStack Query v5), Zustand, Shadcn/UI, Tailwind CSS, Framer Motion, BullMQ (003-phase1-foundation-mvp)
- PostgreSQL 16, Redis 7 (003-phase1-foundation-mvp)
- TypeScript 5+ (Playwright Scripts), C# (.NET 8 for Backdoor Endpoints) + `@playwright/test` (004-e2e-testing-all)
- Separate `nadergorge_e2e` database instance on PostgreSQL. (004-e2e-testing-all)
- TypeScript / React (Next.js 14+) + Tailwind CSS, next/image (006-papyrus-package-ui)
- N/A (Frontend cosmetic update) (006-papyrus-package-ui)
- C# (.NET 8.0/9.0), TypeScript, Node.js. + Entity Framework Core, React Query, Zustand, BullMQ/ioredis, Tailwind CSS. (007-phase2-academic-ops)
- PostgreSQL, Redis. (007-phase2-academic-ops)
- TypeScript 5, C# .NET 8 + Next.js App Router, Shadcn/UI Component Library, Lucide Icons, ASP.NET Identity Framework. (009-admin-academic-cms)
- Existing PostgreSQL with EF Core. (009-admin-academic-cms)
- TypeScript 5.x (strict mode) + Next.js 15 (App Router), React 19, Framer Motion (animations), Lucide React (icons), Tailwind CSS (styling) (010-admin-shared-components)
- N/A — frontend-only refactoring, no database changes (010-admin-shared-components)
- TypeScript (Next.js 15+ Frontend) / C# (.NET 8 Backend) + React Hook Form, Zod, Axios, MediatR, Entity Framework Core (012-student-auth-redesign)
- PostgreSQL (existing user & profile tables) (012-student-auth-redesign)
- TypeScript (Next.js 15) + YouTube IFrame API, `shadow-dom`, `MutationObserver` (013-video-url-protection)
- N/A (no backend changes) (013-video-url-protection)
- C# .NET 8 (backend), TypeScript 5.x / Next.js 14 (frontend) + Entity Framework Core, React Query, Framer Motion, Zustand (014-registration-codes-hierarchy)
- PostgreSQL (Supabase), Redis (014-registration-codes-hierarchy)

- Markdown (documentation-only phase — no application code) + N/A (no code dependencies) (001-phase0-discovery-blueprint)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for Markdown (documentation-only phase — no application code)

## Code Style

Markdown (documentation-only phase — no application code): Follow standard conventions

## Recent Changes
- 014-registration-codes-hierarchy: Added C# .NET 8 (backend), TypeScript 5.x / Next.js 14 (frontend) + Entity Framework Core, React Query, Framer Motion, Zustand
- 013-video-url-protection: Added TypeScript (Next.js 15) + YouTube IFrame API, `shadow-dom`, `MutationObserver`
- 012-student-auth-redesign: Added TypeScript (Next.js 15+ Frontend) / C# (.NET 8 Backend) + React Hook Form, Zod, Axios, MediatR, Entity Framework Core


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
