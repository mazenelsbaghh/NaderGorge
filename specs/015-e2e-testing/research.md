# Research: E2E Testing and Verification

**Date**: 2026-03-27

## Objective
Analyze the requirements from `spec.md` regarding automated End-to-End (E2E) testing for registration, codes, wallet logic, and admin visibility to select appropriate patterns and dependencies.

## Key Findings & Decisions

### Playwright vs Cypress
**Decision**: Playwright
**Rationale**: 
The existing Nader George Academy codebase already has `test-results` and `tests/e2e` structures aligned with Playwright artifacts. Playwright also natively supports parallel testing and multiple browser engines (Webkit, Firefox, Chromium) simultaneously which is critical for simulating varying student devices efficiently < 3 minutes.
**Alternatives considered**: Cypress. Rejected due to slower parallelization and the already present `.last-run.json` hinting at Playwright footprint.

### Test Data Isolation & State Resetting
**Decision**: Pre-Seed via Global Setup & Test API endpoints
**Rationale**:
Running Bulk Code Generation tasks concurrently might spam the `AccessCodes` and `CodeGroups` table. To isolate tests without a complex database clean-up teardown hook that impacts staging, E2E tests will utilize unique randomly prefixed `student_phones` (e.g., `0119999xxxx`) and specific Admin fixture payloads to track ownership easily and tear down via a specialized E2E cleanup routine or just be ignored since `StudentBalances` will naturally be sandboxed by the random user IDs generated inside the spec loops.
**Alternatives considered**: EF Core In-Memory Database (abandoned for true E2E), Mocking API responses (abandoned because it violates E2E true integration tests checking for race conditions).

## Conclusion
The path forward is to implement Playwright tests using `global-setup.ts` to provision Admin accounts and define Test suites (`codes-wallet.spec.ts`, `admin-users.spec.ts`) that test the actual API alongside the browser UI.
