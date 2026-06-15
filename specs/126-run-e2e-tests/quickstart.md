# Quickstart: E2E Test Execution

This guide outlines how to set up the environment and execute the E2E tests.

## Prerequisites

1. Ensure the Docker containers (database, redis, worker, backend) are running:
   ```bash
   # From the repository root
   docker compose up -d
   ```
2. Verify the backend is responding and running in the `E2e` environment:
   ```bash
   curl http://localhost:5245/api/health
   ```

## Running the E2E Tests

1. Start the Next.js dev server on port 3000 (which is the target `baseURL` for E2E):
   ```bash
   cd frontend
   npm run dev -- -p 3000
   ```
2. In a separate terminal, run the Playwright test command:
   ```bash
   cd frontend
   npm run test:e2e
   ```
3. To view the HTML test report:
   ```bash
   npx playwright show-report
   ```
