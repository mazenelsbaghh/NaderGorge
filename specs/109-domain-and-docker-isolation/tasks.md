# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in [spec.md](./spec.md)
- [x] Phase 2: Technical Planning (`speckit-plan`) completed in [plan.md](./plan.md)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed in [tasks.md](./tasks.md)

---

# Tasks: Domain and Docker Isolation Finalization

**Input**: Design documents from `/specs/109-domain-and-docker-isolation/`
**Prerequisites**: plan.md, spec.md

**Tests**: E2E domain surface isolation checks and script verification.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Update global configuration fallbacks and template definitions.

- [x] T001 [P] [US1] In `frontend/src/packages/surface-runtime/config.ts`, update the default fallback of `mainDomain` from `'massarplatform.com'` to `'massar-academy.net'`.
- [x] T002 [P] [US1] In `.env.example`, change `NEXT_PUBLIC_APP_DOMAIN=massarplatform.com` to `NEXT_PUBLIC_APP_DOMAIN=massar-academy.net`.
- [x] T003 [P] [US1] In `docker-compose.yml`, update the default `Cors__AllowedOrigins` list for the `backend` service, removing all references to legacy domains (`massarplatform.com` and `bsma-academy.com` subdomains) and retaining only `massar-academy.net` subdomains and localhost origins.
- [x] T004 [P] [US1] In `docker-compose.yml`, update `NEXT_PUBLIC_APP_DOMAIN` default under `x-frontend-environment` to `massar-academy.net`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add DOM markers and HTTP response indicators.

- [x] T005 [US3] In `frontend/src/app/layout.tsx`, import `getSurfaceName` from `@/packages/surface-runtime/config` and add the `data-massar-surface={getSurfaceName()}` attribute on the `<html>` element.

---

## Phase 3: User Story 1 - Active Subdomain Reverse Proxying (Priority: P1)

**Goal**: Configure Nginx routing mapping and legacy domain redirections.

- [x] T006 [US1] In `docker/nginx/massar.conf`, clean all active server blocks of legacy domains (`massarplatform.com` and `bsma-academy.com`). Set up catch-all redirect server blocks for `massarplatform.com` and `bsma-academy.com` (and their subdomains) that return a `301` redirect to the equivalent URL on `massar-academy.net`.

---

## Phase 4: User Story 2 & 3 - Verification Additions & Checks (Priority: P1)

**Goal**: Enhance the verification script and perform checks.

- [x] T007 [US2] In `scripts/verify-surface-separation.mjs`, add static checks verifying that legacy domains do not appear in the active server blocks of `massar.conf`.
- [x] T008 [US2] In `scripts/verify-surface-separation.mjs`, add automated runtime check requests verifying:
  - Header validation (`x-massar-surface` must equal the active surface).
  - DOM validation (`data-massar-surface` attribute value in response HTML).
  - Cross-boundary forbidden redirects (e.g. requesting `/admin` on student port redirects to admin surface).
- [x] T009 [US2] Run `node scripts/verify-surface-separation.mjs --static-only` locally to verify that all static checks pass successfully.

---

## Phase 5: Quality Gate & Builds

**Goal**: Ensure clean code, testing, and production build compliance.

- [x] T010 [P] Execute `clean-code-guard` against all modified frontend files and resolve any findings.
- [x] T011 [P] Execute `test-guard` against any modified test files (or record that none changed) and resolve any findings.
- [x] T012 [P] Perform a final production build verification on the frontend (`npm run build` or equivalent) to ensure no warnings or compilation issues exist.
