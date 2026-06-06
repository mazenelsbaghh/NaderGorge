# Quickstart: Surface Docker Separation

## Configure

Copy the environment template:

```bash
cp .env.example .env
```

Default local URLs:

```text
Landing: http://localhost:8738
Student: http://localhost:8739
Admin:   http://localhost:8740
Backend: http://localhost:5245/api/health
Worker:  http://localhost:3001/health
```

## Start

```bash
make up
```

## Verify Static Docker Separation

```bash
make verify-surfaces-static
```

## Verify Running Surfaces

```bash
make verify-surfaces
```

## Inspect Logs

```bash
make logs-landing
make logs-student
make logs-admin
make logs-backend
```

## Rebuild One Surface Image

All frontend surfaces share the same image, so rebuilding any frontend surface rebuilds the shared frontend artifact:

```bash
make build-frontend
```

## Expected Outcomes

- `docker compose ps` shows `masar_landing`, `masar_student`, `masar_admin`, `masar_backend`, `masar_worker`, `masar_db`, and `masar_redis`.
- Landing, student, and admin surfaces are available on distinct ports.
- Browser API requests use `http://localhost:5245/api` by default.
- Server-side Next.js calls use `http://backend:5245/api` inside Docker.

## Troubleshooting

- **Port already in use**: change `MASAR_LANDING_PORT`, `MASAR_STUDENT_PORT`, `MASAR_ADMIN_PORT`, or `MASAR_BACKEND_PORT` in `.env`, then run `docker compose config --format json` to confirm the rendered ports.
- **Static verification fails on secrets**: set `JWT_SECRET`, `API_CALLBACK_SECRET`, `AI_CALLBACK_SECRET`, `PARENT_REPORT_SIGNING_SECRET`, and `WORKER_ADMIN_TOKEN` in `.env`.
- **Browser cannot reach API**: confirm `NEXT_PUBLIC_API_URL` is a browser URL such as `http://localhost:5245/api`, not `http://backend:5245/api`.
- **Server-side Next.js cannot reach API**: confirm `INTERNAL_API_URL` is `http://backend:5245/api` inside Docker.
- **Old route opens on wrong surface**: run `make verify-surfaces` after the stack is up to confirm landing redirects `/student` and `/admin` to the dedicated origins.
