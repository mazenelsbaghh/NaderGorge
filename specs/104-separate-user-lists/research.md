# Technical Research: Separate User Lists

## Decision 1: Reuse `GET /api/admin/users` Endpoint with Client-Side Role Filtering

- **Decision**: We will keep the existing backend endpoint and filter the returned list by role in each dedicated frontend page component.
- **Rationale**: The backend `ListUsersQuery` handler already loads user records and includes their roles list. The frontend page components can easily perform `users.filter(u => u.roles.includes('Student'))` (or similar). This prevents having to modify backend C# logic, queries, and controller endpoints, aligning with the DRY and YAGNI principles.
- **Alternatives considered**:
  - *Add a role query parameter to C# backend*: This would require modifying `ListUsersQuery.cs`, `AdminController.cs`, and corresponding handlers. While it limits data payload slightly, the current user dataset is small enough that client-side filtering offers a faster and safer implementation with no risk of backend regressions.

## Decision 2: Keep `/admin/users/[id]` for Student Profiles

- **Decision**: Keep the student profile detail page route at `/admin/users/[id]`, rather than renaming/moving it to `/admin/students/[id]`.
- **Rationale**: Moving the route would break links in other pages (like `/admin/codes/[groupId]` which points to `/admin/users/[id]`). Keeping it at `/admin/users/[id]` preserves link stability, but we will update the sidebar highlighting on this page to mark "الطلاب" (Students) as active, and change the back-button to return to `/admin/students`.
- **Alternatives considered**:
  - *Move to `/admin/students/[id]`*: Rejected because it requires updating all internal links across multiple separate pages, increasing risk of broken links.
