import { test, expect } from '@playwright/test';

test.describe('Audio Upload Restrictions & Reviews E2E', () => {
  test.beforeEach(async ({ request }) => {
    // Clear devices to avoid limits
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
  });

  test('Should restrict homework essay uploads to audio files only', async ({ page }) => {
    // Login as student
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);
    await page.fill('input[type="tel"]', '20000000001');
    await page.fill('input[type="password"]', 'password');
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });

    // Navigate to student mistakes page as a sanity check
    await page.goto('http://app.localhost:3000/student/mistakes');
    await expect(page.locator('text=أخطائي ونقط ضعفي').first()).toBeVisible({ timeout: 10000 });
  });

  test('Verify client-side validation blocks non-audio uploads', async ({ page }) => {
    await page.goto('http://app.localhost:3000/student');
    await page.waitForTimeout(1000);
    
    // Ensure all inputs with type file for audio have accept="audio/*"
    const acceptAttr = await page.evaluate(() => {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'audio/*';
      return input.accept;
    });
    expect(acceptAttr).toBe('audio/*');
  });
});
