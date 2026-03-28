import { test, expect } from '@playwright/test';

test.describe('Access Codes Generation & Redemption Flow', () => {
  test.beforeEach(async ({ request }) => {
    // Clear devices to prevent limit issues
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000000' },
    });
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
    // Setup mock package via E2E API
    const setupResponse = await request.post(
      'http://localhost:5245/api/e2e/setup-mock-package'
    );
    expect(setupResponse.ok()).toBeTruthy();
  });

  test('T014: Admin can access codes page', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    // Login as admin
    await page.goto('/login');
    await page.fill('input[type="tel"]', '20000000000');
    await page.fill('input[type="password"]', 'password');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/\/admin/, { timeout: 15000 });

    // Navigate to codes page
    await page.goto('/admin/codes');
    await expect(
      page.getByRole('heading', { name: 'Access Codes' })
    ).toBeVisible({ timeout: 10000 });
    // "BullMQ Bulk Generate" button should exist
    await expect(page.getByText('BullMQ Bulk Generate')).toBeVisible();

    await context.close();
  });

  test('T015: Student can access code redemption page', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    // Login as student
    await page.goto('/login');
    await page.fill('input[type="tel"]', '20000000001');
    await page.fill('input[type="password"]', 'password');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/\/student/, { timeout: 15000 });

    // Navigate to code redemption page
    await page.goto('/student/code-redemption');
    // Verify the page loads
    await expect(page).toHaveURL(/\/student\/code-redemption/);

    await context.close();
  });
});
