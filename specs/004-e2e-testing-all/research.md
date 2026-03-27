# Phase 0: Research & Technical Decisions

## Decision 1: Playwright for E2E Frontend Testing
- **Decision**: Use Playwright with TypeScript as the primary E2E testing framework.
- **Rationale**: Playwright was selected by the user. It is modern, fast, supports WebKit/Chromium/Firefox natively, has great tracing tools, and integrates cleanly with Next.js (which we use for the frontend).
- **Alternatives considered**: Cypress (good but struggles with iframes and multi-tab scenarios), Selenium (outdated, slow).

## Decision 2: Backend Test Isolation (appsettings.E2e.json)
- **Decision**: The E2E tests will run against a real, running instance of the backend API, but it must be configured to use an isolated database (`nadergorge_e2e`).
- **Rationale**: True E2E requires a real backend to catch integration bugs. However, to avoid destroying developer or production data, the backend must be launched with an environment variable `ASPNETCORE_ENVIRONMENT=E2e` that uses a separate connection string.
- **Alternatives considered**: Complete API mocking (rejected because it defeats the purpose of an E2E test verifying full system functionality).

## Decision 3: Test Data Seeding and OTP Bypass
- **Decision**: Implement a backdoor API route or environment flag that allows universal OTPs (e.g., "0000") and provides endpoints for bulk seeding packages/lessons exclusively when `ASPNETCORE_ENVIRONMENT=E2e`.
- **Rationale**: To test the Auth and Content Management streams reliably, tests need predictable starting states. Relying on external SMS providers for OTPs during E2E will cause flakiness and cost money. A backdoor in the E2E environment solves this.
- **Alternatives considered**: Injecting data directly via raw SQL in the Playwright `globalSetup` (possible, but tightly couples frontend tests to backend schema changes).

## Decision 4: CI/CD Pipeline Integration
- **Decision**: Playwright tests will be wired into standard npm scripts (`npm run test:e2e`) intended to run in GitHub Actions or locally.
- **Rationale**: The specification requires CI/CD gates. Playwright provides built-in HTML reporters and GitHub Action support to fulfill this requirement easily.
- **Alternatives considered**: None.
