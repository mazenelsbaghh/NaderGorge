# Data Model: Role and Permission System Boundaries

## User Roles (Enum Values / Strings)

- `Admin`: Full bypass of permission evaluations. Has access to all administration features.
- `Supervisor`: A specialized staff role. Checked explicitly for permissions (e.g., `users.manage`, `content.manage`, `reports.view`).
- `Teacher`: Academic content creator. Bound to specific courses and packages. Does not bypass permissions.
- `Assistant`: Operates under limited permissions assigned to CRM, Operations, or Academic review.
- `Staff`: General operational user with assigned tasks and HR functions.
- `Student`: Learner role. Restricted to the student surface only.

## User Token Claims / DTO Properties

```json
{
  "id": "guid",
  "fullName": "string",
  "phone": "string",
  "roles": ["Admin" | "Supervisor" | "Teacher" | "Assistant" | "Staff" | "Student"],
  "permissions": ["string"],
  "profileComplete": "boolean",
  "avatarSlug": "string | null"
}
```

## Backend Attribute Authorization

`[HasPermission("permission_name")]` evaluates:
1. Is the current request authenticated? (If not, returns `401 Unauthorized`).
2. Does the user possess the `Admin` role? (If yes, bypasses authorization check).
3. If not an Admin, does the user have a claim of type `permission` with value matching `"permission_name"` (case-insensitive)? (If not, returns `403 Forbidden`).
