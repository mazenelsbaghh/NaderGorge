import { defineConfig, devices } from '@playwright/test';

const port = process.env.PLAYWRIGHT_WEB_PORT || '3017';
const baseURL = process.env.ADMIN_E2E_URL || `http://admin.lvh.me:${port}`;

// All API boundaries are intercepted by this suite. No database seeding or
// external support server is needed to exercise the real page and its guard.
export default defineConfig({
  testDir: './tests/e2e',
  testMatch: 'center-desktop.spec.ts',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  forbidOnly: true,
  workers: 1,
  retries: 0,
  reporter: 'line',
  webServer: {
    command: `NEXT_PUBLIC_API_URL=http://api.lvh.me:5245/api npx next dev -p ${port}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{
    name: 'chromium',
    use: {
      ...devices['Desktop Chrome'],
      ...(process.env.PLAYWRIGHT_CHROME_CHANNEL === '1' ? { channel: 'chrome' as const } : {}),
    },
  }],
});
