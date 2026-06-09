# Feature Specification: Complete Frontend-to-Backend DTO & Field Synchronization Audit

**Feature Branch**: `100-endpoint-alignment-and-sidebar-sync`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "عايزك تعمل مراجعه دقيقه لكل الحقول و كل endpont بتاعتهم من الفرونت للباك و تشوف اي ناقص و تضيفوا و تعمل بيها بالريكوستات بناءآ هلي اللي موجود ف الفرونت فاهمني مش اللي موجود عموما"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Multi-Service Frontend Field & Casing Verification (Priority: P1)

As an administrator or staff member, I want my form submissions and data grids (HR Employees, Payouts, Payrolls, Operations Tasks, CRM leads, and Custom Forms) to work reliably without Kestrel throwing parsing exceptions (such as JSON deserialization errors, casing differences, or type mismatches) when transmitting payloads.

**Why this priority**: High priority as it guarantees zero data transmission bugs and prevents network crash reports.

**Independent Test**: Perform end-to-end payload submissions on the frontend forms and assert that all fields are received by the C# backend and saved without database errors or casing mismatches.

**Acceptance Scenarios**:
1. **Given** any frontend form schema (e.g. `EmployeeProfileDto`, `TeacherPayoutDto`, `TaskItemDto`), **When** the frontend submits the payload, **Then** every field expected by the frontend UI MUST align with the backend's C# DTO properties in type, casing (camelCase to PascalCase), and enum representation.
2. **Given** an API response from a backend controller, **When** parsed in the frontend service, **Then** all properties MUST match the declared TypeScript DTO types, preventing `undefined` field crashes or bad table mappings.

---

### User Story 2 - Comprehensive Endpoint Path Synchronization (Priority: P1)

As a developer, I want to ensure that all API endpoint URL paths in frontend services map directly to active routing endpoints in backend controllers (e.g., matching version prefixes like `/api/v1/` vs `/api/`), preventing `404 Not Found` errors.

**Why this priority**: High priority as path mismatches completely break service functionality.

**Independent Test**: Run a comprehensive network scan of all endpoints defined in the frontend services against the backend controller routes and verify that no route returns a `404 Not Found` or `405 Method Not Allowed` when triggered with correct authorization tokens.

**Acceptance Scenarios**:
1. **Given** any frontend API service call, **When** dispatched, **Then** it MUST target the exact URL route and HTTP method declared in the corresponding C# controller.
2. **Given** route parameter mappings (such as Guid IDs or query parameters), **When** passed, **Then** they MUST be formatted exactly as expected by the backend endpoints (e.g. correct query string names).

---

### Edge Cases

- **Enum Representation Differences**: C# enums can serialize as either integers or strings depending on whether `JsonStringEnumConverter` is applied. The frontend MUST accept both and map them cleanly to Arabic status labels.
- **Handling of Optional/Null Fields**: Fields like rejection reasons, notes, and profile bios can be null. The backend DTOs must allow nullability (e.g., `string?` in C#) for all fields that are optional on the frontend forms.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Verification**: Submit forms across HR (Employee Profile, Attendance clock), Finance (Payout requests, Payout resolutions), Operations Tasks (Comments, Status changes), and CRM (student assignments, call logs) and confirm that all data persists properly in the database.
- **Docker Acceptance**: Verify all Docker containers are running and healthy.
- **Negative Check**: Send empty or malformed requests to endpoints and confirm that Kestrel returns a `400 Bad Request` or validation error, rather than crashing with a `500 Internal Server Error`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The C# backend DTO models for HR, Finance, CRM, and Operations MUST support all fields entered by the frontend forms.
- **FR-002**: Property naming conventions MUST align: PascalCase properties on the C# backend must map to camelCase fields in TypeScript DTOs.
- **FR-003**: Enums returned by the API (e.g. `PayoutStatus`, `PayrollStatus`, `TaskStatus`, `AttendanceStatus`, `VacationStatus`, `CrmStatus`) MUST be parsed safely in the frontend as both strings and numeric IDs.
- **FR-004**: All frontend service endpoints (path prefix, route parameters, query strings) MUST match the actual C# controller routes and HTTP methods.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of frontend service methods map to active, correct backend API routes (zero 404s).
- **SC-002**: Zero serialization errors when sending TSX form values to Kestrel backend controllers.
- **SC-003**: 100% of table rows display correct status labels without showing "unknown" (غير معروفة) badges.

## Assumptions

- We assume the backend uses standard JSON serializer settings (camelCase resolver) which automatically maps PascalCase properties in C# to camelCase fields in TypeScript.
- We assume that the database tables have been updated with migrations to support all fields required by the front-to-back synchronization.
