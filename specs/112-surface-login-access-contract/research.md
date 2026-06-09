# Research Notes: Surface Login and Access Contract

## Decisions

### 1. Dynamic Surface Detection in Local Development

- **Decision**: Update `getSurfaceName()` to dynamically detect the surface name from the port number when on localhost/127.0.0.1 and the environment variable is set to `'all'` or is not configured.
- **Rationale**: Enables seamless local testing of multiple surfaces on a single Next.js dev server without having to rebuild the project or change the environment variables.
- **Alternatives Considered**: Running 5 separate Next.js instances locally. Rejected because it is resource-intensive and complicates the development setup.

### 2. Strict Return URL Validation

- **Decision**: Validate the `returnUrl` parameter in both `LoginPage` and `LoginForm` using a utility function.
  - If the `returnUrl` is absolute, its origin must match the active surface's origin.
  - If the `returnUrl` is relative, it must begin with the path prefix of the active surface (e.g. `/student` for student surface, `/teacher` for teacher, etc.).
  - If the validation fails, it must fallback to the default dashboard for the user's role.
- **Rationale**: Prevents open redirect vulnerability and cross-boundary access leakages via return URL tampering.

### 3. Subdomain Isolation via Rewrite to 404

- **Decision**: Replace cross-surface redirects in non-landing surfaces (student, teacher, assistant, admin) with a rewrite to `/not-found` (triggering a custom 404/Forbidden page).
- **Rationale**: Prevents silent redirects that leak information about other surfaces and ensures the user stays strictly inside their domain sandbox. Landing page will remain the only surface that routes users across domains.

### 4. Custom Branded 404 Page

- **Decision**: Create a single responsive `not-found.tsx` page under `frontend/src/app/not-found.tsx` that dynamically adapts its branding (logo, title, text, colors) based on the current surface.
- **Rationale**: Next.js automatically serves `not-found.tsx` for all unhandled routes. By branding it, we can display a clear Arabic message "الصفحة غير موجودة أو لا تخص هذا الحساب" matching the active surface's theme.

### 5. Supervisor Routing Policy

- **Decision**: The `Supervisor` role is treated as an administrative role and uses the `admin` surface. If a user visits `super.massar-academy.net`, they are routed to the `admin` surface, and the origin is preserved as `super.massar-academy.net`.

---

## Technical Mapping Table

| Surface | Port | Subdomain | Role Allowed | Path Prefix | Default Dashboard |
|---|---|---|---|---|---|
| Landing | `8738` | `massar-academy.net` | Any (Visitor) | `/` | `/` |
| Student | `8739` | `app.massar-academy.net` / `student.massar-academy.net` | `Student`, `Admin` | `/student` | `/student` |
| Teacher | `8741` | `teacher.massar-academy.net` | `Teacher`, `Admin` | `/teacher` | `/teacher` |
| Assistant | `8742` | `staff.massar-academy.net` / `assistant.massar-academy.net` | `Assistant`, `Staff`, `Admin`, `Supervisor` | `/assistant` | `/assistant/dashboard` |
| Admin | `8740` | `admin.massar-academy.net` / `super.massar-academy.net` | `Admin`, `Supervisor` | `/admin` | `/admin` |
