import { expect, test, type Page } from '@playwright/test';
import { adminUrl, installAuthAndGoto, staffUrl } from './e2e-contract-helpers';

const giftId = '95200000-0000-0000-0000-000000000001';
const packageId = '95200000-0000-0000-0000-000000000002';
const studentId = '95200000-0000-0000-0000-000000000003';
const title = 'باقة هدية تجريبية';

async function installGiftSession(page: Page, surface: 'admin' | 'assistant', permission = true, navigation = true) {
  const root = `/${surface}/gifts`;
  const user = {
    id: '95200000-0000-0000-0000-000000000004', fullName: 'موظفة هدايا تجريبية', phone: '20000000952',
    roles: [surface === 'admin' ? 'Admin' : 'Assistant'], permissions: permission ? ['gifts.manage'] : [],
    allowedDomains: [surface], allowedNavbarItems: [`/${surface}/dashboard`, ...(navigation ? [root] : [])],
    profileComplete: true, authorizationVersion: 1,
  };
  const requests = { reads: 0, issued: null as Record<string, unknown> | null };
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let data: unknown = [];
    if (path.endsWith('/auth/session')) data = { user, authorizationVersion: 1 };
    if (path.endsWith('/admin/gifts')) {
      requests.reads++;
      if (route.request().method() === 'POST') {
        requests.issued = route.request().postDataJSON();
        data = { id: giftId, isReplay: false, recipients: [] };
      } else data = { items: [{ id: giftId, targetType: 'Package', targetName: title, status: 'Active', issuerName: user.fullName,
        recipientCount: 1, successfulCount: 1, issuedAt: '2026-11-26T12:00:00Z' }], totalCount: 1, page: 1, pageSize: 20, totalPages: 1 };
    }
    if (path.endsWith(`/admin/gifts/${giftId}`)) {
      requests.reads++;
      data = { id: giftId, targetType: 'Package', targetName: title, status: 'Active', issuerName: user.fullName,
        reason: 'تعويض مشكلة تشغيل', issuedAt: '2026-11-26T12:00:00Z', recipients: [
          { studentId, studentName: 'طالبة تجريبية', status: 'Granted', usesConsumed: 0, outcomeMessage: 'تمت الإضافة' },
        ] };
    }
    if (path.endsWith('/gifts/lookups/students')) data = [{ id: studentId, name: 'طالبة تجريبية' }];
    if (path.endsWith('/gifts/lookups/targets')) data = [{ id: packageId, name: title }];
    if (path.endsWith('/public/settings')) return route.fulfill({ json: { maintenanceMode: false } });
    return route.fulfill({ json: { success: true, data } });
  });
  await installAuthAndGoto(page, 'synthetic-gift-staff-token', user, `${surface === 'admin' ? adminUrl : staffUrl}${root}`);
  return requests;
}

for (const surface of ['assistant', 'admin'] as const) {
  test(`${surface} can review and issue gifts without leaving its workspace (2026-11-26)`, async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    const requests = await installGiftSession(page, surface);
    const root = `/${surface}/gifts`;
    const origin = surface === 'admin' ? adminUrl : staffUrl;
    await expect(page.getByRole('heading', { name: 'الهدايا والوصول المجاني', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'الهدايا', exact: true }).first()).toHaveAttribute('href', root);
    await page.getByRole('link', { name: `فتح هدية ${title}` }).click();
    await expect(page).toHaveURL(`${origin}${root}/${giftId}`);
    await expect(page.getByText('تعويض مشكلة تشغيل', { exact: true })).toBeVisible();
    await page.goBack();
    await page.getByRole('link', { name: 'هدية جديدة', exact: true }).click();
    await expect(page).toHaveURL(`${origin}${root}/new`);
    await page.getByRole('combobox').selectOption(packageId);
    await page.getByRole('button', { name: 'طالبة تجريبية', exact: true }).click();
    await page.getByLabel('سبب الهدية').fill('تعويض مشكلة تشغيل');
    await page.getByRole('button', { name: 'إصدار الهدية', exact: true }).click();
    await expect(page).toHaveURL(`${origin}${root}/${giftId}`);
    expect(requests.issued).toMatchObject({ targetType: 'Package', targetId: packageId, studentIds: [studentId], reason: 'تعويض مشكلة تشغيل' });
    await expect(page.getByText('طالبة تجريبية', { exact: true })).toBeVisible();
  });
}

for (const denied of [{ permission: false, navigation: true }, { permission: true, navigation: false }]) {
  test(`assistant without gift ${denied.permission ? 'navigation' : 'permission'} cannot open gift pages`, async ({ page }) => {
    const requests = await installGiftSession(page, 'assistant', denied.permission, denied.navigation);
    await expect(page.getByText('404', { exact: true }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: 'الهدايا', exact: true })).toHaveCount(0);
    for (const suffix of ['/new', `/${giftId}`]) {
      await page.goto(`${staffUrl}/assistant/gifts${suffix}`);
      await expect(page.getByText('404', { exact: true }).first()).toBeVisible();
    }
    expect(requests.reads).toBe(0);
  });
}
