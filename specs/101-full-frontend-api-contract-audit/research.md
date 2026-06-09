# Research: Full Frontend API Contract Audit

## Decision: Frontend service calls are the source of truth

**Rationale**: The requester explicitly asked to fix contracts based on what currently exists in the frontend, not based on every backend endpoint that exists generally. This avoids adding unused backend work and focuses on user-visible breakage.

**Alternatives considered**:

- Backend-first OpenAPI generation: rejected because it can miss frontend calls that drifted away from the backend route.
- Runtime crawling every endpoint: rejected because many endpoints need authentication, roles, seed data, or have side effects.

## Decision: Extend the existing endpoint inventory script

**Rationale**: `scripts/generate-endpoint-inventory.mjs` and `tests/test_endpoint_inventory.py` already exist and are wired into the Python test surface. Extending them keeps the audit repeatable and discoverable.

**Alternatives considered**:

- New contract-audit script: rejected because it would duplicate backend route parsing and create two reports.
- Manual spreadsheet/report only: rejected because it would not prevent future drift.

## Decision: Normalize dynamic route templates for comparison

**Rationale**: Frontend calls use template literals such as `` `/admin/users/${id}/devices` `` while ASP.NET routes use templates such as `api/admin/users/{id}/devices`. Comparison must treat both as the same route while still preserving the source expression for debugging.

**Alternatives considered**:

- String exact matching only: rejected because it would produce false positives for every route parameter.
- Full AST evaluation: rejected for this pass because service call patterns are simple enough for targeted static extraction.

## Decision: Classify frontend-local and external calls separately

**Rationale**: Calls like `/api/worker/...` hit Next.js API routes, not the ASP.NET backend controller set. Worker and public landing fetches still belong in the inventory but must not be counted as missing backend routes unless they target the backend API base.

**Alternatives considered**:

- Exclude non-`apiClient` calls entirely: rejected because frontend API handlers also depend on backend routes such as `/auth/me` and video embed material.

## Decision: Keep UI changes minimal

**Rationale**: Impeccable and UI-UX Pro Max guidance for this task means preserving product UI consistency and accessibility if any touched contract fix reaches UI code. The main user need is correctness, so decorative redesign is explicitly out of scope.

**Alternatives considered**:

- Broad UI redesign while touching services: rejected because it increases risk and does not serve the contract-remediation goal.
