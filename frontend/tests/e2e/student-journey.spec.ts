import { test, expect } from '@playwright/test';

test.describe('Student Lesson Consumption and Exams', () => {

  test.beforeEach(async ({ request, page }) => {
    // 0. Clear devices for Student 1 so login doesn't hit device limit
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' }
    });

    // 1. Setup mock package via E2E API
    const setupResponse = await request.post('http://localhost:5245/api/e2e/setup-mock-package');
    expect(setupResponse.ok()).toBeTruthy();
    const mockData = await setupResponse.json();

    // 2. Grant package to Student 1
    await request.post('http://localhost:5245/api/e2e/grant-package', {
      data: { packageId: mockData.packageId }
    });

    // 3. Login as Student 1
    await page.goto('/login');
    await page.waitForTimeout(1000);
    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('password');
    await page.locator('button[type="submit"]').click({ force: true });
    await expect(page).toHaveURL(/\/student/, { timeout: 15000 });
  });

  test('T012: Student can see enrolled package', async ({ page }) => {
    await page.goto('/student/packages');
    
    // Wait for packages to load
    await expect(page.locator('text=الباقات المفعّلة').first()).toBeVisible({ timeout: 10000 });
    
    // Should see an "Enter Package" / "دخول الباقة" button on at least one package
    await expect(page.getByText('دخول الباقة').first()).toBeVisible({ timeout: 10000 });
    
    // Click the card that has entry button
    const enterPackageBtn = page.locator('button:has-text("دخول الباقة")').first();
    await enterPackageBtn.waitFor({ state: 'visible', timeout: 10000 });
    await enterPackageBtn.click();
    
    await expect(page).toHaveURL(/\/student\/packages\//, { timeout: 10000 });
  });

  test('T013: Student dashboard loads correctly', async ({ page }) => {
    await page.goto('/student');
    await expect(page.locator('text=بوابة الطالب').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=باقاتي').first()).toBeVisible();
    await expect(page.locator('text=تفعيل كود').first()).toBeVisible();
  });
});
