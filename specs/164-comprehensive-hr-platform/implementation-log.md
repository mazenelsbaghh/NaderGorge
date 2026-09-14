# Implementation Log

## 2026-07-22 — T001/T004/T005 baseline

- Active branch: `160-employee-realtime-refresh`; Spec Kit feature is selected explicitly through `SPECIFY_FEATURE=164-comprehensive-hr-platform` because the working tree was already on another feature branch.
- Starting commit observed: `e19a44d58 chore: use dedicated production SSH key`.
- User-owned modification preserved: `.agents/skills/ssh-server/docs/database_schema.md`.
- During planning, untracked 164 artifacts and previously observed product edits disappeared after an external worktree change; the 164 artifacts were restored and revalidated. No removed external product edits were recreated.
- Ignore verification passed: root `.gitignore` covers .NET, Node, build, logs, env, IDE and test artifacts; `.dockerignore` excludes dependencies/build/secrets/specs; `frontend/eslint.config.mjs` and `.prettierignore` contain generated/dependency/build exclusions. No ignore edit required.
- Legacy HR inventory: `EmployeeProfile`, `AttendanceLog`, `EmployeeVacation`, `PayrollRecord`, `PayrollAdjustment`; HR commands/queries live in `Application/Features/HR`, payroll in `Application/Features/Admin/Finance`.
- Critical baseline issues confirmed: leave approval creates `ClockOut=null` attendance rows; employee query infers workforce from non-Student roles; employee create is account-only then profile save; HR uses coarse `hr.manage`; employee payroll controller uses Admin/Supervisor roles and shares surface with teacher finance.
- Realtime consumers to preserve: `hr:employees`, `hr:attendance`, `hr:vacations`, `finance:payroll`; clock-in/out also changes live-support eligibility.
- Rollout checkpoints: Legacy → ShadowValidated → NewActive per module; compatibility reads removed only after rollback window and recorded go/no-go; no module allows two writers.

## Verification results

- Baseline `make verify`: PASS. .NET build 0 warnings/0 errors; Application tests 429 passed, 1 skipped; PostgreSQL integration skipped because connection string was not set; frontend lint and Next build passed; worker TypeScript build passed; `docker compose config -q` passed.
- The standalone frontend lint/build requested by T003 is included in the successful `make verify` execution and produced 93 routes without TypeScript errors.

## 2026-07-22 — Wave 0 and atomic employee slice

- Test-first evidence: focused run failed exactly on teacher/admin workforce leakage and synthetic leave rows; future leave produced `You already have an active clock-in session` before the fix.
- Production fix: leave approval no longer writes AttendanceLog rows; EmployeeProfile is the workforce source.
- `CreateEmployeeCommand` creates User/UserRole/EmployeeProfile/Audit/IdempotencyRecord in one SaveChanges unit, requires actor and idempotency key, rejects missing roles/duplicate phone, and replays the original result without duplicate rows.
- Assistant/staff creation UI now collects salary/start/daily hours and calls the atomic endpoint; Student/Admin generic account creation remains separate.
- Added `AddHrSafetyFoundation` migration with idempotency/rollout tables and Restrict delete behaviors for user→employee, employee→attendance/leave/payroll and payroll→adjustments.
- Finance API now uses `finance.manage` permission rather than hard-coded Admin/Supervisor roles; route/root/settings mappings no longer map finance to `users.manage` or `payments.manage`.
- Focused result: 23 passed, 0 failed across foundation, provisioning, employee, vacation and payroll tests. Frontend lint passed and production build passed after adding the provisioning export.
- After merging concurrent teacher-isolation work, the full Application suite passed with 442 passed, 1 skipped and 0 failed. Frontend lint and production build passed with 93 generated routes.
- Employee lifecycle slice added deterministic unique `EMP-<GUID>` numbers, status, hire/termination dates and work mode. `AddEmployeeLifecycle` backfills existing profiles from their IDs and creation dates before applying non-null/unique constraints; it contains no teacher-isolation schema operation.
- Real PostgreSQL HR foundation suite ran against isolated `massar_hr_test`: 2 passed, 0 failed. It proved composite idempotency uniqueness, one rollout row per module and restrictive employee-history deletion at the database layer.
- Docker PostgreSQL initially restarted on a stale `postmaster.pid`; the exact stale lock file was removed after confirming the container PID was zero. PostgreSQL completed crash recovery and returned healthy without deleting database data.
- Organization/contract slice added effective-dated units, positions, grades, locations, cost centers, assignments and contracts; rules cover cycles, self-manager, overlap and closed contract transitions. Exit disables login while retaining the employee profile/history. Focused organization tests passed 6/6.
- Full Application regression after the organization slice: 448 passed, 1 skipped, 0 failed; solution build completed with 0 warnings and 0 errors.
- `make up` completed with all project services healthy, and a subsequent backend-only image rebuild succeeded. `make migrate` then hit Docker Desktop BuildKit `metadata_v2.db: input/output error`; the Docker engine stopped responding and port 5435 closed. No project data was deleted and the migration gate remains open until the engine is restarted/stable.

