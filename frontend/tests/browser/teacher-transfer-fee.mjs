import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const origin = process.env.TEACHER_TRANSFER_TEST_ORIGIN ?? 'http://admin.lvh.me:8738';
const teacherId = '848bd203-2316-43c1-90b6-061a3f85c245';
const settlementId = '12345678-1234-1234-1234-123456789abc';

for (const width of [390, 1280]) {
  test(`Transfer shows server fee, recovers failed preview and submits net cash at ${width}px (synthetic API)`, { timeout: 90000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    let page;
    const requests = [];
    try {
      page = await browser.newPage({ viewport: { width, height: 1000 } });
      page.setDefaultTimeout(20000);
      const user = { id: 'test-admin', fullName: 'أدمن الاختبار', roles: ['Admin'], permissions: ['finance.manage', 'users.manage'],
        allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'synthetic-test-token'); localStorage.setItem('user', JSON.stringify(user));
      }, user);
      const settlement = { id: settlementId, teacherId, status: 'Approved', periodFrom: '2026-09-01T00:00:00Z',
        periodTo: '2026-09-30T23:59:59Z', grossDueAmount: 80, debtDeductionAmount: 0, netPayableAmount: 80, lines: [], payments: [] };
      let failPreview = true;
      const payments = [];
      await page.route('**/api/**', async route => {
        const request = route.request(); const url = new URL(request.url());
        const path = url.pathname.replace(/^\/api/, '');
        requests.push({ path, method: request.method() });
        const ok = data => route.fulfill({ json: { success: true, data } });
        if (path === '/auth/session') return ok({ user, authorizationVersion: 1 });
        if (path === `/admin/teachers/${teacherId}`) return ok({ id: teacherId, userId: 'teacher-user', fullName: 'مدرّس الاختبار', subjectIds: [], subjectNames: [], packages: [] });
        if (path.endsWith('/payment-preview')) {
          if (failPreview) return route.fulfill({ status: 503, json: { message: 'Test unavailable' } });
          const vodafone = url.searchParams.get('paymentMethod') === 'فودافون كاش';
          await new Promise(resolve => setTimeout(resolve, 300));
          return ok({ teacherAmount: 80, platformShareBasis: vodafone ? 20 : 0, feeRate: vodafone ? 1.5 : 0,
            transferFee: vodafone ? 0.3 : 0, netTransferAmount: vodafone ? 79.7 : 80 });
        }
        if (path.endsWith(`/settlements/${settlementId}/pay`)) {
          payments.push(request.postDataJSON()); settlement.status = 'Paid'; settlement.netPayableAmount = 79.7;
          settlement.payments = [{ amount: 79.7 }]; return ok(true);
        }
        if (path.endsWith(`/settlements/${settlementId}`)) return ok(settlement);
        if (path.endsWith('/settlements')) return ok({ items: [settlement], total: 1, page: 1, pageSize: 20 });
        if (path.endsWith('/ledger')) return ok({ items: [], total: 0, page: 1, pageSize: 100 });
        if (path.endsWith('/statement')) return route.fulfill({ status: 503, json: { message: 'Unused statement in UI test' } });
        if (path.endsWith('/collections')) return ok({ items: [], total: 0, page: 1, pageSize: 20 });
        return ok([]);
      });
      await page.goto(`${origin}/admin/teachers/${teacherId}/account`);
      await page.getByText('تسجيل تحويل للمدرس أو مرتجع', { exact: true }).click();
      await page.getByRole('button', { name: 'فتح التسوية', exact: true }).click();
      const dialog = page.getByRole('dialog');
      const submit = dialog.getByRole('button', { name: /^تسجيل تحويل/ });
      await dialog.getByRole('alert').filter({ hasText: 'تعذر حساب مبلغ التحويل' }).waitFor();
      assert.equal(await submit.isDisabled(), true);
      failPreview = false;
      await dialog.getByRole('button', { name: 'جرّب تاني' }).click();
      await dialog.getByText('79.70 ج.م', { exact: true }).waitFor();
      assert.equal(await submit.isDisabled(), false);
      await dialog.getByLabel('طريقة التحويل', { exact: true }).selectOption('bank');
      assert.equal(await submit.isDisabled(), true);
      await dialog.locator('dd').filter({ hasText: '80.00 ج.م' }).last().waitFor();
      assert.equal(await dialog.getByText(/عمولة فودافون كاش —/).count(), 0);
      await dialog.getByLabel('طريقة التحويل', { exact: true }).selectOption('فودافون كاش');
      await dialog.getByText('79.70 ج.م', { exact: true }).waitFor();
      await dialog.getByLabel('مرجع التحويل', { exact: true }).fill('LOCAL-TEST-1');
      assert.ok(await dialog.getByText('1.5٪', { exact: false }).isVisible());
      assert.ok(await dialog.getByText('0.30 ج.م', { exact: true }).isVisible());
      await mkdir('../artifacts/changes/vodafone-teacher-transfer-fee-20261003', { recursive: true });
      await dialog.screenshot({ path: `../artifacts/changes/vodafone-teacher-transfer-fee-20261003/payment-${width}.png` });
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await submit.click();
      await dialog.getByText(/تم تسجيل الدفع/).waitFor();
      assert.deepEqual(payments, [{ paymentMethod: 'فودافون كاش', amount: 79.7, transferReference: 'LOCAL-TEST-1' }]);
      assert.equal(await dialog.getByLabel('طريقة التحويل', { exact: true }).count(), 0);
    } catch (error) {
      await mkdir('../artifacts/changes/vodafone-teacher-transfer-fee-20261003', { recursive: true });
      await writeFile(`../artifacts/changes/vodafone-teacher-transfer-fee-20261003/browser-failure-${width}.json`,
        JSON.stringify({ requests, text: page ? await page.locator('body').innerText() : '' }, null, 2));
      throw error;
    } finally { await browser.close(); }
  });
}
