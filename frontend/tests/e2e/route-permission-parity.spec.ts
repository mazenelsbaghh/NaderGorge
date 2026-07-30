import { expect, test, type Page } from '@playwright/test';

import { adminUrl, installAuthAndGoto } from './e2e-contract-helpers';

const scopedStaffUser = {
  id: 'e2e-route-permission-staff',
  fullName: 'E2E Scoped Staff',
  phone: '20000000999',
  roles: ['Assistant'],
  permissions: ['users.manage'],
  profileComplete: true,
  allowedDomains: ['admin'],
  allowedNavbarItems: ['/admin/students'],
  authorizationVersion: 1,
};

const deniedAdminRoutes = [
  '/admin/settings',
  '/admin/content/video-types',
  '/admin/unmapped-speed-probe',
];

async function installScopedSession(page: Page) {
  await page.route('**/api/**', async (route) => {
    if (new URL(route.request().url()).pathname.endsWith('/api/auth/session')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            user: scopedStaffUser,
            authorizationVersion: scopedStaffUser.authorizationVersion,
          },
        }),
      });
      return;
    }

    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Not needed by the route-permission scenario.' }),
    });
  });

  await installAuthAndGoto(
    page,
    'e2e-route-permission-token',
    scopedStaffUser,
    `${adminUrl}/admin/students`
  );
}

test.describe('admin role-route and navigation parity', () => {
  test('scoped staff cannot see or directly open denied admin routes', async ({ page }) => {
    await installScopedSession(page);

    await expect(page).toHaveURL(`${adminUrl}/admin/students`);
    await expect(page.getByRole('link', { name: 'الطلاب', exact: true })).toBeVisible();

    for (const pathname of deniedAdminRoutes) {
      await expect(page.locator(`a[href="${pathname}"]`)).toHaveCount(0);
    }

    for (const pathname of deniedAdminRoutes) {
      await page.goto(`${adminUrl}${pathname}`);

      await expect(page).toHaveURL(`${adminUrl}/admin/unauthorized`);
      await expect(page.getByRole('heading', { name: 'غير مصرح بالدخول' })).toBeVisible();
    }
  });
});
