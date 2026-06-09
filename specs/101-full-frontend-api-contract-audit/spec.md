# Feature Specification: Full Frontend API Contract Audit

**Feature Branch**: `100-endpoint-alignment-and-sidebar-sync`
**Created**: 2026-06-09
**Status**: Draft
**Input**: User description: "عايزك تعمل مراجعه دقيقه لكل الحقول و كل endpont بتاعتهم من الفرونت للباك و تشوف اي ناقص و تضيفوا و تعمل بيها بالريكوستات بناءآ هلي اللي موجود ف الفرونت فاهمني مش اللي موجود عموما لكل حاجه حرفيا ف البروجكت يعني مراجعه دقيقا علشان لاقي ؛ل حاجه مظبطوه كل حاجه"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Frontend Service Requests Never Hit Missing Backend Routes (Priority: P1)

As an operator using any current frontend surface, I need every request made by the existing frontend service layer to resolve to an implemented backend route with the correct HTTP method, path parameters, and query-string names.

**Why this priority**: A route mismatch causes hard failures such as 404, 405, or unusable screens. The audit must prioritize what the frontend actually calls, not endpoints that merely exist in the backend.

**Independent Test**: Generate an inventory from `frontend/src/services` and compare every discovered request against `backend/src/NaderGorge.API/Controllers`. The comparison must report zero missing frontend-called routes.

**Acceptance Scenarios**:

1. **Given** a frontend service method calls `/api/admin/users/{id}/devices`, **When** the contract audit runs, **Then** it confirms an active backend action with the same method and route template exists.
2. **Given** a frontend service passes a query parameter such as `studentId` or `status`, **When** the backend action accepts the request, **Then** the accepted query/route parameter name matches the frontend request field.
3. **Given** the backend exposes a route not called by the frontend, **When** the audit runs, **Then** it does not force unnecessary frontend work unless the route is part of an existing frontend flow.

---

### User Story 2 - Frontend Payload Fields Match Backend Request DTOs (Priority: P1)

As a user submitting any existing form, I need every field sent by the frontend to be accepted by the backend request DTO without casing, enum, nullability, or type mismatch failures.

**Why this priority**: Field mismatch is the direct cause of failed saves, bad validation responses, and data silently not persisting.

**Independent Test**: Statically inspect frontend service payload shapes and representative form call-sites, compare them with backend command/request records used by the matching controller actions, then run focused regression tests for changed endpoints.

**Acceptance Scenarios**:

1. **Given** a frontend form includes optional `notes`, `description`, `assignedToUserId`, or enum status fields, **When** the request reaches the backend, **Then** the backend DTO includes compatible nullable or required properties.
2. **Given** the frontend sends camelCase JSON, **When** ASP.NET Core model binding runs, **Then** it binds to the expected PascalCase DTO properties without custom per-endpoint adapters.
3. **Given** a frontend enum field can be represented as a string or number, **When** the request is parsed or the response is displayed, **Then** the user sees the correct Arabic state instead of an unknown badge or failed request.

---

### User Story 3 - Frontend Response Types Match Backend Response DTOs (Priority: P2)

As an admin, assistant, teacher, student, or public visitor using current pages, I need displayed table columns, cards, guards, and dashboards to receive the fields their TypeScript types expect.

**Why this priority**: A response field mismatch usually degrades the UI through undefined values, wrong badges, hidden actions, or runtime rendering errors.

**Independent Test**: Compare TypeScript DTO interfaces in every `frontend/src/services/*-service.ts` file with backend query/controller response shapes used by those routes and record all aligned fields in the contract report.

**Acceptance Scenarios**:

1. **Given** a frontend table expects `createdAt`, `updatedAt`, `status`, and display names, **When** the backend returns the collection, **Then** those fields exist with compatible types.
2. **Given** the frontend consumes wrapped API responses, **When** the service parses the response, **Then** the unwrap logic remains consistent across services and does not assume a different envelope than the backend returns.

---

### User Story 4 - Contract Drift Becomes Detectable Before It Reaches Users (Priority: P2)

As a developer maintaining the project, I need a repeatable contract inventory and regression test so future frontend/backend drift is found during verification, not after deployment.

**Why this priority**: The requested review is project-wide; without automation, the same mismatch can reappear after the next feature.

**Independent Test**: Run one command that regenerates the frontend-driven endpoint inventory and fails if any frontend-called endpoint lacks a backend route.

**Acceptance Scenarios**:

