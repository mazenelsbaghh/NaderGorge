# Contract: Infrastructure Security

## Docker Compose

**Root `docker-compose.yml` requirements**:

- Worker healthcheck uses `curl -f http://localhost:3001/ready`.
- Worker container does not run as root.
- Production-like worker port publishing is disabled, profile-gated, or explicitly local-only with documentation that it must not be enabled in production.
- Redis has authentication (`requirepass` or equivalent), append-only persistence, and bounded maxmemory policy in production-like compose.
- Redis healthcheck authenticates when a password is configured.
- Backend and worker Redis connection strings include the configured password.

**Auxiliary compose requirements**:

- `docker/docker-compose.yml` and `docker/docker-compose.infra-only.yml` get equivalent Redis auth/persistence hardening or are clearly labeled local-only.

## Nginx

**Protected assets**:

- `/secured-assets/` remains `internal`.
- Wildcard CORS is not allowed for protected assets.
- Allowed origins are Massar frontend origins only.
- `Content-Disposition: attachment` from backend is preserved.

**Public assets**:

- `assets.massar-academy.net` must not expose protected resource roots.
- Public caching applies only to intentional public assets.

**TLS**:

- If Nginx is direct production ingress, checked-in config must include 443 listener and TLS directives through environment-mounted cert paths.
- If TLS terminates before Nginx, documentation must explicitly require external TLS, `X-Forwarded-Proto`, and secure headers/HSTS.

## Container Smoke

- `docker compose config -q` passes.
- Worker image defines a non-root user.
- Static checks can verify healthcheck, Redis command, port exposure, and Nginx protected CORS without launching external services.
