# Implementation Plan: Domain and Docker Isolation Finalization

**Branch**: `109-domain-and-docker-isolation` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)

## Goal Description
Clean up legacy domains (`massarplatform.com`, `bsma-academy.com`) from active routing configurations and backend CORS settings. Ensure that the active domain `massar-academy.net` (and `localhost` for development) is the sole active host. Set up legacy redirects inside the Nginx container, add HTML markers to denote the active surface in the DOM, and enhance `scripts/verify-surface-separation.mjs` to validate cross-boundary redirects and header/HTML markers.

---

## User Review Required

> [!NOTE]
> The single image frontend build (`massar_frontend:local`) is kept as a conscious engineering choice to prevent slow, duplicated Next.js compilations on VM environments suffering from high steal times. The runtime surface isolation is fully enforced through environment variables, separated ports, proxy middleware, and Nginx routing.

---

## Open Questions
None.

---

## Proposed Changes

### Configuration & Nginx

#### [MODIFY] [docker-compose.yml](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/docker-compose.yml)
* Update default `Cors__AllowedOrigins` values under `backend` to only contain localhost origins and `massar-academy.net` subdomains. Clean up all `massarplatform.com` and `bsma-academy.com` domains from the default CORS list.
* Update default `NEXT_PUBLIC_APP_DOMAIN` under `x-frontend-environment` to `massar-academy.net`.

#### [MODIFY] [massar.conf](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/docker/nginx/massar.conf)
* Remove legacy domains (`massarplatform.com`, `bsma-academy.com` and all subdomains) from the active server blocks. Keep only `massar-academy.net`, its subdomains, and `localhost` (where appropriate).
* Add catch-all legacy redirect blocks to issue a `301 Moved Permanently` status from legacy domains/subdomains to their equivalents on `massar-academy.net`. For example:
  * Redirect `massarplatform.com`/`bsma-academy.com` to `https://massar-academy.net$request_uri`.
  * Redirect `*.massarplatform.com`/`*.bsma-academy.com` to `https://$1.massar-academy.net$request_uri` (by capturing the subdomain prefix).

#### [MODIFY] [.env.example](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/.env.example)
* Update `NEXT_PUBLIC_APP_DOMAIN=massarplatform.com` to `NEXT_PUBLIC_APP_DOMAIN=massar-academy.net`.

### Frontend Surface Runtime

#### [MODIFY] [config.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/packages/surface-runtime/config.ts)
* Update the default fallback value of `mainDomain` from `'massarplatform.com'` to `'massar-academy.net'`.

#### [MODIFY] [layout.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/layout.tsx)
* Import `getSurfaceName` and set `data-massar-surface={getSurfaceName()}` on the `<html>` element.

### Verification Tools

#### [MODIFY] [verify-surface-separation.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/verify-surface-separation.mjs)
* Update the static Nginx check to assert that legacy domains do not appear in the active server blocks of `massar.conf`.
* Add assertion scenarios validating:
  * Redirects when hitting forbidden routes across all frontend containers (e.g., student container hitting `/admin`, admin container hitting `/student`, etc.).
  * Response HTTP headers verify `x-massar-surface` matches the active surface.
  * Response HTML bodies contain the `data-massar-surface` attribute matching the expected surface name.

---

## Verification Plan

### Automated Tests
* Run static checks of the verification script:
  `node scripts/verify-surface-separation.mjs --static-only`
* On local setup or production, run full verification checks including runtime HTTP assertions.

### Manual Verification
* Inspect the page source code (DOM) on any loaded surface to verify the presence of `data-massar-surface` on the `<html>` tag.
* Attempt to access a legacy domain URL (e.g., `student.massarplatform.com`) and confirm it issues a `301` redirect to `app.massar-academy.net`.
