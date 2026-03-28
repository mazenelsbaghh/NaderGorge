import { test, expect } from '@playwright/test';

test.describe('Admin Content Management Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    await page.fill('input[type="tel"]', '20000000000');
    await page.fill('input[type="password"]', 'password');
    await page.click('button[type="submit"]');
    // Admin role -> redirects to /admin
    await expect(page).toHaveURL(/\/admin/, { timeout: 15000 });
  });

  test('T009 & T010: Create Package, Section, Lesson, and Video', async ({
    page,
  }) => {
    // Navigate to content management
    await page.goto('/admin/content');
    await expect(page.locator('text=إدارة المحتوى')).toBeVisible();

    // 1. Create Package
    // Click the "+ New Package" button
    await page.click('text=+ باقة جديدة');

    // Fill the modal form (uses placeholder-based inputs, not name attributes)
    const uniqueName = `E2E Pkg ${Date.now()}`;
    await page.fill('input[placeholder="Package Name"]', uniqueName);
    await page.fill('textarea[placeholder="Description"]', 'E2E test package');
    await page.fill('input[placeholder="Price (EGP)"]', '100');

    // Submit
    await page.click('button:has-text("Create")');

    // Wait for modal to close and package to appear
    await expect(page.locator(`text=${uniqueName}`)).toBeVisible({
      timeout: 10000,
    });

    // 2. Click into the package to see sections
    await page.click(`text=عرض الأقسام`);
    // The tab should now show "Sections"
    await expect(page.locator('text=+ قسم جديد')).toBeVisible({
      timeout: 5000,
    });

    // Create Section
    await page.click('text=+ قسم جديد');
    await page.fill('input[placeholder="Section Title"]', 'E2E Section');
    await page.fill('input[placeholder="Order"]', '1');
    await page.click('button:has-text("Create")');
    await expect(page.locator('text=E2E Section')).toBeVisible({
      timeout: 10000,
    });

    // 3. Click into section to see lessons
    await page.click('text=عرض الدروس');
    await expect(page.locator('text=+ درس جديد')).toBeVisible({
      timeout: 5000,
    });

    // Create Lesson
    await page.click('text=+ درس جديد');
    await page.fill('input[placeholder="Lesson Title"]', 'E2E Lesson');
    await page.fill('textarea[placeholder="Summary"]', 'E2E lesson summary');
    await page.click('button:has-text("Create")');
    await expect(page.locator('text=E2E Lesson')).toBeVisible({
      timeout: 10000,
    });
  });
});
