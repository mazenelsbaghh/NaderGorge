# Implementation Plan: Admin Shared Components Library

**Branch**: `010-admin-shared-components` | **Date**: 2026-03-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/010-admin-shared-components/spec.md`

## Summary

Extract duplicated admin page UI patterns (sidebar, header, footer, tables, pagination, stat cards, modals, search toolbar, tab bar) into a reusable shared component library located in `frontend/src/components/admin/`. The existing `AdminShellChrome` wrapper and `useAdminTheme` hook serve as the foundation. All 5 admin page files (Users, Content, Codes, Questions, Overrides) will be refactored to consume these shared components, reducing per-page boilerplate by 40%+ and ensuring consistent styling across the admin dashboard.

## Technical Context

**Language/Version**: TypeScript 5.x (strict mode)
**Primary Dependencies**: Next.js 15 (App Router), React 19, Framer Motion (animations), Lucide React (icons), Tailwind CSS (styling)
**Storage**: N/A — frontend-only refactoring, no database changes
**Testing**: Playwright E2E tests (existing), visual regression via browser screenshots
**Target Platform**: Web (Desktop-first admin dashboard, responsive)
**Project Type**: Web application — frontend component library extraction
**Performance Goals**: No performance regression; components must render within 16ms (60fps)
**Constraints**: All CSS must use the `--admin-*` CSS custom properties from `useAdminTheme`; no hard-coded color values in shared components
**Scale/Scope**: 6 new/enhanced shared components, 5 admin pages refactored

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Modular Clean Architecture | ✅ PASS | Components → Pages → Services hierarchy preserved. Shared components sit in `components/admin/` layer. |
| II | Provider Abstraction First | ✅ N/A | No external integrations involved. |
| III | Security & Access Control | ✅ N/A | No auth changes. Admin route protection unchanged. |
| IV | Phased Delivery with MVP | ✅ PASS | This is a refactoring within Phase 2.5 scope—no future-phase logic leaking. |
| V | Academic Content Integrity | ✅ N/A | No academic logic changes. |
| VI | UX Simplicity | ✅ PASS | Shared components enforce consistent, familiar navigation. |
| VII | Observability | ✅ N/A | No backend changes. |
| VIII | Premium Editorial Design | ✅ PASS | All components use the "Curated Archive" design tokens (`--admin-*` vars). No borders for layout, glassmorphism for modals, gold/cream palette enforced. |

**Gate Result**: ✅ ALL PASS — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/010-admin-shared-components/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (component interfaces)
├── contracts/           # Phase 1 output (component API contracts)
│   └── component-api.md
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
frontend/src/
├── components/
│   └── admin/
│       ├── AdminShellChrome.tsx    # Enhanced (existing) — layout shell
│       ├── AdminDataTable.tsx      # NEW — generic typed table + pagination
│       ├── AdminStatCard.tsx       # NEW — 3-variant metric card
│       ├── AdminModal.tsx          # NEW — animated modal wrapper
│       ├── AdminSearchToolbar.tsx  # NEW — search input + action buttons
│       ├── AdminTabBar.tsx         # NEW — sub-navigation pill tabs
│       ├── useAdminTheme.ts        # UNCHANGED — theme hook
│       └── index.ts               # NEW — barrel export
├── app/
│   └── admin/
│       ├── users/page.tsx          # REFACTORED — use shared components
│       ├── content/page.tsx        # REFACTORED — use shared components
│       ├── codes/page.tsx          # VERIFIED — already uses AdminShellChrome
│       ├── questions/page.tsx      # REFACTORED — use shared components
│       └── overrides/page.tsx      # REFACTORED — use shared components
```

**Structure Decision**: All shared admin components live under `frontend/src/components/admin/`. This directory already contains `AdminShellChrome.tsx` and `useAdminTheme.ts`, making it the natural home. A barrel `index.ts` file will provide clean imports.

## Complexity Tracking

> No constitution violations — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
