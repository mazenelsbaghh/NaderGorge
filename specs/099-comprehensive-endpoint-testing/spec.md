# Feature Specification: Comprehensive Endpoint Testing

**Feature Branch**: `099-comprehensive-endpoint-testing`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Test every endpoint with real requests, see what it does, check for problems, create account and login and test everything"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Programmatic Route Scan & Execution (Priority: P1)

As an operator, I want to execute a Python test harness that dynamically parses `tests/endpoint_inventory.json` and issues HTTP requests to all registered API routes on the running Docker stack, so that I can detect any unhandled 500 errors or logic crashes.

**Why this priority**: Crucial for detecting hidden code crashes, routing conflicts, or deserialization failures before launch.

**Independent Test**: Can be validated by executing the python script and reviewing the generated log summary showing the status codes of all requests.

**Acceptance Scenarios**:
1. **Given** a running Docker container stack, **When** running the python test harness, **Then** the script must successfully scan all routes and send requests to them.
2. **Given** any REST API endpoint, **When** hit with an invalid GUID parameter, **Then** it must return a structured 404 or 400 response and MUST NOT throw a 500 Internal Server Error.

---

### User Story 2 - Multirole Authentication Boundaries (Priority: P1)

As a security auditor, I want to test all endpoints using the credentials of Admin, Assistant, Teacher, and Student roles, so that I can verify that authentication rules and role boundaries (e.g. 403 Forbidden for unauthorized actions) are strictly enforced at the HTTP layer.

**Why this priority**: Highly critical to verify that the API authorization layer blocks unauthorized access between roles.

**Independent Test**: Validated by asserting that students/assistants trying to call administrative routes receive a 403 status code.

**Acceptance Scenarios**:
1. **Given** an authenticated Student session, **When** calling an Admin controller endpoint, **Then** the server must respond with 403 Forbidden.
2. **Given** an authenticated Assistant session, **When** calling a Teacher finance summary endpoint, **Then** the server must respond with 403 Forbidden.

---

### Edge Cases

- **Wildcard parameters in paths**: Path patterns like `{id:guid}` or `{commentId:guid}` must be dynamically replaced with test GUIDs to ensure the routing engine matches the path correctly.
- **Empty payload handling**: POST/PUT requests with empty or partial payloads must be caught gracefully by model validation rather than crashing Kestrel.

### Manual QA & Docker Acceptance *(mandatory)*

- **Docker Acceptance**: `docker compose ps` shows all 8 containers healthy.
- **Verification Command**: `python3 scratch/test_all_endpoints.py` must run and output a full CSV or Markdown audit report.
- **Negative Check**: Assert that 100% of unauthorized role queries return `403 Forbidden` or `401 Unauthorized`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Harness MUST read endpoints directly from `tests/endpoint_inventory.json`.
- **FR-002**: Harness MUST authenticate as Admin (`20000000000`), Student (`20000000001`), Assistant (`20000000003`), and Teacher (`20000000004`) using the password `password`.
- **FR-003**: Harness MUST substitute route parameters (e.g., `{id:guid}`) with a default test GUID `00000000-0000-0000-0000-000000000000`.
- **FR-004**: Harness MUST run all requests against `http://localhost:5245`.
- **FR-005**: Harness MUST distinguish between expected errors (400, 401, 403, 404) and unexpected errors (500).
- **FR-006**: Harness MUST generate a comprehensive Markdown report detailing the outcome of every endpoint.
