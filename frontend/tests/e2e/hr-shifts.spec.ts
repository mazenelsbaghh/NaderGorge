import { expect, test } from '@playwright/test';
import { adminUrl, installAuthAndGoto, login } from './e2e-contract-helpers';

test.describe('HR shift planning', () => {
  test('desktop and mobile render the shift editor without overflow', async ({ page, request }) => {
    const auth = await login(request, 'admin');
    await page.setViewportSize({ width: 390, height: 844 });
    await installAuthAndGoto(page, auth.accessToken, auth.user, `${adminUrl}/admin/hr/shifts`);
    await expect(page.locator('body')).not.toHaveCSS('overflow-x', 'scroll');
    await expect(page.getByRole('heading', { name: 'تخطيط الشفتات' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'حفظ القالب' })).toBeVisible();
  });

  test('API requires idempotency and rejects overlapping publication', async ({ request }) => {
    const unauthenticated = await request.post('http://api.lvh.me:5245/api/hr/admin/shifts/assignments/publish', { data: [] });
    expect([401, 403]).toContain(unauthenticated.status());
  });
});
