# Surface Runtime and Routing Contract

## Domain Routing Matrix

Next.js redirects and Nginx proxy rules MUST route incoming domains to internal services as follows:

| Inbound Domain / Port | Target Frontend Service | Port (Internal) | Surface Identifier (`APP_SURFACE`) |
|---|---|---|---|
| `localhost:8738` / `massaracademy.com` | `landing` | `8738` | `landing` |
| `localhost:8739` / `app.massaracademy.com` | `student` | `8738` | `student` |
| `localhost:8740` / `admin.massaracademy.com` | `admin` | `8738` | `admin` |
| `localhost:8741` / `teacher.massaracademy.com` | `teacher` | `8738` | `teacher` |
| `localhost:8742` / `staff.massaracademy.com` | `assistant` | `8738` | `assistant` |

## Frontend Proxy Rewrites (`proxy.ts`)

- Every request to a surface is evaluated via the `proxy` function in `proxy.ts`.
- Subdomain detections translate to corresponding `APP_SURFACE` properties.
- If a pathname doesn't match the designated surface:
  - If target surface is `landing` and path is `/student/*` -> redirect to `student` origin.
  - If target surface is `student` and path is `/admin/*` -> redirect to `admin` origin.
  - If target surface is `student` and path is `/teacher/*` -> redirect to `teacher` origin.
  - If target surface is `student` and path is `/assistant/*` -> redirect to `assistant` origin.
  - And so on for all non-aligned path/surface combinations.
- A custom response header `x-massar-surface` must be appended to the response to identify the surface that served the request.
