import { defineConfig, devices } from '@playwright/test';
import dotenv from 'dotenv';
import path from 'path';

// Read from default ".env" file.
dotenv.config({ path: path.resolve(__dirname, '.env.test') });

const e2ePort = process.env.PLAYWRIGHT_WEB_PORT || '3000';
const e2eBaseURL = process.env.PLAYWRIGHT_BASE_URL || `http://app.lvh.me:${e2ePort}`;
const e2eApiURL = process.env.E2E_API_URL || 'http://api.lvh.me:5245/api';
const e2eBackendURL = e2eApiURL.replace(/\/api\/?$/, '');
const useProductionBuild = process.env.PLAYWRIGHT_USE_PRODUCTION_BUILD === '1';

export default defineConfig({
  testDir: './tests/e2e',
  timeout: 30000,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // Tests share E2E database state, must run sequentially
  reporter: 'html',
  globalSetup: require.resolve('./tests/fixtures/global-setup'),
  webServer: {
    command:
      `NEXT_PUBLIC_API_URL=${e2eApiURL} NEXT_PUBLIC_BACKEND_URL=${e2eBackendURL} npx next ${useProductionBuild ? 'start' : 'dev'} -p ${e2ePort}`,
    url: e2eBaseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
  },
  use: {
    baseURL: e2eBaseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    extraHTTPHeaders: {
      'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345',
    },
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'webkit',
      use: { ...devices['iPhone 13'] },
    },
  ],
});
