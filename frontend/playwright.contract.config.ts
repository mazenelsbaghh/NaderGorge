import { defineConfig } from '@playwright/test';


export default defineConfig({
  testDir: './tests/e2e',
  testMatch: [
    'auth-return-navigation.spec.ts',
    'selective-prefetch.spec.ts',
  ],
  timeout: 10_000,
  fullyParallel: true,
  workers: 2,
  reporter: 'line',
});
