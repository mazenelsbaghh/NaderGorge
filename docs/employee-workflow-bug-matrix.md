# Employee Workflow Bug Matrix — Final contract checkpoint 2026-07-13

| ID | Role/session | Trigger | Current risk | Expected observable result | Verification |
|---|---|---|---|---|---|
| EMP-001 | Admin, one session | Create employee | List and lookup can remain stale | New employee appears in all active affected lists without reload | Playwright US1 create |
| EMP-002 | Admin, one session | Edit employee profile | Detail/list/HR views can diverge | All active employee/HR queries converge from mutation response/invalidation | HR integration + Playwright |
| EMP-003 | Admin, one session | Disable employee | Lookup may still offer inactive employee | Disabled state disappears from eligible lookups and protected actions | HR permission-negative test |
| EMP-004 | Admin + employee session | Change employee roles | JWT/Zustand/navbar can remain old | Session snapshot refreshes, navbar rebuilds, protected route reevaluates | Two-session Playwright |
| EMP-005 | Admin + employee session | Revoke permission | UI may show access until reload | Backend rejects immediately; UI safely redirects/denies after event or 403 | API + Playwright |
| EMP-006 | Two staff sessions | Attendance/vacation change | HR dashboard/report may not converge | Active HR/KPI queries refetch for connected peers | HR realtime E2E |
| EMP-007 | Two staff sessions | Reconnect after event loss | Client may retain stale screen | Groups rejoin and active critical queries reconcile | Reconnect E2E |
| EMP-008 | Employee editing form | External update | Blind refetch can erase draft | Draft remains and conflict decision appears | Conflict E2E |
| EMP-009 | Any staff | Duplicate/burst events | Duplicate rows/request storm | Event IDs dedupe, active queries batch, no duplicate toast | Realtime contract/E2E |

## Final evidence

| Area | Result |
|---|---|
| Mutation inventory | 217 service mutations across 27 files; exact source/count contract check passes |
| Canonical event routing | Platform event handlers route through `realtime-invalidation-map.ts`; detail keys remain supported |
| Metrics assertions | Contract test asserts mutation-visible-refresh and reconnect-duration counters; cache invalidation records invalidation/refetch counts |
| Static checks | `node frontend/scripts/check-query-contracts.mjs` and `npm run lint` pass |
| Typecheck | `npm run typecheck` passes |
| E2E | Blocked until backend E2E service and Chromium are available |

## Rollout decision

Enable employee/HR for internal staff first, compare stale-state, duplicate-event, refetch, and 401/403 metrics for two business days, then expand domain-by-domain. Rollback disables the feature flag; compatible API/schema changes remain in place and unaffected domains can continue using the legacy adapter.

## Scope boundary

Backend authorization, audit, transactions, and durable employee/HR state are not replaced by client cache behavior. Student/video security reloads require separate allowlist decisions.