## 2026-07-22 — HR audit, scoped authorization and organization UI

- Added a transaction-local HR audit writer requiring either a real database actor or a named system actor. It records actor snapshot, reason, request/IP/correlation context and recursively redacts salary, amount, phone, password, token, bank, document, attachment, evidence and case values. `AddHrAuditContext` contains only the four audit context columns.
- Added MediatR handler authorization with `self`, `direct-team`, `organization-subtree` and `all` scopes. Admin remains an explicit bypass; non-admin permissions are resolved from database role grants and employee scope is resolved from current effective assignments.
- Replaced coarse `hr.manage` checks on the current HR controllers with granular employee, organization, contract, attendance and leave permissions.
- Added the organization tree and employee profile UI with assignment and contract history, plus an idempotent system-actor lifecycle notifier for contract/probation deadlines through NotificationEvent/outbox.
- Focused HR result: 64 passed, 0 failed. Frontend lint passed; production build passed and generated 94 routes including `/admin/hr/organization` and `/admin/hr/employees/[id]`.
- The employee provisioning authorization/browser test is implemented. Its live Playwright execution remains open because `docker info` still hangs after printing only the client section; the Docker server is unavailable and was not broadly restarted because that could affect unrelated user projects.

## 2026-07-22 — Shift planning wave

- Test-first compile failed on the missing scheduling namespace, then the completed focused shift suite passed 4/4: overnight attribution, split segment overlap, atomic publication conflict and two-level swap approval retaining originals.
- Added work calendars, templates, segments, assignments and swap requests; publication is idempotent and validates every row before a write. PostgreSQL adds an exclusion constraint over employee/date ranges for published assignments, in addition to application validation.
- Added granular template/calendar/assignment/validation/publish/swap APIs and the responsive Arabic shift editor with pre-publication conflict feedback.
- Focused HR regression passed 68/68. Frontend lint and production build passed with 95 routes, including `/admin/hr/shifts`. Playwright files are present; live execution remains part of the final gate while Docker is unavailable.

## 2026-07-22 — Trusted attendance and correction waves

- Added unrestricted/geofence/trusted-device policies with employee-over-shift precedence, bounded accuracy/radius checks, hashed device tokens and time-bounded remote exceptions. Rejected attempts persist decision evidence but never create a session.
- Added idempotent clock-in/break/clock-out sessions with database uniqueness for source replays, one open employee session and one open break. Accepted state changes retain the existing live-support assignment/release coordination.
- Added calculation and missing-clock bounds, versioned two-stage corrections, self-approval prevention, immutable before/applied evidence and dry-run recalculation.
- Shared responsive attendance and correction UI now mounts on employee, assistant and admin surfaces; reviewer diff UI is available at `/admin/hr/attendance-corrections`.
- Test-first evidence captured missing namespaces before implementation. Focused HR regression passed 80/80; frontend lint and production build passed with 97 routes. Live policy/browser scenarios remain in the final Docker-dependent Playwright gate.
## Leave and multilevel approvals (T066-T076)

- Added work-calendar-aware leave calculation, balance reservation/release/debit ledger, overlap and attachment checks, and idempotent finalization through workday classifications without creating attendance sessions.
- Added versioned approval definitions with three resolver choices (direct manager, permission, specific user), ordered decisions, active-window delegation, self-approval prevention, SLA escalation to the next manager, durable idempotency records, and escalation outbox events.
- Added database constraints and migration `20260722190003_AddHrLeaveApprovals`, secured leave/approval/delegation/configuration APIs, employee leave workspace, HR policy editor, approval rule editor, reviewer inbox, and delegation form.
- Focused backend result: 6/6 leave and approval tests passed. Backend build passed with 0 warnings/errors; frontend lint and production build passed with 100 routes.
## Configurable employee payroll (T077-T086)

- Added effective-dated pay components, constrained versioned rules, compensation history, payroll runs, employee snapshots, unique explained lines, immutable payslips, and later-run settlement links.
- The formula engine accepts only documented expressions (`base`, fixed, percentage, attendance metrics, or named input multiplied by a configured rate), rounds away from zero to two decimals, and snapshots inputs/explanations/rule versions.
- Added prepare/replay, finance review/approval, GM approval, pay, close, return and settlement workflows with optimistic versions and a PostgreSQL trigger preventing mutation of final payroll lines.
- Removed employee payroll actions from the broad teacher-finance controller and exposed granular employee payroll permissions and self-only payslips through `HrPayrollController`.
- Focused payroll tests: 5/5 passed. Backend build passed with 0 warnings/errors; frontend lint and production build passed with 102 routes.
## Advances, loans, expenses and commissions (T087-T092)

