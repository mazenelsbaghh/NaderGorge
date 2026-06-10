import { test, expect } from '@playwright/test';

test.describe('Teacher Isolation Boundaries', () => {
  let mockPackageData: any;

  test.beforeEach(async ({ request }) => {
    // 1. Seed database with Teachers and Students
    await request.post('http://localhost:5245/api/e2e/seed', {
      data: {
        clearDatabase: true,
        seedAdmin: true,
        seedStudents: true,
        seedAssistant: false,
        seedTeacher: true,
      },
    });

    // 2. Setup mock package (linked to Teacher A by default)
    const setupResponse = await request.post(
      'http://localhost:5245/api/e2e/setup-mock-package'
    );
    expect(setupResponse.ok()).toBeTruthy();
    mockPackageData = await setupResponse.json();
  });

  test('T009: Teacher A can access their own package', async ({ page }) => {
    // Login as Teacher A (20000000004)
    await page.goto('http://teacher.localhost:3000/login');
    await page.locator('input[type="tel"]').fill('20000000004');
    await page.locator('input[type="password"]').fill('password');
    await page.click('button[type="submit"]', { force: true });

    await expect(page).toHaveURL(/.*\/teacher$/, { timeout: 15000 });

    // Go to the package detail page
    await page.goto(`http://teacher.localhost:3000/teacher/packages/packages/${mockPackageData.packageId}`);

    // Verify they see the package terms list or page content
    await expect(page.locator('text=نظرة عامة')).toBeVisible({ timeout: 15000 });
  });

  test('T009: Teacher B is blocked from accessing Teacher A\'s package', async ({ page }) => {
    // Login as Teacher B (20000000005)
    await page.goto('http://teacher.localhost:3000/login');
    await page.locator('input[type="tel"]').fill('20000000005');
    await page.locator('input[type="password"]').fill('password');
    await page.click('button[type="submit"]', { force: true });

    await expect(page).toHaveURL(/.*\/teacher$/, { timeout: 15000 });

    // Try to access Teacher A's package
    await page.goto(`http://teacher.localhost:3000/teacher/packages/packages/${mockPackageData.packageId}`);

    // Verify they get "لا يمكن العثور على الباقة المطلوبة" (The requested package cannot be found)
    await expect(page.locator('text=لا يمكن العثور على الباقة المطلوبة')).toBeVisible({ timeout: 15000 });
  });
});
