# Phase 2 Foundation Report

**Date**: 2026-07-29  
**Status**: GO for navigation implementation.

## Implemented scope

- Added one repository-owned query client/provider with deterministic keys,
  in-flight deduplication, freshness, cancellation, retained data, targeted
  invalidation, protected-cache removal, and no new dependency.
- Added canonical student/admin/support query keys and route/workflow
  performance budgets.
- Added a root client provider island for query state, user motion preference,
  auth bootstrap, and toast ownership.
- Replaced the duplicate Admin layout permission matrix with the typed policy
  already shared by navigation. Only the built-in Admin role bypasses; scoped
  supervisor/staff roles remain permission and allowed-navbar constrained.
- Documented and enforced the user's local no-download boundary.

## Automated evidence

- `npm run check:query-client` — PASS.
- `npm run check:route-permissions` — PASS for 24 navigation routes.
- Local TypeScript strict check using existing `node_modules/.bin/tsc` — PASS.
- Focused ESLint over all Phase 2 files — PASS with zero warnings/errors.

No command downloaded a package, browser, image, SDK, or tool.

## Docker gate

Local Docker is deliberately not run because required images were not fully
present and the user prohibited downloads. Full Compose/build/health gates
remain mandatory on the reviewed remote builder before production.

## Manual QA checklist

- Admin full role: canonical policy permits all existing admin routes.
- Scoped staff/supervisor: only configured domains, navbar paths, and
  permissions are eligible.
- Unauthorized route: redirects to `/admin/unauthorized`.
- Query provider: no user-visible page has been migrated yet, so there is no
  product-state change to manually approve in this foundation.

These flows require browser execution in the later US1/US2 checkpoints.

## Risks

- Public navigation still resides at the root until US1 route-group/shell work.
- Existing screens still use legacy cache registrations until migrated
  incrementally.
- Remote build/Docker evidence remains a release blocker, not a waived test.
