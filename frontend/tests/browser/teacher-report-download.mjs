import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const origin = process.env.TEACHER_REPORT_TEST_ORIGIN ?? 'http://admin.lvh.me:8738';
const teacherId = '848bd203-2316-43c1-90b6-061a3f85c245';

async function profile(browser, width, roles) {
  const page = await browser.newPage({ viewport: { width, height: 900 }, acceptDownloads: true });
  const user = { id: 'test-admin', fullName: 'أدمن الاختبار', roles, permissions: ['users.manage'], allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => { localStorage.setItem('accessToken', 'test-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
  const exports = [];
  let failDownload = true;
  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const headers = { 'access-control-allow-origin': origin, 'access-control-allow-credentials': 'true', 'access-control-allow-headers': '*' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    if (url.pathname.endsWith('/reports/detailed.pdf')) {
      exports.push(url);
      if (failDownload) { failDownload = false; return route.fulfill({ status: 503, headers, json: { message: 'تعذر تجهيز الكشف' } }); }
      return route.fulfill({ headers, contentType: 'application/pdf', body: '%PDF-1.7\n% local UI test\n%%EOF' });
    }
    if (url.pathname.endsWith('/auth/session')) return route.fulfill({ headers, json: { success: true, data: { user, authorizationVersion: 1 } } });
    if (url.pathname === `/api/admin/teachers/${teacherId}`) return route.fulfill({ headers, json: { success: true, data: { id: teacherId, userId: 'teacher-user', fullName: 'محمد فرحات', phoneNumber: '01000000000', subjectNames: [], subjectIds: [], packages: [] } } });
    if (url.pathname.endsWith('/stats')) return route.fulfill({ headers, json: { success: true, data: { packagesCount: 0, studentsCount: 0, activeStudentsCount: 0, packageSales: [] } } });
    if (url.pathname.includes('/teacher-finance-center/')) return route.fulfill({ status: 503, headers, json: { message: 'لوحة اختبار' } });
    return route.fulfill({ headers, json: { success: true, data: [] } });
  });
  await page.goto(`${origin}/admin/teachers/${teacherId}`);
  await page.getByRole('heading', { name: 'محمد فرحات', exact: true }).waitFor();
  return { page, exports };
}

for (const width of [390, 1280]) {
  test(`Admin selects exact dates, retries failed export and downloads teacher PDF at ${width}px (synthetic API)`, { timeout: 90000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const { page, exports } = await profile(browser, width, ['Admin']);
      await page.getByRole('button', { name: 'تنزيل كشف الحساب', exact: true }).click();
      const dialog = page.getByRole('dialog');
      await dialog.getByLabel('المدة', { exact: true }).selectOption('custom');
      await dialog.getByLabel('من يوم', { exact: true }).fill('2026-09-01');
      await dialog.getByLabel('لحد يوم', { exact: true }).fill('2026-09-30');
      await mkdir('../tmp/previews/teacher-report', { recursive: true });
      await dialog.screenshot({ path: `../tmp/previews/teacher-report/dates-${width}.png` });
      await dialog.getByRole('button', { name: 'تنزيل PDF', exact: true }).click();
      await dialog.getByRole('alert').filter({ hasText: 'تعذر تنزيل الكشف' }).waitFor();
      const downloaded = page.waitForEvent('download');
      await dialog.getByRole('button', { name: 'تنزيل PDF', exact: true }).click();
      const pdf = await downloaded;
      assert.match(pdf.suggestedFilename(), /محمد فرحات-2026-09-01-2026-09-30\.pdf$/);
      assert.equal(exports.length, 2);
      assert.equal(exports[1].searchParams.get('from'), '2026-09-01');
      assert.equal(exports[1].searchParams.get('to'), '2026-09-30');
      await page.getByRole('button', { name: 'تنزيل كشف الحساب', exact: true }).click();
      await dialog.getByLabel('المدة', { exact: true }).selectOption('beginning');
      const fullDownload = page.waitForEvent('download');
      await dialog.getByRole('button', { name: 'تنزيل PDF', exact: true }).click();
      await fullDownload;
      assert.equal(exports[2].searchParams.has('from'), false);
      assert.equal(exports[2].searchParams.get('to'), '2026-09-30');
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth), false);
    } finally { await browser.close(); }
  });
}

test('Staff can read teacher profile but cannot access report download (synthetic API)', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const { page, exports } = await profile(browser, 1280, ['Staff']);
    assert.equal(await page.getByRole('button', { name: 'تنزيل كشف الحساب', exact: true }).count(), 0);
    assert.equal(exports.length, 0);
  } finally { await browser.close(); }
});
