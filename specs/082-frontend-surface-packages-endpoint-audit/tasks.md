# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

# Tasks: Frontend Surface Packages, Register Branding, and Endpoint Audit

Target prompt: create the tasks file so that a cheaper llm model can implement without problems

## Phase A - Shared Brand Logo

- [x] T001 In `frontend/src/packages/brand/platform-identity.ts`, export `PLATFORM_IDENTITY` with Arabic name, English name, full logo path `/images/logo.svg`, mark logo path `/images/logo-mark.svg`, and default alt text.
- [x] T002 In `frontend/src/packages/brand/index.ts`, re-export all exports from `platform-identity.ts`.
- [x] T003 In `frontend/src/components/shared/PlatformLogo.tsx`, create a client-safe reusable `PlatformLogo` component using `next/image`, supporting `variant: "mark" | "full"`, `size: "sm" | "md" | "lg"`, optional `className`, and accessible alt text.
- [x] T004 In `frontend/src/app/(public)/register/page.tsx`, import `PlatformLogo` and replace the hardcoded `𓂀` inside `.auth-avatar` with `<PlatformLogo variant="mark" size="lg" priority />`.
- [x] T005 In `frontend/src/components/layout/GlobalNav.tsx`, remove the `SphinxMark` import and replace the animated login icon content with `<PlatformLogo variant="mark" size="sm" />`.
- [x] T006 In `frontend/src/app/(public)/auth.css`, update `.auth-avatar` so image content fits without relying on font-size glyph styling.

## Phase B - Frontend Surface Packages

- [x] T007 In `frontend/src/packages/landing/home.tsx`, move the landing page composition and registered-students fetch helper from `frontend/src/app/page.tsx` into a `LandingHome` server component.
- [x] T008 In `frontend/src/packages/landing/index.ts`, re-export `LandingHome`.
- [x] T009 In `frontend/src/app/page.tsx`, replace inline landing composition with `import { LandingHome } from "@/packages/landing"` and return `<LandingHome />`.
- [x] T010 In `frontend/src/packages/admin/navigation.ts`, export `adminMenuItems` for layout navigation and `adminRootLinks` for the admin home page using existing labels, hrefs, body copy, and lucide icon references.
- [x] T011 In `frontend/src/packages/admin/index.ts`, re-export all admin package exports.
- [x] T012 In `frontend/src/app/admin/layout.tsx`, import `adminMenuItems` from `@/packages/admin` and remove the local duplicate menu array.
- [x] T013 In `frontend/src/app/admin/page.tsx`, import `adminRootLinks` from `@/packages/admin` and remove the local duplicate link array.
- [x] T014 In `frontend/src/packages/student/dashboard.ts`, re-export the dashboard components currently imported by `frontend/src/app/student/page.tsx`.
- [x] T015 In `frontend/src/packages/student/index.ts`, re-export all student package exports.
- [x] T016 In `frontend/src/app/student/page.tsx`, import dashboard components from `@/packages/student` instead of directly from multiple component directories.

## Phase C - Endpoint Inventory and Tests

- [x] T017 In `scripts/generate-endpoint-inventory.mjs`, implement controller parsing for class route, HTTP method attributes, action method name, authorization classification, source file, and line number.
- [x] T018 In `scripts/generate-endpoint-inventory.mjs`, implement `--check` mode that compares generated output to `tests/endpoint_inventory.json` and exits non-zero on mismatch.
- [x] T019 Run `node scripts/generate-endpoint-inventory.mjs` to generate `tests/endpoint_inventory.json` and `tests/endpoint_inventory.md`.
- [x] T020 In `tests/test_endpoint_inventory.py`, add tests that run the generator in `--check` mode, validate the JSON schema fields, and assert every endpoint path starts with `/api/`.

## Phase D - Verification

- [x] T021 Run `node scripts/generate-endpoint-inventory.mjs --check` and fix any mismatch.
- [x] T022 Run `pytest tests/test_endpoint_inventory.py` and fix any failure.
- [x] T023 Run `cd frontend && npm run lint` and fix any new lint failure caused by this feature.
- [x] T024 Record verification results in the final report and mark all phases complete in `achievements.md`.
