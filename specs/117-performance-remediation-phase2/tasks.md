# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: 117-performance-remediation-phase2

**Input**: Design documents from `/specs/117-performance-remediation-phase2/`
**Prerequisites**: plan.md, spec.md

## Phase 1: Setup

- [x] T001 Verify workspace and confirm all target files exist

---

## Phase 2: User Story 1 — Backend Query Optimization (P1)

- [x] T010 [US1] In GetMistakesQuery.cs, replace exams Include chain with SQL projections
- [x] T011 [US1] In GetMistakesQuery.cs, replace lessons Include chain with projections
- [x] T012 [US1] In GetMistakesQuery.cs, replace homework submissions Include with projections
- [x] T013 [US1] In GetMistakesQuery.cs, update downstream code for projected types
- [x] T014 [US1] In GetLessonDetailQuery.cs, add AsNoTracking() to VideoWatchEvents query
- [x] T015 [US1] In GetLessonDetailQuery.cs, add AsNoTracking() to main lesson query

---

## Phase 3: User Story 2 — Hero Image Optimization (P1)

- [x] T020 [US2] Convert landing-hero.png to WebP (1.4MB → 45KB)
- [x] T021 [US2] Convert landing-hero-dark.png to WebP (1.4MB → 52KB)
- [x] T022 [US2] Update all references in HeroSection.tsx and LandingFooter.tsx

---

## Phase 4: User Story 3 — Worker Source Maps Cleanup (P2)

- [x] T030 [US3] Set declarationMap: false in worker/tsconfig.json
- [x] T031 [US3] Delete all .d.ts.map files from worker/src/

---

## Phase 5: User Story 4 — Nginx Cache Headers (P2)

- [x] T040 [US4] Add _next/static/ immutable cache headers to all 5 frontend server blocks
- [x] T041 [US4] Add image/font 7-day cache headers to all 5 frontend server blocks

---

## Phase 6: User Story 5 — Performance Budget Script (P2)

- [x] T050 [US5] Create scripts/perf-budget.mjs with SVG, hero, force-dynamic, and route checks

---

## Phase 7: Polish & Verification

- [x] T060 Backend build: 0 warnings, 0 errors
- [x] T061 Performance budget: all checks pass
- [x] T062 Docker compose config: valid
- [x] T063 Update docs/performance-deep-audit-2026-06-11.md status table

---

## Phase 8: Quality-Gate Tail Tasks

- [x] T070 Clean-code-guard: all imperatives pass, no findings
- [x] T071 Test-guard: no changed test files
- [x] T072 Final build verification and report generation
