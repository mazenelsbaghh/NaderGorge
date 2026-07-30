# Implementation Plan: Frontend Design System Unification

**Branch**: `162-unify-frontend-design-system` | **Date**: 2026-07-14 | **Spec**: [spec.md](spec.md)

## Summary

Frontend-only migration from scattered raw palettes and duplicated controls to semantic light/dark tokens and shared primitives across every production UI route. Existing copy, route order, permissions, data requests, realtime behavior, and workflows remain unchanged.

Planning inputs: `research.md`, `data-model.md`, `quickstart.md`, and `contracts/`.

## Technical Context

**Language/Version**: TypeScript strict, Next.js 16.2.7, React 19.2.4
**Primary Dependencies**: Tailwind CSS 4, CVA, clsx/tailwind-merge, existing Base UI/React components
**Storage**: N/A; preserve existing localStorage theme keys
**Testing**: ESLint, TypeScript, Next build, accessibility checks, live-support/platform contracts, Playwright
**Target Platform**: RTL responsive web application
**Project Type**: Frontend web application
**Performance Goals**: no added route-load network work; preserve existing realtime/render behavior
**Constraints**: WCAG 2.2 AA, raw colors only via exact allowlist, no backend/API/DB/worker changes
**Scale/Scope**: every production UI route, dynamic route, permission state, and shared UI component across six role surfaces

## Constitution Check

- Frontend only is affected; backend, worker, database, API contracts, and migrations are explicitly unchanged.
- Foundation tests: token governance, primitive accessibility, route inventory, existing live-support/platform contracts.
- Manual QA: public, student, teacher, assistant, admin, and live-support representative flows in both themes and narrow/wide viewports, including denied/disabled states.
- Docker gate: `docker compose config -q && make up && make ps`; `make migrate` is N/A because no schema changes.
- No phase advances with a failed automated gate unless the failure is fixed or documented as an owner-approved environment blocker.

## Structure Decision

Use the existing `frontend/src/app` route tree, `frontend/src/components` primitives and role modules, `frontend/src/app/globals.css` token registry, and `frontend/scripts` governance checks. Add typed contracts under `frontend/src/lib/design-tokens.ts`, allowlist under `frontend/config/design-color-allowlist.json`, and inventory under `docs/design-system-route-inventory.md`.

## Phase Closure & Verification Plan

Automated: `npm run lint`, `npm run typecheck`, `npm run build`, `npm run check:accessibility`, `npm run check:live-support-contracts`, `npm run check:platform-events`, query/reload checks, token scanner, and targeted Playwright role/theme flows.

Manual: verify existing copy, order, permissions, loading/empty/error/disabled behavior and keyboard focus for each role surface in light/dark and mobile/desktop.

Docker: `docker compose config -q`, `make up`, `make ps`, health/surface checks. Record unavailable browser/backend/secrets separately.

## Implementation Waves

## Phase 0: Outline & Research

Research decisions are recorded in `research.md`; no backend or persistent data changes are permitted.

## Phase 1: Design & Contracts

Typed token, primitive, route inventory, and raw-color governance contracts are recorded in `data-model.md` and `contracts/`.

1. Inventory and semantic token contract.
2. Shared primitives and dialog/status consolidation.
3. Public and student surfaces.
4. Teacher, assistant, and admin surfaces.
5. Live-support surfaces and final route evidence.
