# Surface Runtime Contract

## Default Surfaces

| Surface | Docker service | Container name | Host port env | Default URL | Entry behavior |
| --- | --- | --- | --- | --- | --- |
| Landing | `landing` | `massar_landing` | `MASSAR_LANDING_PORT` | `http://localhost:8738` | `/` serves landing |
| Student | `student` | `massar_student` | `MASSAR_STUDENT_PORT` | `http://localhost:8739` | `/` rewrites to `/student` |
| Admin | `admin` | `massar_admin` | `MASSAR_ADMIN_PORT` | `http://localhost:8740` | `/` rewrites to `/admin` |
| Backend API | `backend` | `massar_backend` | `MASSAR_BACKEND_PORT` | `http://localhost:5245` | `/api/health` health |

## Required Frontend Environment

| Variable | Purpose |
| --- | --- |
| `APP_SURFACE` | Runtime surface identity: `landing`, `student`, `admin`, or `all` |
| `NEXT_PUBLIC_APP_SURFACE` | Browser-visible surface identity for diagnostics |
| `NEXT_PUBLIC_API_URL` | Browser-reachable backend API URL |
| `NEXT_PUBLIC_BACKEND_URL` | Browser-reachable backend origin for static/media URLs |
| `INTERNAL_API_URL` | Docker-internal backend API URL for server-side Next.js code |
| `INTERNAL_BACKEND_URL` | Docker-internal backend origin for server-side Next.js code |
| `LANDING_PUBLIC_ORIGIN` | Public landing origin for redirects |
| `STUDENT_PUBLIC_ORIGIN` | Public student origin for redirects |
| `ADMIN_PUBLIC_ORIGIN` | Public admin origin for redirects |

## Route Boundary Rules

- `APP_SURFACE=landing`
  - `/` -> landing
  - `/student*` -> redirect to `STUDENT_PUBLIC_ORIGIN`
  - `/admin*` -> redirect to `ADMIN_PUBLIC_ORIGIN`
- `APP_SURFACE=student`
  - `/` -> rewrite `/student`
  - `/admin*` -> redirect to `ADMIN_PUBLIC_ORIGIN`
- `APP_SURFACE=admin`
  - `/` -> rewrite `/admin`
  - `/student*` -> redirect to `STUDENT_PUBLIC_ORIGIN`
- `APP_SURFACE=all`
  - preserve existing behavior for local development and subdomain routing.

## Verification Contract

The verification command must fail if:

- any required service is missing from Docker Compose output
- landing, student, admin, or backend host ports duplicate each other
- application container names do not start with `massar_`
- a required health check is missing
- a frontend surface lacks `APP_SURFACE`
- runtime mode is requested and an endpoint fails to respond
