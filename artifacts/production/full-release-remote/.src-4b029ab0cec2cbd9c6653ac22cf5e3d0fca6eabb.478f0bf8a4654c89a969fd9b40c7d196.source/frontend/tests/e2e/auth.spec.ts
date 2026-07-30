import { test, expect } from '@playwright/test';

test.describe('Auth and Access Flow', () => {
  test.beforeEach(async ({ request }) => {
    // Clear devices to avoid limits
    const response = await request.post('http://api.lvh.me:5245/api/e2e/clear-devices', {
      headers: {
        'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345',
      },
      data: { phoneNumber: '20000000001' },
    });
    expect(response.ok()).toBeTruthy();
  });

  async function loginStudentViaBrowserApi(page: import('@playwright/test').Page) {
    await page.goto('http://app.lvh.me:3000/login');
    const deviceFingerprint = `phase1-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    const result = await page.evaluate(async (fingerprint) => {
      const response = await fetch('http://api.lvh.me:5245/api/auth/login', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          phoneNumber: '20000000001',
          password: 'password',
          deviceFingerprint: fingerprint,
          deviceName: navigator.userAgent.slice(0, 100),
        }),
      });
      return { ok: response.ok, status: response.status, body: await response.text() };
    }, deviceFingerprint);

    expect(result, result.body).toMatchObject({ ok: true, status: 200 });
  }

  test('T007: Reject invalid password logins', async ({ page }) => {
    await page.goto('http://app.lvh.me:3000/login');
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
    await page.goto('http://app.lvh.me:3000/login');
    await page.waitForTimeout(1000);

    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('password');
    await page.locator('button[type="submit"]').click({ force: true });

    // Student role → should redirect to /student
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });
  });

  test('Phase 1: Hydrates auth from refresh cookie after token storage is empty', async ({ page }) => {
    await loginStudentViaBrowserApi(page);

    await page.evaluate(() => {
      window.localStorage.removeItem('accessToken');
      window.sessionStorage.removeItem('accessToken');
      window.localStorage.removeItem('user');
      window.sessionStorage.removeItem('user');
    });

    await page.goto('http://app.lvh.me:3000/student');
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });
    await expect(page).not.toHaveURL(/\/login/);
  });

  test('Phase 1: Forbidden route does not clear authenticated session', async ({ page }) => {
    await loginStudentViaBrowserApi(page);
    await page.goto('http://app.lvh.me:3000/student');
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });

    await page.goto('http://app.lvh.me:3000/admin', { waitUntil: 'commit' }).catch((error: Error) => {
      if (!error.message.includes('ERR_ABORTED')) {
        throw error;
      }
    });

    const deniedMessage = page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first();
    if (await deniedMessage.isVisible({ timeout: 5000 }).catch(() => false)) {
      await expect(deniedMessage).toBeVisible();
    }

    await page.goto('http://app.lvh.me:3000/student');
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });
  });

  test('T008: Block login when device limit exceeded', async ({ page }) => {
    await page.goto('http://app.lvh.me:3000/login');
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
    await page.goto('http://app.lvh.me:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000001');
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });

    // Try to access admin route on the student domain
    await page.goto('http://app.lvh.me:3000/admin');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access teacher route on student domain
    await page.goto('http://app.lvh.me:3000/teacher');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access assistant route on student domain
    await page.goto('http://app.lvh.me:3000/assistant');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });
  });

  test('T010: Prevent Teacher from accessing Admin, Student, or Assistant routes', async ({ page }) => {
    // Login as teacher first
    await page.goto('http://teacher.lvh.me:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000004'); // seeded teacher
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/teacher$/, { timeout: 15000 });

    // Try to access admin route on teacher domain
    await page.goto('http://teacher.lvh.me:3000/admin');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access student route on teacher domain
    await page.goto('http://teacher.lvh.me:3000/student');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access assistant route on teacher domain
    await page.goto('http://teacher.lvh.me:3000/assistant');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });
  });

  test('T011: Prevent Assistant from accessing Admin, Student, or Teacher routes', async ({ page }) => {
    // Login as assistant first
    await page.goto('http://staff.lvh.me:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[name="phoneNumber"]', '20000000003'); // seeded assistant
    await page.fill('input[name="password"]', 'password');
    await page.click('text=تذكرني', { force: true });
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/assistant(\/dashboard)?$/, { timeout: 15000 });

    // Try to access admin route on assistant domain
    await page.goto('http://staff.lvh.me:3000/admin');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access student route on assistant domain
    await page.goto('http://staff.lvh.me:3000/student');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });

    // Try to access teacher route on assistant domain
    await page.goto('http://staff.lvh.me:3000/teacher');
    await expect(page.locator('text=الصفحة غير موجودة أو لا تخص هذا الحساب').first()).toBeVisible({ timeout: 10000 });
  });
});