1. **Given** a developer adds a new frontend service method, **When** the endpoint inventory test runs, **Then** the method is included in the report.
2. **Given** a developer changes a backend route without updating the frontend, **When** the inventory test runs, **Then** it fails with the exact frontend service file and backend route gap.

### Edge Cases

- Frontend base URLs may include `/api` while backend controller route attributes also include `/api`; normalization must avoid false positives.
- Dynamic template expressions such as `` `/admin/users/${id}` `` must be normalized to route parameters before comparison.
- Some endpoints require authentication, role permissions, or side effects; the contract audit must not create destructive runtime requests without explicit seed/test setup.
- Optional frontend fields must remain optional in backend DTOs unless the UI enforces a required value before submission.
- Backend-only maintenance/internal endpoints are out of remediation scope unless the current frontend calls them.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin, `/admin`, submit representative forms and load tables for users, content, codes, questions, exams, settings, subjects, teachers, HR, operations, chat, CRM, media, finance, and reports; each request should complete without 404/405/model-binding failures.
- **Manual QA Role/Flow 2**: Assistant, `/assistant/dashboard`, `/assistant/chat`, and `/assistant/crm`; verify task, chat, and CRM calls use existing backend endpoints and display returned fields.
- **Manual QA Role/Flow 3**: Teacher, `/teacher`; verify teacher finance/profile requests match backend route and field contracts.
- **Manual QA Role/Flow 4**: Student/public flows for login/register, dashboard, content, exams, homework, community, public forms, and playback sessions; verify calls remain aligned.
- **Manual QA Negative Check**: Unauthorized or wrong-role users must receive 401/403 responses, not route-not-found errors caused by frontend/backend drift.
- **Docker Acceptance**: `docker compose config -q`, backend build, frontend lint/build/type verification, and project smoke/API tests must complete or any environment blocker must be recorded.
- **External Dependencies**: Real SMS/WhatsApp, payment, Telegram, Google Drive, VK, Gemini, and production storage secrets are not required for static contract verification; runtime checks involving them must use existing test stubs or be documented as unavailable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The audit MUST treat `frontend/src/services` and current frontend API route handlers as the source of truth for required backend routes.
- **FR-002**: Every frontend-called HTTP method/path pair MUST be normalized and matched to an implemented backend controller action.
- **FR-003**: Every frontend-sent payload field found in service methods or direct call-sites MUST be accepted by the corresponding backend request DTO or command.
- **FR-004**: Every frontend-consumed response field declared in TypeScript DTOs MUST be provided by the corresponding backend response DTO or service wrapper.
- **FR-005**: Route parameters and query-string names used by the frontend MUST match backend action parameter names or explicit binding names.
- **FR-006**: Enum request/response values used by frontend screens MUST be normalized safely when the backend can serialize strings or numeric values.
- **FR-007**: The project MUST include a repeatable frontend-driven endpoint inventory report and automated test that fails on route drift.
- **FR-008**: Remediation MUST be minimal and scoped to existing frontend flows; do not invent new backend features for screens the frontend does not currently call.
- **FR-009**: Any contract issue that cannot be fixed safely in this pass MUST be documented with exact file paths, route names, and the reason it remains unresolved.

### Key Entities *(include if feature involves data)*

- **Frontend Endpoint Contract**: A normalized service request containing service file, method, HTTP verb, path template, route parameters, query parameters, and payload field hints.
- **Backend Route Contract**: A normalized controller action containing controller file, HTTP verb, route template, action parameters, request DTO type, and response DTO hint.
- **Contract Finding**: A route, payload, query, enum, or response mismatch with severity, affected flow, source files, fix status, and verification evidence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of discovered frontend service HTTP calls have a matching backend route or a documented intentional frontend-local/proxy exception.
- **SC-002**: Zero newly discovered frontend payload fields remain unsupported by their backend request DTOs after remediation.
- **SC-003**: Zero newly discovered frontend response DTO fields remain unsupported by their backend source after remediation, except documented non-blocking display fallbacks.
- **SC-004**: A repeatable contract inventory command and test are available and pass in local verification.
- **SC-005**: Full frontend and backend verification commands either pass cleanly or record only environment-related blockers with no known unresolved contract drift.

## Assumptions

- Existing frontend service files represent current user-facing and staff-facing API requirements.
- ASP.NET Core JSON options support normal camelCase-to-PascalCase binding, so casing fixes should focus on missing/incorrect fields rather than custom serializers.
- Some existing uncommitted project changes may already implement related backend/frontend features; this work must preserve them and only add scoped remediation.
- Runtime destructive requests should be avoided unless covered by existing seeded test data or test harnesses.
