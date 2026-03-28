# Research: Admin Student Profile

## Context
The goal is to provide a comprehensive Student Details Profile for administrators, combining various domain entities into a single 360-degree view. It must align with the "Editorial Scholar" design language and utilize the recently completed `AdminShellChrome`, `AdminStatCard`, `AdminDataTable`, `AdminTabBar`, and `AdminModal` components.

## Technical Decisions

### D1: Frontend Navigation and Routing
- **Decision**: Implement the profile as a nested Next.js App Router page at `/admin/users/[id]`.
- **Rationale**: A modal would be too constrained for a comprehensive 360-degree view containing multiple tabs and tables (watch history, devices, payments, etc.). A dedicated route maintains browser history, allows deep-linking to specific profiles, and provides ample screen real-estate.
- **Alternatives Considered**: A side-panel or large modal (rejected due to space limitations and lack of direct URL linking).

### D2: Backend Data Aggregation Strategy
- **Decision**: Aggregate the data via a single Facade endpoint `GET /api/admin/users/{userId}/profile` that returns a nested DTO (`StudentProfileExtendedDto`), supplemented by specific endpoints for discrete actions.
- **Rationale**: Avoids the "N+1 request" problem on the frontend and ensures the profile loads swiftly in one network call, fulfilling the SC-004 (<1.5s load time).
- **Alternatives Considered**: Fetching each segment (devices, payments, packages) via separate HTTP calls in parallel (rejected to avoid overwhelming the client with complex Promise.all logic and multiple loading states).

### D3: Profile Layout and Navigation
- **Decision**: Use `AdminTabBar` for intra-profile navigation, separating the view into logical tabs: `[ 'نظرة عامة', 'الأكاديمية', 'الأجهزة', 'التجاوزات', 'الماليات', 'السجل' ]`.
- **Rationale**: Follows the `AdminTabBar` pattern established in `010-admin-shared-components`. Reduces cognitive load by revealing only relevant info at a time.
- **Alternatives Considered**: A single massive scrolling page (rejected because it conflicts with the "Editorial Scholar" clean aesthetics and creates overwhelming noise).

### D4: Action Execution / Audit Logs
- **Decision**: All administrative actions (granting views, adding points, disconnecting devices) will POST to specific command endpoints. The backend MediatR pipeline will emit a `DomainEvent` caught by an `AuditLogger` to record the change in a new `AuditLogs` table.
- **Rationale**: Ensures SC-003 is met reliably. MediatR behaviors are the standard pattern in ASP.NET Core for cross-cutting concerns like event logging.
- **Alternatives Considered**: Manually inserting audit logs in every command handler (rejected due to code duplication and risk of omission).

## Design System Compliance
- Will exclusively use `var(--admin-*)` CSS tokens.
- Will heavily rely on `AdminStatCard` for the "Summary" tab.
- Tables inside tabs (e.g., Device History, Watch History) will use `AdminDataTable`.
