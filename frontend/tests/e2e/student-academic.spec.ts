import { test, expect } from '@playwright/test';

test.describe('Student Academic Journey & Gamification', () => {
  let mockPackageData: any;

  test.beforeEach(async ({ request, page }) => {
    // 0. Clear devices and gamification for Student 1
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
    await request.post('http://localhost:5245/api/e2e/reset-gamification', {
      data: { phoneNumber: '20000000001' },
    });

    // 1. Setup mock package via E2E API (now includes Homework)
    const setupResponse = await request.post(
      'http://localhost:5245/api/e2e/setup-mock-package'
    );
    expect(setupResponse.ok()).toBeTruthy();
    mockPackageData = await setupResponse.json();

    // 2. Grant package to Student 1
    await request.post('http://localhost:5245/api/e2e/grant-package', {
      data: { packageId: mockPackageData.packageId },
    });

    // 3. Login as Student 1
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('password');
    await page.click('text=تذكرني', { force: true });
    await page.locator('button[type="submit"]').click({ force: true });
    await expect(page).toHaveURL(/.*\/(student|onboarding)$/, { timeout: 15000 });
  });

  test('T005 & T006: Student completes homework and receives gamification points', async ({
    page,
  }) => {
    test.setTimeout(60000);
    // Navigate to the enrolled package directly
    await page.goto(`http://app.localhost:3000/student/packages/${mockPackageData.packageId}`);

    // Expand the "E2E Section" accordion
    const sectionTitle = page.getByRole('heading', { name: 'E2E Section' });
    await sectionTitle.waitFor({ state: 'visible', timeout: 15000 });
    await sectionTitle.click();

    // Navigate to the lesson page
    const viewButton = page.locator('button:has-text("مشاهدة")').first();
    await viewButton.waitFor({ state: 'visible', timeout: 10000 });
    await viewButton.click();

    await expect(page).toHaveURL(
      new RegExp(`/lessons/${mockPackageData.lessonId}`),
      { timeout: 10000 }
    );

    // Answer the essay question
    const textarea = page.locator('textarea, [placeholder*="إجابتك"]').first();
    await textarea.waitFor({ state: 'visible' });
    await textarea.fill(
      'This is a test essay answering the E2E homework question.'
    );

    // Submit homework
    const submitBtn = page.locator('button:has-text("تسليم وإنهاء")');
    await submitBtn.click();

    // Await success message
    await expect(page.locator('text=تم تسليم الواجب بنجاح')).toBeVisible({
      timeout: 10000,
    });

    // Navigate back to the student dashboard where the sidebar containing the gamification widget is present
    await page.goto('http://app.localhost:3000/student');

    // Validate Gamification Widget via Sidebar UI (XP should ideally be 20 for homework submission)
    // The widget typically renders "20" "XP" or similar.
    const xpElement = page.locator('span:has-text("20")').first();
    await expect(xpElement).toBeVisible({ timeout: 15000 });
  });
});
