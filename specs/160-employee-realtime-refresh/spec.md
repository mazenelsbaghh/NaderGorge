# Feature Specification: Employee Workflows and Realtime Refresh

**Feature Branch**: `160-employee-realtime-refresh`
**Created**: 2026-07-12
**Status**: Draft
**Input**: Approved Arabic feature brief derived from `docs/employee-and-realtime-refresh-remediation-plan.md`.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.

  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Employee and permission changes are immediately consistent (Priority: P1)

An administrator or authorized employee creates, edits, disables, or changes permissions for an employee and sees the result immediately in the current list, detail, lookup, profile, navbar, and protected route surfaces. Other connected sessions converge through realtime events without a full-page refresh.

**Why this priority**: Stale permissions and employee records can create incorrect access, operational mistakes, and security exposure.

**Independent Test**: Use two authenticated browser sessions: mutate an employee in session A, then verify lists, lookups, navbar, and access in session A and session B without reload.

**Acceptance Scenarios**:

1. **Given** an employee list and lookup are open, **When** an employee is created or disabled successfully, **Then** all active affected queries show the new state within one second in the mutating session.
2. **Given** an employee session is open, **When** an administrator changes or revokes its permissions, **Then** the session refreshes authorization state within two seconds when connected, rebuilds its navbar, and redirects to a safe route if the current route is no longer allowed.
3. **Given** a form draft is being edited, **When** an external realtime event changes the same record, **Then** the draft remains intact and a conflict/reload decision is shown.

---

### User Story 2 - Mutations keep server data fresh without manual refresh (Priority: P1)

Users across administration, HR, operations, CRM, content, finance, assessments, community, notifications, and reporting see successful changes reflected through a single server-state contract. A mutation updates or invalidates its declared query keys locally; SignalR synchronizes other active sessions.

**Why this priority**: The current 217 mutations and page-local loading patterns produce inconsistent stale data and duplicate cache behavior.

**Independent Test**: Verify the mutation inventory has a typed update/invalidation contract and exercise representative mutations from every migrated domain.

**Acceptance Scenarios**:

1. **Given** two components request the same active query, **When** both render, **Then** only one network request is issued for the shared query key.
2. **Given** a mutation succeeds, **When** its response is handled, **Then** the local cache is updated or invalidated before success feedback is finalized and no stale service cache masks the result.
3. **Given** a duplicated or burst realtime event, **When** it is received, **Then** event deduplication and debounced active-query refetch prevent duplicate rows and request storms.

---

### User Story 3 - Reconnect and failure paths reconcile safely (Priority: P1)

When SignalR disconnects, reconnects, or misses events, the application rejoins authorized groups and reconciles active critical queries with a server snapshot. Failed mutations roll back optimistic state where used and expose actionable errors.

**Why this priority**: Realtime delivery is advisory; correctness must survive transient transport failures.

**Independent Test**: Simulate offline/reconnect, duplicate events, failed mutations, and permission revocation, then compare the UI with backend state.

**Acceptance Scenarios**:

1. **Given** a connected client loses SignalR, **When** it reconnects, **Then** it rejoins groups and invalidates/refetches active critical queries without reloading the document.
2. **Given** an optimistic mutation fails, **When** the error is returned, **Then** the prior state is restored and the user receives a validation or server error.
3. **Given** a permission is revoked while a protected screen is open, **When** the next API request is made, **Then** the backend returns 403 and the UI enters a safe denied state even if no realtime event arrived.

---

### Edge Cases

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right edge cases.
-->

- A repeated event with the same `eventId` is ignored after the first invalidation.
- A broad scope invalidates only active queries mapped to that scope; inactive pages are not refetched.
- Reconnect reconciliation is safe when no event sequence is available and must not expose unauthorized data.
- Full-page reload remains only for documented security recovery cases such as an unrecoverable secure-video session; new reload workarounds are prohibited.
- Concurrent employee edits use a row version/ETag-style check and return a conflict without silently overwriting another edit.
- Empty, loading, retry, canceled, and unauthorized states remain explicit for every migrated domain.

### Manual QA & Docker Acceptance *(mandatory)*

<!--
  ACTION REQUIRED: Define what the product owner must test manually and what
  Docker evidence is required before this feature/phase can be considered done.
-->

