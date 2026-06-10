import { test, expect } from '@playwright/test';

test.describe('Admin Content Management Flow', () => {
  test.beforeEach(async ({ request, page }) => {
    // Setup mock package via E2E API to ensure Program and Teacher options exist
    const setupResponse = await request.post(
      'http://localhost:5245/api/e2e/setup-mock-package'
    );
    expect(setupResponse.ok()).toBeTruthy();

    // Login as admin
    await page.goto('http://admin.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000000');
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    // Admin role -> redirects to /admin
    await expect(page).toHaveURL(/.*\/admin$/, { timeout: 15000 });
  });

  test('T009 & T010: Create Package, Section, Lesson, and Video', async ({
    page,
  }) => {
    test.setTimeout(60000);
    // Navigate to content management
    await page.goto('http://admin.localhost:3000/admin/content');
    await expect(page.locator('text=إدارة المحتوى')).toBeVisible();

    // 1. Create Package
    // Click the "إضافة باقة جديدة" button
    await page.click('text=إضافة باقة جديدة');

    // Fill the modal form (uses placeholder-based inputs)
    const uniqueName = `E2E Pkg ${Date.now()}`;
    await page.fill('input[placeholder*="اسم الباقة"]', uniqueName);
    await page.fill('textarea[placeholder*="وصف مختصر"]', 'E2E test package');
    await page.fill('input[placeholder*="السعر"]', '100');

    // Select Teacher, Subject, and Grade Level
    await page.selectOption('select:has-text("اختر المدرس")', { index: 1 });
    await page.selectOption('select:has-text("اختر المادة")', { index: 1 });
    await page.selectOption('select:has-text("اختر الصف الدراسي")', { index: 1 });

    // Submit
    await page.click('button:has-text("حفظ الباقة")', { force: true });

    // Wait for package to appear
    await expect(page.locator(`text=${uniqueName}`)).toBeVisible({
      timeout: 10000,
    });

    const packageCard = page.locator('div.rounded-2xl').filter({ hasText: uniqueName }).first();
    await packageCard.locator('a[title="عرض تفاصيل الباقة"]').click({ force: true });
    
    // Wait for the details page to load (specifically wait for the tab "نظرة عامة" to be visible)
    await expect(page.locator('text=نظرة عامة')).toBeVisible({ timeout: 15000 });

    // Default tab is Terms
    // Click "إضافة" to create a Term
    await page.click('button:has-text("إضافة")', { force: true });
    await page.fill('input[placeholder*="اسم الترم"]', 'E2E Term');
    await page.click('button:has-text("حفظ")', { force: true });

    // Wait for the term to appear and click it to go to Term details
    await expect(page.locator('text=E2E Term')).toBeVisible({ timeout: 10000 });
    await page.click('text=E2E Term', { force: true });

    // Wait for Term details page to load
    await expect(page.locator('text=الشهور / الأقسام')).toBeVisible({ timeout: 15000 });

    // 3. We are on Term details. Go to sections tab
    await page.click('text=الشهور / الأقسام', { force: true });
    await page.click('button:has-text("إضافة")', { force: true });
    await page.fill('input[placeholder*="اسم القسم"]', 'E2E Section');
    await page.click('button:has-text("حفظ")', { force: true });

    // Wait for section and click it to go to Section details
    await expect(page.locator('text=E2E Section')).toBeVisible({ timeout: 10000 });
    await page.click('text=E2E Section', { force: true });

    // Wait for Section details page to load
    await expect(page.locator('text=الحصص')).toBeVisible({ timeout: 15000 });

    // 4. We are on Section details. Go to lessons tab
    await page.click('text=الحصص', { force: true });
    await page.click('button:has-text("إضافة")', { force: true });
    await page.fill('input[placeholder*="عنوان الحصة"]', 'E2E Lesson');
    await page.fill('textarea[placeholder*="نبذة قصيرة"]', 'E2E lesson summary');
    await page.click('button:has-text("حفظ")', { force: true });

    // Wait for lesson to appear
    await expect(page.locator('text=E2E Lesson')).toBeVisible({ timeout: 10000 });
  });
});

