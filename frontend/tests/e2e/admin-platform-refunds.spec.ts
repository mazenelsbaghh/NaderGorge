import { expect, test } from '@playwright/test';

test('admin can open refunds and see amount, operator and reason', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('accessToken', 'e2e-admin-token');
    localStorage.setItem('user', JSON.stringify({
      id: 'admin-refunds', fullName: 'Admin', roles: ['Admin'], permissions: [],
      profileComplete: true, allowedDomains: ['admin'], allowedNavbarItems: ['/admin/gifts'],
    }));
  });
  await page.route('**/api/admin/wallets**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) }));
  await page.route('**/api/admin/platform-finance/refunds/bootstrap**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ treasuryAccounts: [] }) }));
  await page.route('**/api/admin/platform-finance/refunds', route => route.fulfill({
    status: 200, contentType: 'application/json', body: JSON.stringify([{
      id: '11111111-1111-1111-1111-111111111111', originalSourceId: '22222222-2222-2222-2222-222222222222',
      originalSourceType: 'PurchaseOperation', studentId: '33333333-3333-3333-3333-333333333333',
      studentName: 'طالب تجريبي', studentPhoneNumber: '01000000001', processedByUserId: '44444444-4444-4444-4444-444444444444',
      processedByName: 'الموظف أحمد', platformAmount: 80, teacherAmount: 20, totalAmount: 100,
      method: 2, status: 2, reason: 'رد مصروف الحصة', createdAt: '2026-09-30T10:00:00Z', isHistorical: false,
    }]),
  }));

  await page.goto('http://admin.lvh.me:8738/admin');
  await page.getByRole('link', { name: /استردادات الطلاب/ }).click();
  await expect(page).toHaveURL(/\/admin\/platform-finance\/refunds$/);
  await expect(page.getByRole('columnheader', { name: 'المبلغ المسترد' })).toBeVisible();
  await expect(page.getByText('100.00 ج.م')).toBeVisible();
  await expect(page.getByText('الموظف أحمد')).toBeVisible();
  await expect(page.getByText('رد مصروف الحصة')).toBeVisible();
});

test('anonymous cannot mutate or enumerate refunds', async ({ request }) => {
  const responses = await Promise.all([request.get('http://api.lvh.me:5245/api/admin/platform-finance/refunds'), request.post('http://api.lvh.me:5245/api/admin/platform-finance/refunds', { data: {} })]);
  expect(responses.map(response => response.status())).toEqual([401, 401]);
});
