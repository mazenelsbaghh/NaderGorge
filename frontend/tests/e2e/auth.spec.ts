import { test, expect } from '@playwright/test';

test.describe('Auth and Access Flow', () => {

  test('T007: Reject invalid password logins', async ({ page }) => {
    await page.goto('/login');
    await page.waitForTimeout(1000);
    
    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('wrongpassword');

    // Intercept the API response
    const responsePromise = page.waitForResponse(resp => 
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
    await page.goto('/login');
    await page.waitForTimeout(1000);
    
    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('password');
    await page.locator('button[type="submit"]').click({ force: true });

    // Student role → should redirect to /student
    await expect(page).toHaveURL(/\/student/, { timeout: 15000 });
  });

  test('T008: Block login when device limit exceeded', async ({ page }) => {
    await page.goto('/login');
    await page.waitForTimeout(1000);
    
    await page.locator('input[type="tel"]').click();
    await page.locator('input[type="tel"]').fill('20000000002');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').fill('password');

    // Intercept the API response 
    const responsePromise = page.waitForResponse(resp => 
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
});
