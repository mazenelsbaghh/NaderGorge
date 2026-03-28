# Quickstart: E2E Playwright Tests

**Date**: 2026-03-27

## Installation

```bash
cd frontend
npm ci
npx playwright install
```

## Running the E2E Verification Tests

First, start both the .NET API backend and NextJS frontend locally. Set the URL of the App inside your command or just use localhost.

### Headless execution (Full Suite)

```bash
cd frontend
npx playwright test --project=chromium
```

### Visual Execution (Debugging Mode)

```bash
cd frontend
npx playwright test --ui
```

### Specific Feature Testing (E.g., Only Wallet Purchase flow)

```bash
cd frontend
npx playwright test tests/e2e/student-journey.spec.ts --headed
```

### Troubleshooting
* **Tests hang or timeout**: Check if your Development API is active on port `5001`. The API client in E2E tests defaults to expecting `NEXT_PUBLIC_API_URL` locally. If testing Production/Staging, export `PLAYWRIGHT_TEST_BASE_URL` before testing.
* **Flaky Database State**: Since generated data (like `011999xxxx` students) stays in the DB, it's safe to clear Development schemas if it gets too bloated. Tests uniquely prefix entities per-run so conflicts are impossible.
