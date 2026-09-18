import { expect, test } from '@playwright/test';

const payload = {
  generatedAt: '2026-09-18T21:00:00Z', earliestDate: '2026-01-01',
  platform: { from: '2026-09-01', to: '2026-09-19', revenue: 500, refunds: 20, expenses: 100, netProfit: 380, accounts: [] },
  teachers: [
    { period: { teacherId: 'first', teacherName: 'المدرس الأول', grossSales: 1000, teacherShare: 600, platformShare: 350, refunds: 50, paid: 200, outstanding: 400 }, currentCalculatedBalance: 400, currentAccountBalance: 400, currentLedgerBalance: 400, reconciliationDifference: 0 },
    { period: { teacherId: 'second', teacherName: '=2+2', grossSales: 300, teacherShare: 150, platformShare: 130, refunds: 20, paid: 0, outstanding: 150 }, currentCalculatedBalance: 150, currentAccountBalance: 100, currentLedgerBalance: 150, reconciliationDifference: 50 },
  ],
};

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('accessToken', 'test-admin-token');
    localStorage.setItem('user', JSON.stringify({ id: 'test-admin', fullName: 'Admin', roles: ['Admin'], permissions: [], profileComplete: true, allowedDomains: ['admin'], allowedNavbarItems: [] }));
  });
  await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
});

test('dedicated profit report filters teachers, exposes mismatches, and exports safe complete CSV', async ({ page }) => {
  let requestedDates = '';
  await page.route('**/api/admin/platform-finance/profits?*', route => {
    requestedDates = new URL(route.request().url()).search;
    return route.fulfill({ json: payload });
  });
  await page.goto('/admin/platform-profits');
  await expect(page.getByRole('heading', { name: 'أرباح المنصّة', exact: true })).toBeVisible();
  await expect(page.getByText('صافي ربح المبيعات', { exact: true }).locator('../..')).toContainText('380.00');
  await expect(page.getByText('أرصدة الحسابات المسجّلة تحتاج مطابقة لعدد 1 من المدرسين.')).toBeVisible();
  await page.getByLabel('من', { exact: true }).fill('2026-09-01');
  await page.getByLabel('إلى', { exact: true }).fill('2026-09-19');
  await page.getByRole('button', { name: 'عرض التقرير', exact: true }).click();
  await expect(page.getByRole('table')).toBeVisible();
  expect(requestedDates).toContain('from=2026-09-01');
  expect(requestedDates).toContain('to=2026-09-19');
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'تصدير التقرير CSV' }).click();
  const stream = await (await downloadPromise).createReadStream();
  const chunks = [];
  for await (const chunk of stream!) chunks.push(Buffer.from(chunk));
  const csv = Buffer.concat(chunks).toString('utf8');
  expect(csv).toContain('"\'=2+2"');
  expect(csv).toContain('"صافي الربح المسجل","380"');
  expect(csv).toContain('"المدرس الأول","1000","600","350","50","200"');
  await page.getByRole('combobox', { name: 'المدرّس', exact: true }).selectOption('second');
  await expect(page.getByRole('table')).not.toContainText('المدرس الأول');
  await page.getByRole('button', { name: 'تفاصيل =2+2' }).click();
  const detail = page.getByRole('region', { name: 'حساب =2+2' });
  await expect(detail).toContainText('50.00');
  await expect(detail.getByRole('link')).toHaveAttribute('href', '/admin/teachers/second/account');
});

test('failed profit report does not present zero earnings and can be retried on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  let failing = true;
  await page.route('**/api/admin/platform-finance/profits?*', route => failing
    ? route.fulfill({ status: 503, json: { message: 'Unavailable' } }) : route.fulfill({ json: payload }));
  await page.goto('/admin/platform-profits');
  await expect(page.getByRole('alert').filter({ hasText: 'تعذر تحميل تقرير الأرباح' })).toBeVisible();
  await expect(page.getByRole('table')).toHaveCount(0);
  failing = false;
  await page.getByRole('button', { name: 'إعادة المحاولة' }).click();
  await expect(page.getByRole('table')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: '/tmp/platform-profits-mobile.png', fullPage: true });
});
