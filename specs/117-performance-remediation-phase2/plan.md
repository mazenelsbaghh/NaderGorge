# Technical Plan: 117-performance-remediation-phase2

**Feature**: Performance Remediation Phase 2  
**Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/117-performance-remediation-phase2/spec.md)  
**Date**: 2026-06-11

## Technical Context

- **Backend**: C# 13 / .NET 9, Entity Framework Core 9, MediatR
- **Frontend**: TypeScript 5.x / Next.js 16.2.1 / React 19
- **Worker**: Node.js / TypeScript
- **Infra**: Docker, Nginx, PostgreSQL

## Changes Overview

### 1. Backend Query Optimization

#### GetMistakesQuery.cs
- Replace `Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question).ThenInclude(q => q.Options)` with SQL projection `.Select()` directly to DTOs.
- Replace `Include(s => s.Homework)` on homework submissions with projection.
- All data assembly via in-memory dictionaries after projected fetches.

#### GetLessonDetailQuery.cs
- Add `AsNoTracking()` to the VideoWatchEvents query (line 163).
- Add `AsNoTracking()` to lesson main query.

### 2. Hero Image Optimization
- Convert `landing-hero.png` and `landing-hero-dark.png` to WebP using `cwebp` or `sharp`.
- Update references in any component that uses them.
- Target: under 500KB each.

### 3. Worker Source Maps
- Set `declarationMap: false` in `worker/tsconfig.json` (or add `declarationDir: "./dist"`).
- Delete existing `.d.ts.map` files from `worker/src/`.

### 4. Nginx Cache Headers
- Add `location /_next/static/` block with immutable cache headers to all frontend server blocks.
- Add image cache headers for public assets.

### 5. Performance Budget Script
- Create `scripts/perf-budget.mjs` that checks:
  - SVG sizes in `frontend/public/images/`
  - Hero image sizes
  - `force-dynamic` in root layout
  - Static vs dynamic route count from build output

## Files to Modify

| File | Action |
|------|--------|
| `backend/.../GetMistakesQuery.cs` | Refactor to projections |
| `backend/.../GetLessonDetailQuery.cs` | Add AsNoTracking |
| `frontend/public/images/landing-hero.png` | Convert to WebP |
| `frontend/public/images/landing-hero-dark.png` | Convert to WebP |
| `worker/tsconfig.json` | Disable declarationMap |
| `docker/nginx/massar.conf` | Add cache headers |
| `scripts/perf-budget.mjs` | NEW - performance budget |

## Verification Plan

```bash
dotnet build backend/NaderGorge.sln
cd frontend && npm run build
node scripts/perf-budget.mjs
docker compose config -q
```
