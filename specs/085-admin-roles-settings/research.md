# Research Notes: Technical Design Decisions

## 1. Dynamic Roles and Permissions Architecture

### Options Considered
1. **Classic Role-Based Access Control (RBAC)**: Hardcoded roles (Admin, Teacher, Assistant, Student) checked using `[Authorize(Roles = "Admin")]`.
   - *Verdict*: Too restrictive. The user explicitly requested custom roles (e.g., creating specific assistant roles with different rights).
2. **Permissions Junction Table (`RolePermissions`)**: A separate table linking `Roles` and a new `Permissions` table.
   - *Verdict*: Over-engineered for a platform of this scale, adding unnecessary joins on simple user-access queries.
3. **JSON-Serialized Permissions Column in Roles Table (`PermissionsJson`)**: Add a text/json column directly to the existing `roles` table.
   - *Verdict*: **Selected**. Highly efficient, keeps the schema simple, requires no extra tables, and maps perfectly to a string list (`List<string>`) in Entity Framework Core.

## 2. Authorization Enforcement in Backend

### Options Considered
1. **Dynamic Database Lookup per Request**: A custom policy or filter queries the database to see if the user's role has the required permission.
   - *Verdict*: Inefficient. Adds a DB trip to every state-changing request.
2. **JWT Claims-Based Authorization**: During login or token refresh, the `TokenService` retrieves the user's assigned role and its permissions, injecting them as multiple `permission` claims in the JWT token.
   - *Verdict*: **Selected**. This leverages JWT standard token payload, eliminates DB queries during authorization checks, and works seamlessly with standard ASP.NET Core authorization filters.

## 3. Maintenance Mode Mechanics

### Options Considered
1. **Backend Middleware Blocking All Requests**: Returns 503 Service Unavailable for all non-admin API requests.
   - *Verdict*: A bit too harsh; it breaks the frontend UI completely if not handled gracefully.
2. **Frontend Layout Guard**: The `/student` layout runs a lightweight public check. If `MaintenanceMode` is true, it displays a premium full-screen banner instead of rendering the child pages.
   - *Verdict*: **Selected**. Delivers a beautiful, polished UX. Admins can still log in and view the admin dashboard to turn maintenance off, while students see a professional "سنعود قريباً" maintenance screen.
