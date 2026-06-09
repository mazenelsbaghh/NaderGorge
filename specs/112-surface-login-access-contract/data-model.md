# Data Model: Surface Login and Access Contract

This feature does not introduce any new database tables, columns, or migrations. It relies entirely on the existing user and session entities.

## Existing Entities Utilized

### 1. `User` (and Roles)
- Attributes checked:
  - `user.roles` (Student, Teacher, Assistant, Staff, Admin, Supervisor)

### 2. Session Configuration (Client-side)
- `useAuthStore` stores the active session, including user roles and token.
- Cookie-based authentication details are handled by the browser and API Client.
