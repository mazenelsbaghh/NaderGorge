# UI Routing Contracts

## Route Overviews

| Route | Displays | Navigation Sidebar Label | Permissions Needed | Actions |
|---|---|---|---|---|
| `/admin/students` | Students list with full filters | الطلاب | `users.manage` | Add Student, view student profile, suspend student, export |
| `/admin/assistants` | Assistants list | المساعدين | `users.manage` | Add Assistant, view assistant info, suspend assistant, edit roles |
| `/admin/admins` | Administrators list | المديرين | `users.manage` | Add Admin, view admin details, suspend/activate admin |
| `/admin/users` | Redirects to `/admin/students` | — | `users.manage` | Redirect |
| `/admin/users/[id]` | Student profile details | (Keeps الطلاب active) | `users.manage` | Update profile, override views, modify balance, reset password |
