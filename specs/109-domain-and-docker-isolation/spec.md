# Feature Specification: Domain and Docker Isolation Finalization

**Feature Branch**: `109-domain-and-docker-isolation`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: platform_expansion_gap_report_2026-06-09.md (Phase 1)

## Summary

Finalize the domain migration and container boundary isolation for the Massar Platform. Specifically, this feature addresses cleaning up legacy domains (`massarplatform.com`, `bsma-academy.com`) from configuration templates and reverse proxies, formally documenting the frontend container runtime isolation strategy, adding HTML/header markers to verify the active surface, and expanding the automated verification script to validate cross-boundary forbidden routes.

---

## Technical Decisions & Architecture

### 1. Frontend Runtime Isolation vs. Image Separation
* **Decision**: Maintain a single Docker image `massar_frontend:local` for all 5 frontend surfaces, but enforce strict runtime isolation via separate container instances, dedicated host ports, and individual environment configurations.
* **Rationale**: Next.js standalone builds are highly resource-intensive and slow (5-10 minutes per build). Building five separate images would result in a total build time of 25-50 minutes on the deployment VM due to virtualization CPU steal. Because the underlying codebase is shared, using a single image configured via container environment variables (`APP_SURFACE`, `NEXT_PUBLIC_APP_SURFACE`) delivers identical isolation properties, while reducing build times to under 10 minutes.
* **Enforcement**:
  * Each container runs on a distinct host port (Landing: 8738, Student: 8739, Admin: 8740, Teacher: 8741, Assistant: 8742).
  * Next.js proxy middleware intercepts request routes and forces redirects/rewrites if a container receives a request outside its surface boundary.
  * Subdomain host header routing is terminated at the Nginx layer.

### 2. Domain Consolidation
* **Active Domain**: `massar-academy.net`
* **Legacy Domains to Clean/Redirect**: `massarplatform.com`, `bsma-academy.com`
* **Nginx Changes**:
  * Clean `docker/nginx/massar.conf` of legacy domains in primary server blocks.
  * Add unified catch-all permanent (301) redirect blocks to route any request from legacy domains (`*.massarplatform.com`, `*.bsma-academy.com`) to the corresponding subdomain on the active `massar-academy.net` domain.
* **CORS Config**:
  * Remove all legacy domain origins from the default `Cors__AllowedOrigins` environment variable values in `docker-compose.yml`, `.env`, and `.env.example`.

---

## User Scenarios & Testing

### User Story 1 - Active Subdomain Reverse Proxying (Priority: P1)
As a platform operator, I want Nginx to route only active subdomains (on `massar-academy.net` or `localhost`) to the corresponding container ports, while immediately redirecting legacy domain requests to the new domain, so that our production traffic remains secure and consolidated.

* **Acceptance Scenarios**:
  1. **Given** a request to `app.massar-academy.net`, **When** evaluated by Nginx, **Then** it must proxy pass to `http://student:8738`.
  2. **Given** a request to `student.massarplatform.com`, **When** evaluated by Nginx, **Then** it must issue a `301 Permanent Redirect` to `https://app.massar-academy.net/`.
  3. **Given** a request to `admin.bsma-academy.com/admin`, **When** evaluated by Nginx, **Then** it must issue a `301 Permanent Redirect` to `https://admin.massar-academy.net/admin`.

---

### User Story 2 - Cross-Surface Forbidden Route Enforcement (Priority: P1)
As a security auditor, I want any direct request to an administrative path (like `/admin` or `/assistant`) sent to the student container to be rejected or redirected, and vice versa, to ensure runtime container boundaries cannot be bypassed.

* **Acceptance Scenarios**:
  1. **Given** the student container running on port `8739`, **When** a user accesses `http://localhost:8739/admin`, **Then** the request must be redirected to the admin surface origin (`http://localhost:8740/admin`).
  2. **Given** the admin container running on port `8740`, **When** a user accesses `http://localhost:8740/student`, **Then** the request must be redirected to the student surface origin (`http://localhost:8739/student`).
  3. **Given** the assistant container running on port `8742`, **When** a user accesses `http://localhost:8742/teacher`, **Then** the request must be redirected to the teacher surface origin (`http://localhost:8741/teacher`).

---

### User Story 3 - Surface Markers Verification (Priority: P2)
As a front-end developer, I want the active surface identity to be embedded in both HTTP headers and HTML markup on every page load, so that client-side scripts and integration test suites can verify surface alignment.

* **Acceptance Scenarios**:
  1. **Given** a page response from the Student container, **When** headers are inspected, **Then** `x-massar-surface` must equal `student`.
  2. **Given** a page response from the Student container, **When** the HTML root is inspected, **Then** the `<html>` element must have a `data-massar-surface="student"` attribute.
  3. **Given** a page response from the Admin container, **When** headers are inspected, **Then** `x-massar-surface` must equal `admin`, and the HTML root must have `data-massar-surface="admin"`.

---

## Requirements

### Functional Requirements
* **FR-001**: Set the `data-massar-surface` attribute on the `<html>` tag in `frontend/src/app/layout.tsx` by querying `getSurfaceName()`.
* **FR-002**: Consolidate `docker/nginx/massar.conf` to map only `massar-academy.net` subdomains to docker services.
* **FR-003**: Create redirect rules in `docker/nginx/massar.conf` that map legacy hostnames (`*.massarplatform.com` and `*.bsma-academy.com`) to the corresponding `*.massar-academy.net` hostnames with `301` status.
* **FR-004**: Clean up the `Cors__AllowedOrigins` default list in `docker-compose.yml` and `.env.example` by removing references to legacy domains.
* **FR-005**: Add assertions in `scripts/verify-surface-separation.mjs` to check:
  * Redirects when hitting forbidden routes across all frontend containers (e.g., student hitting `/admin` redirects to admin surface, etc.).
  * Response headers verify `x-massar-surface` equals the active surface.
  * Response HTML bodies contain `data-massar-surface` matching the expected surface.
  * Static file checks in Nginx configuration verify no raw legacy domains exist inside the primary server block names.

### Validation Checklist
- [ ] Run `docker compose config -q` successfully.
- [ ] Verify `verify-surface-separation.mjs` passes locally using `--static-only`.
- [ ] Verify headers and HTML data attributes for each surface.
