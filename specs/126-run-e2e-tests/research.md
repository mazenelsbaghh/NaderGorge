# Research & Decisions: E2E Test Execution

## Decision 1: Target Port & Environment
- **Decision**: Run E2E tests against a local Next.js dev server on `http://localhost:3000`.
- **Rationale**: The `frontend/playwright.config.ts` defines `baseURL` as `http://localhost:3000`. Using the local Next.js dev server allows dynamic reloading and aligns with the default test configs.
- **Alternatives considered**: Running tests against the production-built docker containers on ports like 8739/8740. However, this is slower because any hotfix requires rebuilds, and the config uses a single base URL.

## Decision 2: Seeding Endpoint
- **Decision**: Use the backend E2E seeding endpoint at `http://localhost:5245/api/e2e/seed`.
- **Rationale**: The backend docker container is running with `ASPNETCORE_ENVIRONMENT=E2e` and exposes port 5245. The seeding hook in `global-setup.ts` targets this endpoint.
- **Alternatives considered**: Manually running a database script before tests, but that would bypass the backend entity model and logic.
