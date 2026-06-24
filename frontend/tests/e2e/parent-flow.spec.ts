import { test, expect } from '@playwright/test';

// Helper to construct browser-compatible CORS headers for mocked routes
function getHeaders(route: any) {
  const origin = route.request().headers().origin || "*";
  return {
    "Access-Control-Allow-Origin": origin,
    "Access-Control-Allow-Credentials": "true",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS, PUT, DELETE, HEAD",
    "Access-Control-Allow-Headers": "Content-Type, X-App-Surface, Authorization"
  };
}

test.describe('Parent Code Popup and Header Badge', () => {
  test.beforeEach(async ({ request, page }) => {
    // Log console messages and errors from the page
    page.on('console', msg => console.log('BROWSER LOG:', msg.text()));
    page.on('pageerror', err => console.error('BROWSER ERROR:', err.message));

    // Mock clipboard API
    await page.addInitScript(() => {
      Object.defineProperty(navigator, 'clipboard', {
        value: {
          writeText: async (text) => {
            (window as any)._copiedText = text;
          },
          readText: async () => (window as any)._copiedText || ''
        },
        configurable: true
      });
    });

    // Clear devices for Student 1 so login doesn't hit device limit
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
  });

  test('T101: Modal displays on first login, copy functions, modal closes, and header badge persists', async ({ page }) => {
    let acknowledgeCalled = false;
    let bootstrapCalled = false;

    // Intercept student shell bootstrap and set tracking popup state to false
    await page.route('**/api/student/shell-bootstrap', async (route) => {
      bootstrapCalled = true;
      console.log('MOCK BOOTSTRAP CALLED');
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: getHeaders(route),
        body: JSON.stringify({
          success: true,
          data: {
            unreadNotificationsCount: 5,
            currentBalance: 120.0,
            gamification: {
              totalPoints: 100,
              currentStreakCount: 3,
              longestStreakCount: 5,
              levelName: 'طالب مجتهد'
            },
            themePreferences: {
              currentMode: 'dark',
              selectedLightPaletteId: 'default',
              selectedDarkPaletteId: 'default',
              availableLightPalettes: [],
              availableDarkPalettes: []
            },
            avatarSlug: 'avatar-lion',
            parentTrackingCode: 'XYZ123',
            hasSeenTrackingCodePopup: false,
          }
        })
      });
    });

    // Intercept the acknowledge endpoint
    await page.route('**/api/student/acknowledge-tracking-popup', async (route) => {
      acknowledgeCalled = true;
      console.log('MOCK ACKNOWLEDGE CALLED');
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: getHeaders(route),
        body: JSON.stringify({ success: true, data: null })
      });
    });

    // Go to login page
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);

    // Perform login
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').fill('password');
    await page.click('text=تذكرني', { force: true });
    await page.locator('button[type="submit"]').click({ force: true });

    // Wait until redirected to student area
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });

    console.log('REDIRECTED TO /student');

    // Dismiss the login access contract modal if present
    const contractButton = page.locator('button:has-text("أوافق وأرغب في استكمال استخدام المنصة")');
    await expect(contractButton).toBeVisible({ timeout: 15000 });
    await contractButton.click();

    // 1. Verify modal displays on first login
    const modalHeading = page.locator('h2#popup-title');
    await expect(modalHeading).toBeVisible({ timeout: 10000 });
    await expect(modalHeading).toHaveText('تابع مستواك الدراسي مع ولي أمرك');

    // Verify 6-character code is visible inside modal
    const modalCode = page.locator('[role="dialog"] >> text=XYZ123');
    await expect(modalCode).toBeVisible();

    // 2. Click Copy button and verify it changes text
    const copyButton = page.getByRole('button', { name: 'نسخ الرمز' });
    await expect(copyButton).toBeVisible();
    await copyButton.click();

    // Check that button shows success state
    await expect(page.getByRole('button', { name: 'تم النسخ' })).toBeVisible();

    // 3. Click Close button ("حفظ ومتابعة")
    const closeButton = page.getByRole('button', { name: 'حفظ ومتابعة' });
    await expect(closeButton).toBeVisible();
    await closeButton.click();

    // Verify modal is dismissed
    await expect(modalHeading).not.toBeVisible({ timeout: 5000 });

    // Verify API was called
    expect(acknowledgeCalled).toBe(true);

    // 4. Verify permanent header badge is displayed and shows the code
    const headerBadge = page.locator('[data-testid="header-parent-badge"]');
    await expect(headerBadge).toBeVisible();
    await expect(headerBadge).toContainText('XYZ123');

    console.log('BOOTSTRAP CALLED STATE:', bootstrapCalled);
  });

  test('T102: Modal does not display when student has already seen it, but header badge displays', async ({ page }) => {
    let bootstrapCalled = false;

    // Intercept student shell bootstrap and set tracking popup state to true
    await page.route('**/api/student/shell-bootstrap', async (route) => {
      bootstrapCalled = true;
      console.log('MOCK BOOTSTRAP CALLED (T102)');
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: getHeaders(route),
        body: JSON.stringify({
          success: true,
          data: {
            unreadNotificationsCount: 2,
            currentBalance: 50.0,
            gamification: {
              totalPoints: 50,
              currentStreakCount: 1,
              longestStreakCount: 2,
              levelName: 'طالب'
            },
            themePreferences: {
              currentMode: 'light',
              selectedLightPaletteId: 'default',
              selectedDarkPaletteId: 'default',
              availableLightPalettes: [],
              availableDarkPalettes: []
            },
            avatarSlug: 'avatar-lion',
            parentTrackingCode: 'ABC987',
            hasSeenTrackingCodePopup: true,
          }
        })
      });
    });

    // Go to login page
    await page.goto('http://app.localhost:3000/login');
    await page.waitForTimeout(1000);

    // Perform login
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').fill('password');
    await page.click('text=تذكرني', { force: true });
    await page.locator('button[type="submit"]').click({ force: true });

    // Wait until redirected to student area
    await expect(page).toHaveURL(/.*\/student$/, { timeout: 15000 });

    // Dismiss the login access contract modal if present
    const contractButton = page.locator('button:has-text("أوافق وأرغب في استكمال استخدام المنصة")');
    await expect(contractButton).toBeVisible({ timeout: 15000 });
    await contractButton.click();

    // Verify modal is NOT visible
    const modalHeading = page.locator('h2#popup-title');
    await expect(modalHeading).not.toBeVisible({ timeout: 5000 });

    // Verify permanent header badge is still displayed and shows the code ABC987
    const headerBadge = page.locator('[data-testid="header-parent-badge"]');
    await expect(headerBadge).toBeVisible();
    await expect(headerBadge).toContainText('ABC987');

    console.log('BOOTSTRAP CALLED STATE (T102):', bootstrapCalled);
  });
});
