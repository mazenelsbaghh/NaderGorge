# Quickstart: Playwright E2E 

This guide outlines how to execute the Playwright End-to-End tests against the Nader Gorge platform.

## Pre-requisites
1. Node.js environment installed.
2. Ensure you have the `backend` running with `ASPNETCORE_ENVIRONMENT=E2e`.
3. Ensure PostgreSQL is active on port `5432` with a valid `nadergorge_e2e` database created.
4. Run `npm install typescript ts-node dotenv` in your terminal root.

## Installation
Run the following inside the `frontend` directory:

```bash
npm install -D @playwright/test
npx playwright install --with-deps chromium firefox webkit
```

## Running the Tests
To run tests headlessly:
```bash
npx playwright test
```

To run with full UI tracing:
```bash
npx playwright test --ui
```

If a test fails, screenshots and traces will be stored locally inside the `test-results/` output directory.
