# 10 — System Blueprint (Deployment)

## Services
The production system comprises several distinct, scalable services:
1. **Frontend Server:** Next.js application handling SSR/SSG and client hydration.
2. **Backend API:** The core .NET Web API processing business logic and database commands.
3. **Background Worker:** A separate Node.js process running BullMQ for asynchronous jobs (AI, Email, bulk processing).
4. **Relational Database:** PostgreSQL cluster.
5. **Cache/KV Store:** Redis instance (acting as cache, message broker, and state store).
6. **Reverse Proxy / Edge:** Cloudflare handling DNS, Edge caching, basic WAF, and SSL.
7. **Observability Stack:** OpenTelemetry exporting to a centralized logging/metrics provider (e.g., Datadog, Prometheus/Grafana).

## Environments
1. **Development:** Local environments via Docker Compose holding DB/Redis. Local API and Frontend.
2. **Staging:** A complete replica of production with sanitized data, used for UAT (User Acceptance Testing) and final QA before release.
3. **Production:** Live environment serving actual students.

## Secrets Management
- No secrets (API keys, connection strings, JWT secret keys) shall ever be committed to source control.
- Configuration will use environment variables (`.env` files locally).
- Production secrets will be injected via a secure secrets manager (e.g., AWS Secrets Manager, Azure Key Vault, or GitHub Secrets for CI/CD deployments).

## Performance Targets
The architecture is designed to meet the following SLAs (Service Level Agreements):
- **API Response Time:** < 500ms p95 (excluding complex reporting/AI routes).
- **Video Page Load:** Time-to-interactive < 3 seconds on standard mobile connections.
- **Code Redemption:** Database transaction and UI confirmation < 2 seconds.
- **Uptime:** 99.9% availability during peak exam seasons.
