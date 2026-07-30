import { devices, expect, test, type Page } from '@playwright/test';

import { appUrl, installAuthAndGoto } from './e2e-contract-helpers';

const studentUser = {
  id: 'resilient-student',
  fullName: 'طالب اختبار الحالات',
  phone: '20000000740',
  roles: ['Student'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['student'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};

async function installFailingStudentApi(page: Page, secret: string) {
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    if (pathname.endsWith('/auth/session')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: { user: studentUser, authorizationVersion: 1 },
        }),
      });
      return;
    }
    if (pathname.endsWith('/student/dashboard')) {
      await new Promise((resolve) => setTimeout(resolve, 700));
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: secret }),
      });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: [] }),
    });
  });
}

test('student loading and error regions remain labelled without exposing server details', async ({
  page,
}) => {
  const secret = 'SENSITIVE_INTERNAL_ERROR_MESSAGE';
  await installFailingStudentApi(page, secret);
  await installAuthAndGoto(
    page,
    'resilient-student-token',
    studentUser,
    `${appUrl}/student`,
  );

  await expect(page.getByRole('status', { name: /تحميل.*طالب|تحميل.*لوحة/ }))
    .toBeVisible();
  await expect(page.getByRole('alert')).toContainText(
    'تعذر تحميل لوحة الطالب',
  );
  await expect(page.locator('body')).not.toContainText(secret);
  await expect(page.getByRole('button', { name: 'إعادة المحاولة' })).toBeVisible();
});

test('320px, 200% zoom, long Arabic feedback, and both themes avoid horizontal loss', async ({
  browser,
}) => {
  const context = await browser.newContext({
    ...devices['iPhone 13'],
    viewport: { width: 320, height: 640 },
  });
  const page = await context.newPage();
  const longArabic = 'تعذر إكمال العملية الآن '.repeat(18).trim();
  await page.route('**/api/auth/login', (route) =>
    route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({ message: longArabic }),
    }),
  );
  await page.goto(`${appUrl}/login`);
  await page.locator('#login-phone').fill('20000000740');
  await page.locator('#login-password').fill('password');
  await page.getByRole('button', { name: 'تسجيل الدخول' }).click();
  await expect(page.getByRole('alert')).toContainText(longArabic);

  await page.evaluate(() => {
    document.documentElement.style.zoom = '2';
  });
  const hasHorizontalOverflow = () => page.evaluate(() =>
    document.documentElement.scrollWidth >
    document.documentElement.clientWidth + 1,
  );
  expect(await hasHorizontalOverflow()).toBe(false);

  await page.getByRole('button', { name: 'التحويل إلى الوضع الداكن' }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme-mode', 'dark');
  expect(await hasHorizontalOverflow()).toBe(false);
  await context.close();
});
