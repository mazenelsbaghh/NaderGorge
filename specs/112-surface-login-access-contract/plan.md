# Implementation Plan: Surface Login and Access Contract

**Branch**: `112-surface-login-access-contract` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/112-surface-login-access-contract/spec.md`

## Summary
Improve login customization per subdomain (Student Gateway, Teacher Gateway, Assistant/Staff Gateway, Admin/Supervisor Gateway) and enforce strict access boundaries across subdomains. Accessing unauthorized surface paths on a subdomain must render a branded 404 error page ("الصفحة غير موجودة أو لا تخص هذا الحساب") instead of silently redirecting to another surface. Furthermore, return URLs must be strictly validated to prevent open redirects or cross-role leaks.

---

## Technical Context

**Language/Version**: TypeScript 5.x / Next.js 16.2.1 / React 19  
**Primary Dependencies**: Next.js App Router, Axios, Zustand  
**Storage**: N/A (Stateless cookie/Zustand validation)  
**Testing**: Playwright (`npm run test:e2e`), `scripts/verify-surface-separation.mjs`  
**Target Platform**: Docker-compose standalone frontend  
**Project Type**: Next.js Web Application  
**Performance Goals**: Instant 404 rendering, standard login speeds  
**Constraints**: Zero cross-subdomain redirect leakage  
**Scale/Scope**: All 5 frontend surfaces  

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Frontend configuration, pages, components, middleware/proxy routing, and verification scripts. No backend/database schema changes.
- **Automated tests**: Existing E2E Playwright tests and the custom surface verification script (`scripts/verify-surface-separation.mjs`).
- **Manual QA**: Verification of portal-specific login titles, student redirection validation on login, and 404 appearance when visiting invalid routes (e.g. `/admin` on student portal).
- **Docker gates**: Run `docker compose config -q` and verify compose stability.
- **No Hidden Failure**: Resolve all lint, build, and E2E issues inside this phase.

---

## Project Structure

### Documentation (this feature)

```text
specs/112-surface-login-access-contract/
├── plan.md              # This file
├── research.md          # Research findings
├── data-model.md        # DB impacts (none)
├── quickstart.md        # Developer setup guide
└── tasks.md             # Task breakdown
```

### Source Code (repository root)

```text
frontend/src/
├── app/
│   ├── (public)/login/page.tsx   # Login page
│   ├── not-found.tsx             # Custom branded 404
│   └── middleware.ts             # MANDATORY: Calls proxy.ts
├── components/forms/LoginForm.tsx # Login submission logic
├── packages/surface-runtime/
│   └── config.ts                 # Route decisions
└── proxy.ts                      # Subdomain/surface routing middleware
scripts/
└── verify-surface-separation.mjs # Surface boundaries assertion
```

**Structure Decision**: Web application structure (`frontend/src/app` App Router, `frontend/src/packages/surface-runtime`).

---

## Proposed Changes

### Configuration & Routing

#### [MODIFY] [config.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/packages/surface-runtime/config.ts)
- Update `getSurfaceName` to detect surface name based on port numbers (8738-8742) in local development on localhost/127.0.0.1.
- In `getRouteBoundaryDecision`, change decisions for cross-surface path requests in non-landing surfaces (student, admin, teacher, assistant) from `action: 'redirect'` to `action: 'rewrite', destination: '/not-found'`. Only the `landing` surface should redirect users across portals.

#### [MODIFY] [proxy.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/proxy.ts)
- Update proxy routing logic to align with `getRouteBoundaryDecision` rewrites to `/not-found` / `/_not-found` for unauthorized subdomains, rather than redirecting them.

#### [NEW] [middleware.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/middleware.ts)
- Create `middleware.ts` to call the `proxy` function from `proxy.ts`. Next.js requires this file to exist in order to run middleware.

### Login Flow

#### [MODIFY] [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/(public)/login/page.tsx)
- Check current surface using `getSurfaceName()`. Customize title and subtitles to display the specific gateway ("بوابة الطالب", "بوابة المعلم", "بوابة المساعدين والموظفين", "بوابة الإدارة").
- Update session check: if logged in but role does not match the active surface, validate `returnUrl` or redirect to the correct role dashboard.
- If `returnUrl` is present, validate it: must start with the active surface prefix (e.g. `/student` for student). If invalid, fallback to the default dashboard.

#### [MODIFY] [LoginForm.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/forms/LoginForm.tsx)
- On successful login, validate the `returnUrl` parameter: it must be a relative path starting with the allowed path prefix for the current surface. If invalid/unauthorized, fallback to the role-specific default dashboard.

### Error Boundaries

#### [NEW] [not-found.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/not-found.tsx)
- Create a branded `not-found.tsx` component that checks the active surface and displays "الصفحة غير موجودة أو لا تخص هذا الحساب" ("The page does not exist or does not belong to this account") with styling and colors aligned to the active surface.

### Verification Tools

#### [MODIFY] [verify-surface-separation.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/verify-surface-separation.mjs)
- Update cross-surface assertions: accessing `/admin` on `student` surface, `/student` on `admin` surface, etc., must return HTTP 404 status code (or render the 404 page) instead of performing a 301/302 redirect.

---

## Verification Plan

### Automated Tests
- Run Next.js lint check: `npm run lint` inside `frontend`.
- Run Next.js build: `npm run build` inside `frontend`.
- Run E2E verification tool: `node scripts/verify-surface-separation.mjs --static-only`.

### Manual Verification
- Access `http://localhost:8739/login` and verify "بوابة الطالب" header.
- Access `http://localhost:8739/admin` and verify branded 404/not-found screen.
- Log in and verify returnUrl validation behavior by passing an invalid `returnUrl` parameter.