- **Manual QA Role/Flow 1**: Admin creates/edits/disables an employee and verifies employee list, lookup, HR view, profile, and permission-dependent navbar update without reload.
- **Manual QA Role/Flow 2**: Two staff sessions update an HR/operations record and verify cross-session convergence, duplicate-event safety, and reconnect recovery.
- **Manual QA Negative Check**: Revoke a permission; verify backend 403, safe redirect/denied state, no stale navbar item, and preserved edit draft on external changes.
- **Docker Acceptance**: `docker compose config -q`, migrations, backend/frontend/worker health, `make verify`, focused frontend lint/typecheck/build, and E2E contract checks.
- **External Dependencies**: PostgreSQL, Redis, SignalR transport, and the project’s local E2E domain setup; unavailable external services must be recorded rather than bypassed.

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: Every employee/user mutation MUST declare the affected domain, entity, operation, and typed query keys to update or invalidate.
- **FR-002**: The application MUST use one server-state query cache per migrated domain with deduplicated requests, explicit stale/retry policy, and no conflicting module-level cache.
- **FR-003**: A successful mutation MUST update the current session immediately from its response or invalidate/refetch active affected queries without document reload.
- **FR-004**: SignalR data-change events MUST use a stable envelope containing `eventId`, timestamp, authorized scopes, operation, and optional entity identifiers; clients MUST deduplicate events.
- **FR-005**: Reconnect MUST rejoin authorized groups and reconcile active critical queries; the system MUST not assume SignalR delivery is the source of truth.
- **FR-006**: Permission or employee status changes MUST refresh the current session contract, rebuild navbar/route guards, and safely exit routes that are no longer authorized.
- **FR-007**: Backend authorization MUST remain authoritative and MUST reject revoked access even when frontend session state is stale.
- **FR-008**: Employee edits MUST use concurrency protection and expose a conflict state without silently overwriting a newer version.
- **FR-009**: Realtime updates MUST not overwrite active form drafts; external changes require a visible conflict/reload decision.
- **FR-010**: Domain migrations MUST provide loading, empty, failure/retry, cancellation, and permission-denied behavior.
- **FR-011**: The implementation MUST inventory and classify all current mutations, service caches, force-refresh calls, and full-page reloads; any remaining reload MUST be documented and allowlisted.
- **FR-012**: State-changing backend operations MUST preserve existing validation, authorization, audit, transaction, and error-response conventions.
- **FR-013**: The system MUST expose metrics for mutation outcome, mutation-to-UI latency, realtime delivery/reconnect, invalidation/refetch counts, duplicate/missed events, and 401/403 after permission changes.

*Example of marking unclear requirements:*

### Key Entities *(include if feature involves data)*

- **Session Authorization Snapshot**: Current identity, roles, permissions, allowed domains/navbar items, and authorization/session version for the authenticated user.
- **Employee Record**: User identity, employee profile/status, role/permission associations, and concurrency version.
- **Query Contract**: Typed query key, fetch policy, owning domain, mutation invalidation/update rules, and active/inactive refetch behavior.
- **Realtime Change Event**: Deduplicated event envelope containing event ID, scopes, operation, optional entity IDs, and occurrence time.
- **Domain Cache State**: Server-state entries with loading/error/stale/active status and reconciliation metadata; durable business state remains backend-owned.

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: 100% of inventoried mutations have an update/invalidation contract and a verification case.
- **SC-002**: Same-session employee and HR changes are visible in affected active views within 1 second after a successful response.
- **SC-003**: Connected cross-session permission changes converge within 2 seconds; backend denial remains immediate on unauthorized requests.
- **SC-004**: Reconnect reconciliation restores active critical queries to server-consistent state without document reload in 100% of tested scenarios.
- **SC-005**: Duplicate events produce zero duplicate records/toasts and event bursts stay within the configured debounce/request budget.
- **SC-006**: No migrated domain retains a conflicting service-level cache, and all remaining full-page reloads are documented in an allowlist.
- **SC-007**: Required backend, frontend, Docker, and Playwright verification commands pass, or each external blocker is recorded with evidence.

## Assumptions

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right assumptions based on reasonable defaults
  chosen when the feature description did not specify certain details.
-->

- Existing authentication, authorization, audit, EF Core, PostgreSQL, Redis, SignalR, Axios, and Next.js layers are reused and evolved incrementally.
- The full remediation plan is in scope, but delivery remains domain-by-domain behind feature flags/canary controls where practical.
- Mobile/responsive behavior follows existing frontend support; a separate mobile redesign is out of scope.
- TanStack Query is the recommended server-state implementation; the planning phase must confirm it against dependency, bundle, and migration constraints before adoption.
- Full document reloads are out of scope except for explicitly documented security recovery cases.
- Exact stale-time values, event sequence retention, metric backend, and allowlisted reloads are planning decisions, not product behavior changes.

## Clarifications

### Session 2026-07-12

- Q: ما هو نطاق التنفيذ؟ → A: تنفيذ كل مراحل خطة إصلاح الموظفين والتحديث الفوري على كامل النطاق.
- Q: هل يسمح بإضافة TanStack Query؟ → A: نعم، إذا أثبت التخطيط أنها الأنسب.
- Clarification result: لا توجد غموضات مواصفات عالية التأثير تستلزم سؤالًا إضافيًا؛ تفاصيل stale-time وsequence retention وfeature flags مؤجلة للتخطيط.
