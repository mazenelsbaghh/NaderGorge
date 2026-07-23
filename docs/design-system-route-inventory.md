# Design System Route Inventory

This inventory covers production UI routes under `frontend/src/app/**` including dynamic and permission-state pages. API routes and non-user-facing internals are excluded. Each migration task must add light/dark evidence for the route group it owns.

| Surface | Source root | Evidence owner | Status |
|---|---|---|---|
| Public | `frontend/src/app`, public route clients | Public wave | Pending |
| Student | `frontend/src/app/student/**` | Student wave | Pending |
| Teacher | `frontend/src/app/teacher/**` | Teacher wave | Pending |
| Assistant | `frontend/src/app/assistant/**` | Assistant wave | Pending |
| Admin | `frontend/src/app/admin/**` | Admin wave | Pending |
| Live support | `frontend/src/components/live-support/**` and support routes | Support wave | Pending |

## Inventory source and coverage rule

The inventory is derived from every `page.tsx`, `layout.tsx`, `loading.tsx`, `error.tsx`, and `not-found.tsx` below `frontend/src/app`, excluding `frontend/src/app/api`. Dynamic segments such as `[id]` and `[packageId]` remain route templates, and role/permission variants are reviewed with their parent route. The current production build enumerates 112 user-facing route templates across public, student, teacher, assistant, admin, and support surfaces; API handlers are excluded.

Each route must be checked in both theme modes and for its available loading, empty, error, disabled, and unauthorized states before Phase 5 is complete.