- Added evidence-backed employee financial requests, versioned approval, exact installment schedules with last-installment rounding reconciliation, outstanding balances, and payroll source links.
- Applying due installments changes employee/run totals once, records a unique source, and turns a fully applied request into settled state; replays return zero changes.
- Added self/admin APIs and a responsive employee request, balance and installment workspace.
- Focused payroll and financial request tests: 7/7 passed. Frontend lint and production build passed with 103 routes.
## Employee documents, assets and self-service (T093-T098)

- Added versioned employee document metadata, expiry/retention/legal-hold controls, idempotent expiry outbox alerts, self/admin download authorization and download audit evidence.
- Added asset inventory and custody history, unique active assignment, return/waiver flows, and a hard offboarding check for open custody unless an authorized exception is recorded.
- Added the responsive employee hub with service links plus explicit loading, empty, retry/error, document and asset states.
- Focused document/asset tests: 2/2 passed. Frontend lint and production build passed with 104 routes.
## Performance and confidential employee cases (T099-T104)

- Added weighted performance cycles that cannot activate unless goals total exactly 100%, published score snapshots, employee appeals and optimistic versions.
- Added confidential case, evidence, response and disciplinary action history with server-side restricted reads and self-decision prevention.
- Approved financial penalties generate one uniquely sourced payroll line and are replay-safe.
- Focused performance/case tests: 3/3 passed. Frontend lint and production build passed with 106 routes.
## Recruitment, onboarding and offboarding (T105-T111)

- Added requisitions, candidates, interviews, versioned offers, lifecycle tasks and offboarding processes with durable history and constraints.
- An accepted offer converts in one serializable transaction into a login account, Staff role, employee profile, compensation history and default onboarding/probation tasks; replay returns the same employee.
- Offboarding checks active asset custody and financial balances, closes lifecycle tasks, terminates the employee and increments the account security stamp while preserving all history.
- Focused recruitment/lifecycle tests: 2/2 passed. Frontend lint and production build passed with 108 routes.
## Permissions, migration, retention and reports (T112-T120)

- Added an HTTP permission matrix covering employee, manager, HR, finance, GM and outsider outcomes.
- Added durable migration batches, source-to-target maps and conflicts; dry-run replay, exact count/total/hash reconciliation, ordered module activation and independent rollback keep exactly one write target.
- Added legal-hold-aware retention dry-run/execution, document archival, rejected-candidate anonymization and audit evidence.
- Added organization-scoped workforce reports with paging, attendance/leave/payroll projections and audited CSV exports containing only authorized rows.
- Focused governance tests: 11/11 passed. Frontend lint and production build passed with 110 routes.

## Cross-cutting cache, accessibility and reporting indexes (T121-T123)

- Normalized the complete HR cache namespace across people, organization, contracts, shifts, attendance, corrections, leave, approvals, payroll, financial requests, documents, assets, performance, cases, recruitment, lifecycle, migration and reports. Realtime HR events now invalidate every canonical HR store and refresh both admin and employee routes.
- Query-contract verification passed with exact coverage for 32 service files and 267 mutations. Frontend lint passed.
- Added an authenticated employee layout that withholds page content until session/staff checks complete, routes the built-in Employee role to `/employee` on the assistant surface, and allows only that surface to accept employee return URLs. The shared RTL, visible-focus and reduced-motion rules were verified; coarse-pointer links, buttons, selects and summaries now enforce 44px targets.
- Added composite indexes for workforce status/date, effective organization assignment, leave queues, payroll-run state/date and employee payroll lookup. EF generated and reviewed `20260722195012_AddHrReportingIndexes`; its `Up` contains index-only operations.
- Migration-cycle focused tests passed 4/4, including all five modules completing dry-run → exact count/total/hash reconciliation → activation → rollback → reactivation in dependency order.

## 2026-07-23 — Final verification and release review (T124-T132)

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj` completed with 511 passed, 1 intentionally skipped Redis opt-in test, and 0 failed out of 512.
- The complete HR Playwright set passed on Chromium and WebKit: 48 passed, 0 failed. It covered employee provisioning, surface access, attendance, shifts, leave/approval, payroll, self-service, lifecycle and governance flows.
- The final `npm run lint && npm run build` passed on the current source and generated 110 routes. `make verify` passed: .NET restore/build had 0 warnings and 0 errors, the same 511/1/0 test result passed, frontend lint/build passed, worker TypeScript passed and Compose validation passed.
- `make up` built and started the platform. `make migrate` completed against the real Docker PostgreSQL database with no pending migrations. `make ps` and direct health probes confirmed backend, worker, nginx, landing, student, admin, teacher, assistant, PostgreSQL and Redis healthy.
- Fixed final-gate defects: explicit PostgreSQL text-to-JSONB migration casts, unique E2E support employee numbers, insecure-origin client ID fallback, handled SignalR negotiation cancellation, hydration-safe surface detection, a body-level user drawer portal, non-predictable candidate onboarding passwords and syntax-specific attendance evidence parsing.
- The authorization matrix, deterministic five-module migration evidence, architectural/UI critique, clean-code review, test review, accepted risks and go/no-go are recorded in `verification-report.md` and `review-report.md`.
