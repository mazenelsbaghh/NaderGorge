# Implementation Plan: 116-performance-deep-remediation

**Branch**: `116-performance-deep-remediation` | **Date**: 2026-06-11 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/116-performance-deep-remediation/spec.md)
**Input**: Feature specification from `/specs/116-performance-deep-remediation/spec.md`

## Summary

This plan outlines the optimization of Next.js rendering, API waterfalls, backend EF queries, assets, response compression, and local caching to resolve platform-wide latency issues.

## Technical Context

**Language/Version**: C# (.NET 9 / ASP.NET Core), TypeScript (Next.js 16.2.1 / React 19)  
**Primary Dependencies**: EF Core 9.0, MediatR, tailwindcss, framer-motion, lucide-react  
**Storage**: PostgreSQL 16, Redis 7  
**Testing**: dotnet test, Playwright, Python pytest  
**Target Platform**: Docker-compose, Nginx proxy  
**Project Type**: web-service & frontend app  
**Performance Goals**: TTFB <250ms local, API query times <250ms for code groups and student dashboard  
**Constraints**: Keep all features secure; do not compromise video protection or access controls.  
**Scale/Scope**: 113+ spec modules, 16 backend modules.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Backend API/Application, Frontend Pages/Components/Services, Docker & Nginx config.
- **Automated tests**: Verify backend API build and tests, frontend production build, and check for static routing compile.
- **Manual QA**: Student dashboard, packages view, and admin code-groups list.
- **Docker Gates**: Check compose configuration using `docker compose config -q`.

## Project Structure

### Documentation (this feature)

```text
specs/116-performance-deep-remediation/
├── plan.md              # This file
├── research.md          # Research decisions
├── data-model.md        # DB and query design details
├── quickstart.md        # Verifications and setup
└── tasks.md             # Task checklist
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   ├── NaderGorge.Application/
│   ├── NaderGorge.Domain/
│   └── NaderGorge.Infrastructure/
└── tests/

frontend/
├── src/
│   ├── app/
│   ├── components/
│   ├── services/
│   └── lib/
└── public/
```

**Structure Decision**: Standard web application directory structure with separate backend and frontend projects.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Backend: `dotnet test backend/NaderGorge.sln`
- Frontend: `npm run build` (Next.js compilation, checks static routes)

**Docker Gate Required**:
- `docker compose config -q`

**Manual QA Required**:
- Verify Student dashboard loads in a single bootstrap API call.
- Verify Code Groups screen loads quickly and displays accurate count projections.
- Verify compressed SVG logos load instantly and Nginx sends correct compression headers.

**End-of-Phase Report Format**:
- Summary of resolved performance findings.
- List of modified files.
- Build & test verification outputs.
- Before/after bundle & image size stats.
