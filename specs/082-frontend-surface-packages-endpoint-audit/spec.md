# Feature Specification: Frontend Surface Packages, Register Branding, and Endpoint Audit

**Feature Branch**: `082-frontend-surface-packages-endpoint-audit`  
**Created**: 2026-06-06  
**Status**: Draft  
**Input**: User description: "Separate the frontend folder between landing, admin, and student; provide one place to change the register logo and replace certain icons with the logo; perform a deep search and build complete tests for every endpoint in the project."

## User Scenarios & Testing

### User Story 1 - Frontend Code Is Split by Surface (Priority: P1)

A developer needs a clear frontend package boundary for landing, admin, and student work so each surface can evolve without hunting through unrelated files.

**Independent Test**: Inspect `frontend/src/packages/landing`, `frontend/src/packages/admin`, and `frontend/src/packages/student`; each package exposes surface-specific entry points used by the matching App Router pages or layouts.

**Acceptance Scenarios**:

1. **Given** a developer opens the frontend source, **When** they look for landing-specific entry points, **Then** they find them under `frontend/src/packages/landing`.
2. **Given** a developer opens admin navigation or admin root page configuration, **When** they trace imports, **Then** the source comes from `frontend/src/packages/admin`.
3. **Given** a developer opens the student dashboard page, **When** they trace dashboard components, **Then** the imports pass through `frontend/src/packages/student`.

### User Story 2 - Register Branding Uses One Logo Source (Priority: P1)

A maintainer needs one shared place to change the Massar logo used by registration and small navigation brand marks, replacing older standalone icon glyphs.

**Independent Test**: Change the logo source in the shared logo component and verify the register page and public login navigation both render the new logo asset.

**Acceptance Scenarios**:

1. **Given** the register page renders, **When** the top brand mark appears, **Then** it uses the shared Massar logo component rather than the old eye glyph.
2. **Given** the public login button renders, **When** the decorative brand mark appears, **Then** it uses the shared logo component rather than the Sphinx icon component.
3. **Given** the shared logo source is changed, **When** dependent surfaces rebuild, **Then** all updated logo instances use that new source.

### User Story 3 - Every Backend Endpoint Is Inventoried and Tested (Priority: P1)

A developer needs a complete, repeatable endpoint audit that finds every controller endpoint and fails when the inventory is stale.

**Independent Test**: Run the endpoint inventory generator and the Pytest endpoint inventory test; both must enumerate all controller endpoints and fail if a controller route changes without updating the inventory.

**Acceptance Scenarios**:

1. **Given** a controller endpoint exists, **When** the inventory is generated, **Then** the endpoint appears with controller, method, path, authorization classification, and source line.
2. **Given** an endpoint is added, removed, or changed, **When** the endpoint inventory Pytest runs before the inventory is refreshed, **Then** the test fails and shows the inventory mismatch.
3. **Given** the generated Markdown is opened, **When** a developer reviews it, **Then** they can see all project endpoints grouped by controller.

## Requirements

- **FR-001**: The frontend MUST expose landing, admin, and student package directories under `frontend/src/packages`.
- **FR-002**: App Router entry files MUST consume at least one matching package entry point for each surface.
- **FR-003**: Shared brand logo rendering MUST live in one reusable component.
- **FR-004**: The register page MUST render the shared logo component instead of a hardcoded glyph.
- **FR-005**: Public navigation login branding MUST render the shared logo component instead of the previous Sphinx icon.
- **FR-006**: The endpoint audit MUST parse all `*Controller.cs` files under `backend/src/NaderGorge.API/Controllers`.
- **FR-007**: The generated endpoint inventory MUST include HTTP method, full normalized path, controller name, action name, authorization classification, and source file/line.
- **FR-008**: A Pytest test MUST compare the committed endpoint inventory with the current controllers.
- **FR-009**: A Markdown report MUST list all endpoints grouped by controller for human review.
- **FR-010**: Existing runtime Docker surface separation MUST remain compatible.

## Success Criteria

- **SC-001**: `npm run lint` in `frontend/` completes without new lint failures caused by this feature.
- **SC-002**: `pytest tests/test_endpoint_inventory.py` passes and validates every discovered controller endpoint against the committed inventory.
- **SC-003**: The endpoint inventory includes every current controller endpoint discovered during the deep scan.
- **SC-004**: Registration and public nav brand marks use the shared Massar logo component.

## Assumptions

- Physical movement of every existing component is intentionally incremental to avoid breaking a dirty worktree; package entry points create the stable boundaries first.
- Runtime Docker separation from feature `081` remains in place and is not reworked here.
- Endpoint behavioral tests remain in existing domain-specific test files; this feature adds complete endpoint discovery and stale-inventory protection for every endpoint.
