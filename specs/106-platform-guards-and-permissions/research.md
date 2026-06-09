# Research Notes: Platform Guards and Permissions Routing

## Decisions

### 1. Unified Route Boundaries in `proxy.ts`

- **Decision**: Keep using Next.js 16 `proxy.ts` (instead of the deprecated `middleware.ts` convention) to intercept incoming requests and redirect them across surfaces (landing, student, admin, teacher, assistant).
- **Rationale**: Next.js 16 supports `proxy.ts` at the root/src level for standalone deployments. This file handles all subdomain-level and path-level rewrites or redirects before the layout layers compile.
- **Alternatives Considered**: Direct middleware logic in every layout. Rejected because it leads to duplicate code, hard-to-maintain redirection loops, and makes surface-level sandbox isolation harder.

### 2. Five Independent Frontend Surfaces

- **Decision**: Expand the current 3-surface Docker model to 5 surfaces: `landing`, `student`, `admin`, `teacher`, and `assistant`. Each surface runs as an independent container instance using the same standalone production Docker image, but configured via unique environment variables (`APP_SURFACE` / `NEXT_PUBLIC_APP_SURFACE`).
- **Rationale**: Isolates the runtime memory and process space of each domain. If one frontend surface goes down or experiences heavy load, the other surfaces remain unaffected.
- **Alternatives Considered**: A single frontend container handling all traffic. Rejected because it violates the isolation requirements of the platform expansion plan.

### 3. Permission Evaluation Restructure

- **Decision**: Update `useHasPermission` (frontend) and `HasPermissionAttribute` (backend) to restrict automatic bypass only to users with the "Admin" role. All other roles, including "Supervisor" and "Teacher", must be checked explicitly against permissions.
- **Rationale**: Supervisor, Assistant, and Teacher are operational or academic roles. Automatic bypass for these roles would result in privilege leaks. Only "Admin" is a true superuser.

---

## Port and Domain Mapping Table

| Surface | Local Port | Subdomains (Production) | Surface Name | Target Container |
|---|---|---|---|---|
| Landing | `8738` | `massar-academy.net`, `www.massar-academy.net` | `landing` | `massar_landing` |
| Student | `8739` | `student.massar-academy.net`, `app.massar-academy.net` | `student` | `massar_student` |
| Admin | `8740` | `admin.massar-academy.net`, `super.massar-academy.net` | `admin` | `massar_admin` |
| Teacher | `8741` | `teacher.massar-academy.net` | `teacher` | `massar_teacher` |
| Assistant | `8742` | `staff.massar-academy.net` | `assistant` | `massar_assistant` |
