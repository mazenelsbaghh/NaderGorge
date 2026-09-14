import { devices, expect, test, type Page } from '@playwright/test';

import { appUrl, installAuthAndGoto, staffUrl } from './e2e-contract-helpers';

const assistantUser = {
  id: 'overlay-assistant',
  fullName: 'مساعد اختبار النافذة',
  phone: '20000000720',
  roles: ['Assistant'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['assistant'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};

const studentUser = {
  id: 'overlay-student',
  fullName: 'طالب اختبار النافذة',
  phone: '20000000721',
  roles: ['Student'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['student'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};

test.use({ ...devices['iPhone 13'] });

async function installAssistantApi(page: Page) {
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    const data = pathname.endsWith('/auth/session')
      ? { user: assistantUser, authorizationVersion: 1 }
      : null;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data }),
    });
  });
}

test('mobile drawer traps focus, makes the background inert, and restores its trigger', async ({
  page,
}) => {
  await installAssistantApi(page);
  await installAuthAndGoto(
    page,
    'overlay-assistant-token',
    assistantUser,
    `${staffUrl}/assistant/dashboard`,
  );
  const trigger = page.getByRole('button', { name: 'المزيد' });
  await expect(trigger).toBeVisible();
  await trigger.focus();
  await trigger.click();

  const dialog = page.getByRole('dialog', { name: 'قائمة الموظفين الإضافية' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator(':focus')).toHaveCount(1);
  expect(await page.evaluate(() =>
    Array.from(document.body.children)
      .filter((element) => !element.hasAttribute('data-accessible-overlay-root'))
      .every((element) =>
        (element as HTMLElement).inert &&
        element.getAttribute('aria-hidden') === 'true'),
  )).toBe(true);

  for (let press = 0; press < 8; press += 1) {
    await page.keyboard.press('Tab');
    expect(await dialog.evaluate((element) =>
      element.contains(document.activeElement),
    )).toBe(true);
  }
  await page.keyboard.press('Shift+Tab');
  expect(await dialog.evaluate((element) =>
    element.contains(document.activeElement),
  )).toBe(true);

  await page.keyboard.press('Escape');

  await expect(dialog).toHaveCount(0);
  await expect(trigger).toBeFocused();
  expect(await page.evaluate(() =>
    Array.from(document.body.children).every(
      (element) => !(element as HTMLElement).inert,
    ),
  )).toBe(true);
});

test('student drawer keeps its final actions reachable at 320px and 200% zoom', async ({
  page,
}) => {
  await page.setViewportSize({ width: 320, height: 568 });
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    const data = pathname.endsWith('/auth/session')
      ? { user: studentUser, authorizationVersion: 1 }
      : pathname.includes('/student/shell-bootstrap')
        ? { unreadNotificationsCount: 3, balance: 0, points: 0 }
        : null;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data }),
    });
  });
  await installAuthAndGoto(
    page,
    'overlay-student-token',
    studentUser,
    `${appUrl}/student`,
  );
  await page.evaluate(() => {
    document.documentElement.style.zoom = '2';
  });

  await page.getByRole('button', { name: 'القائمة' }).click();
  const dialog = page.getByRole('dialog', { name: 'القائمة الجانبية' });
  await expect(dialog).toBeVisible();
  await dialog.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
  });
  await expect(dialog.getByRole('button', { name: 'تسجيل الخروج' }))
    .toBeVisible();
});
