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
    await page.goto('http://admin.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000000');
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/admin$/, { timeout: 15000 });

    // Navigate to codes page
    await page.goto('http://admin.localhost:3000/admin/codes');
    await expect(
      page.getByRole('heading', { name: 'مجموعات أكواد الوصول' })
    ).toBeVisible({ timeout: 10000 });
    // "إنشاء دفعة جديدة" button should exist
    await expect(page.getByText('إنشاء دفعة جديدة').first()).toBeVisible();

    await context.close();
  });

  test('T015: Student can access code redemption page', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    // Login as student
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000001');
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });

    // Navigate to code redemption page
    await page.goto('http://app.localhost:3000/student/code-redemption');
    // Verify the page loads
    await expect(page).toHaveURL(/\/student\/code-redemption/);

    await context.close();
  });
});
