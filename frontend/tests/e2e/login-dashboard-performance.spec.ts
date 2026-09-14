import { expect, test, type Page } from '@playwright/test';

import { appUrl } from './e2e-contract-helpers';

const studentUser = {
  id: 'e2e-login-performance-student',
  fullName: 'طالب قياس الأداء',
  phone: '20000000888',
  roles: ['Student'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['student'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};

const dashboard = {
  studentName: studentUser.fullName,
  activePackages: [],
  upcomingExams: [],
  upcomingHomeworks: [],
  overallProgressPercent: 0,
  totalLessonsCompleted: 0,
  totalLessons: 0,
  codesRedeemed: 0,
};

function apiPayload(data: unknown) {
  return JSON.stringify({ success: true, data });
}

async function installStudentLoginApi(page: Page, calls: Map<string, number>) {
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    calls.set(pathname, (calls.get(pathname) ?? 0) + 1);

    if (pathname === '/auth/login') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: apiPayload({
          accessToken: 'e2e-login-performance-token',
          user: studentUser,
        }),
      });
      return;
    }

    if (pathname === '/auth/session') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: apiPayload({
          user: studentUser,
          authorizationVersion: studentUser.authorizationVersion,
        }),
      });
      return;
    }

    if (pathname === '/student/dashboard') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: apiPayload(dashboard),
      });
      return;
    }

    if (pathname === '/student/dashboard/quick-access') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: apiPayload([]),
      });
      return;
    }

    if (pathname === '/student/shell-bootstrap') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: apiPayload({
          unreadNotificationsCount: 0,
          currentBalance: 0,
          gamification: {
            totalPoints: 0,
            currentStreakCount: 0,
            longestStreakCount: 0,
            levelName: '',
          },
          themePreferences: {},
          hasSeenTrackingCodePopup: true,
        }),
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: apiPayload(null),
    });
  });
}

test('login reaches one student shell without duplicate eligible reads', async ({
  page,
}) => {
  const calls = new Map<string, number>();
  await installStudentLoginApi(page, calls);
  await page.goto(`${appUrl}/login`);

  await page.locator('#login-phone').fill('20000000888');
  await page.locator('#login-password').fill('password');
  await page.getByRole('button', { name: 'تسجيل الدخول' }).click();

  await expect(page).toHaveURL(`${appUrl}/student`);
  await expect(page.locator('#main-content')).toHaveCount(1);
  await expect(page.getByText('بوابة الطالب').first()).toBeVisible();
  await expect(page.locator('.auth-shell')).toHaveCount(0);

  for (const pathname of [
    '/auth/login',
    '/student/dashboard',
    '/student/dashboard/quick-access',
    '/student/shell-bootstrap',
  ]) {
    expect(calls.get(pathname), `${pathname} must not be duplicated`).toBe(1);
  }
  expect(calls.get('/auth/session') ?? 0).toBeLessThanOrEqual(1);
});
