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

    // Click the Term card to go to Term page
    const termCard = page.locator('text=E2E Term').first();
    await termCard.waitFor({ state: 'visible', timeout: 15000 });
    await termCard.click();

    // Click the Section card to go to Section page
    const sectionCard = page.locator('text=E2E Section').first();
    await sectionCard.waitFor({ state: 'visible', timeout: 15000 });
    await sectionCard.click();

    // Click the Lesson row to go to Lesson page
    const lessonRow = page.locator('text=E2E Lesson').first();
    await lessonRow.waitFor({ state: 'visible', timeout: 15000 });
    await lessonRow.click();

    await expect(page).toHaveURL(
      new RegExp(`/lessons/${mockPackageData.lessonId}`),
      { timeout: 10000 }
    );

    // Find and click the "حل الواجب" or "اذهب لحل الواجب" button
    const homeworkBtn = page.locator(
      'button:has-text("حل الواجب"), a:has-text("حل الواجب"), button:has-text("اذهب لحل الواجب"), a:has-text("اذهب لحل الواجب")'
    ).first();
    await expect(homeworkBtn).toBeVisible({ timeout: 15000 });
    await homeworkBtn.click({ force: true });

    // Verify homework page loads
    await expect(page).toHaveURL(
      new RegExp(`/student/homework/${mockPackageData.homeworkId}`),
      { timeout: 10000 }
    );



    // ─── Answer Question 1 (MCQ: "ما ناتج 1+1؟") ───
    const mcq1Option = page.locator('label').filter({ hasText: '2' }).first();
    await mcq1Option.click({ force: true });
    await page.locator('button:has-text("التالي")').click({ force: true });

    // ─── Answer Question 2 (MCQ: "ما الغاز الذي تنتجه النباتات؟") ───
    const mcq2Option = page.locator('label').filter({ hasText: 'الأكسجين' }).first();
    await mcq2Option.click({ force: true });
    await page.locator('button:has-text("التالي")').click({ force: true });

    // ─── Skip Question 3 (FindTheMistake) ───
    await page.locator('button:has-text("التالي")').click({ force: true });

    // ─── Answer Question 4 (Essay) ───
    const textarea = page.locator('textarea').first();
    await textarea.waitFor({ state: 'visible', timeout: 5000 });
    await textarea.fill('This is a test essay answering the E2E homework question.');

    // Click submit button ("تسليم الواجب")
    await page.locator('button:has-text("تسليم الواجب")').click({ force: true });

    // A confirmation dialog should appear because we skipped question 3
    const confirmBtn = page.locator('button:has-text("نعم، سلّم الآن")');
    await expect(confirmBtn).toBeVisible({ timeout: 5000 });
    await confirmBtn.click({ force: true });

    // Await result view
    await expect(
      page.locator('text=العودة للحصة').first()
    ).toBeVisible({ timeout: 15000 });

    // Navigate back to the student dashboard where the sidebar containing the gamification widget is present
    await page.goto('http://app.localhost:3000/student');

    // Validate Gamification Widget via Sidebar UI (XP should ideally be 20 for homework submission)
    // The widget typically renders "20" "XP" or similar.
    const xpElement = page.locator('span:has-text("20")').first();
    await expect(xpElement).toBeVisible({ timeout: 15000 });
  });
});
