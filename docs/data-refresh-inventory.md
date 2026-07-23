# Data Refresh Inventory — Final contract checkpoint 2026-07-13

The machine-readable checkpoint reconciles the planned 217 service mutation operations: 27 service files and 217 `apiClient.post|put|patch|delete` calls are covered by `frontend/src/lib/query-contracts.ts`. The checker compares source counts and file names, so a new service mutation fails CI until its contract is added.

## Inventory schema

| Field | Required value |
|---|---|
| Source | Exact file and function/component |
| Endpoint | HTTP method and route |
| Domain | users, hr, operations, crm, support, content, codes, finance, assessments, community, notifications, media, forms, settings, or other |
| Query keys | Typed keys updated/invalidated |
| Current behavior | local state, manual load, force, cache registry, event, reload, or none |
| Realtime event | Event type and scope |
| Failure/permission | validation, 401/403, conflict, rollback behavior |
| Verification | Exact unit/integration/E2E command |

## Final scan evidence

```text
service files with mutations: 27
direct service mutation signatures: 217
typed mutation contract records: 27
unallowlisted `force: true` calls: 0 in service files
unallowlisted full-page reload call sites: 0
```

Commands:

```bash
rg -n "(axios|api)\\.(post|put|patch|delete)|\\.(post|put|patch|delete)\\(" frontend/src/services frontend/src/app frontend/src/components --glob '*.ts' --glob '*.tsx'
rg -n "registerCacheStore|force:\\s*true|window\\.location\\.reload|router\\.refresh" frontend/src
node frontend/scripts/check-query-contracts.mjs
cd frontend && npm run lint && npm run typecheck
```

## Known cache/reload surfaces

| Surface | Current evidence | Planned contract |
|---|---|---|
| `frontend/src/lib/cache-invalidation.ts` | Prefix invalidation stops after first match; 200ms debounce | Typed all-match active-query invalidation with invalidation/refetch counters |
| `frontend/src/services/content-service.ts` | Module cache and force fetch | Content query keys/hooks |
| `frontend/src/services/admin-service.ts` | Code-group cache and force fetch | Codes/admin query keys/hooks |
| `frontend/src/components/layout/StaffRealtimeBoundary.tsx` | Revision context has no production consumer | Scope-to-query adapter |
| `frontend/src/components/live-support/student-context/StudentContextPanel.tsx` | Full reload after action | Student-context invalidation |
| `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel.tsx` | Full reload after playback action | Targeted playback recovery or documented security allowlist |
| `frontend/src/components/video/SecureVideoPlayer.tsx` | Security recovery reloads | Retain only if session renewal cannot be in-component |

## Migrated domains and remaining allowlist

The checkpoint covers users/HR, operations, CRM, support, content, codes, finance, assessments, community, notifications, media, forms, reports, settings, and student balance through typed mutation records and canonical realtime-key routing. The only reload retained by policy is the documented secure-video recovery path in `frontend/src/components/video/SecureVideoPlayer.tsx`; the reload checker enforces this allowlist.

Runtime blockers: Playwright E2E requires the backend at `127.0.0.1:5245` and Chromium; full Docker verification requires a running Docker daemon.

## Completion rule

This file is not complete until every mutation has a typed contract, every cache is classified as migrated/legacy/allowlisted, and every remaining reload has a documented reason and test.
