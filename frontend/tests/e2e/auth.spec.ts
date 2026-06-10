import { test, expect } from '@playwright/test';

test.describe('Auth and Access Flow', () => {
  test.beforeEach(async ({ request }) => {
    // Clear devices to avoid limits
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });

  });

  test('T007: Reject invalid password logins', async ({ page }) => {
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);

    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('wrongpassword');

    // Intercept the API response
    const responsePromise = page.waitForResponse((resp) =>
      resp.url().includes('/auth/login')
    );

    await page.locator('button[type="submit"]').click({ force: true });

    // Verify the API itself returned 401 (unauthorized)
    const response = await responsePromise;
    expect(response.status()).toBe(401);

    // Should stay on login page (not redirect)
    await page.waitForTimeout(500);
    await expect(page).toHaveURL(/\/login/);
  });

  test('T006: Successful login with valid seeded student', async ({ page }) => {
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);

    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('password');
    await page.locator('button[type="submit"]').click({ force: true });

    // Student role → should redirect to /student
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });
  });

  test('T008: Block login when device limit exceeded', async ({ page }) => {
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);

    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000002');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('password');

    // Intercept the API response
    const responsePromise = page.waitForResponse((resp) =>
      resp.url().includes('/auth/login')
    );

    await page.locator('button[type="submit"]').click({ force: true });

    // Device limit → 400 Bad Request (InvalidOperationException)
    const response = await responsePromise;
    expect(response.status()).toBe(400);

    // Should stay on login page
    await page.waitForTimeout(500);
    await expect(page).toHaveURL(/\/login/);
  });

  test('T009: Prevent Student from accessing Admin, Teacher, or Assistant routes', async ({ page }) => {
    // Login as student first
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000001');
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });

    // Try to access admin route on the student domain
    await page.goto('http://app.localhost:3000/admin');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access teacher route on student domain
    await page.goto('http://app.localhost:3000/teacher');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access assistant route on student domain
    await page.goto('http://app.localhost:3000/assistant');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });
  });

  test('T010: Prevent Teacher from accessing Admin, Student, or Assistant routes', async ({ page }) => {
    // Login as teacher first
    await page.goto('http://teacher.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000004'); // seeded teacher
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/teacher$/, { timeout: 15000 });

    // Try to access admin route on teacher domain
    await page.goto('http://teacher.localhost:3000/admin');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access student route on teacher domain
    await page.goto('http://teacher.localhost:3000/student');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access assistant route on teacher domain
    await page.goto('http://teacher.localhost:3000/assistant');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });
  });

  test('T011: Prevent Assistant from accessing Admin, Student, or Teacher routes', async ({ page }) => {
    // Login as assistant first
    await page.goto('http://staff.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000003'); // seeded assistant
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/assistant(\/dashboard)?$/, { timeout: 15000 });

    // Try to access admin route on assistant domain
    await page.goto('http://staff.localhost:3000/admin');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access student route on assistant domain
    await page.goto('http://staff.localhost:3000/student');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access teacher route on assistant domain
    await page.goto('http://staff.localhost:3000/teacher');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });
  });
});
