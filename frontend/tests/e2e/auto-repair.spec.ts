import { expect, test } from '@playwright/test';
import { adminUrl, installAuthAndGoto } from './e2e-contract-helpers';

const admin = { id: '00000000-0000-4000-8000-000000000170', fullName: 'مدير الإصلاح', phone: '20000000170', roles: ['Admin'], permissions: [], profileComplete: true, allowedDomains: ['admin'], allowedNavbarItems: [], authorizationVersion: 1 };

// Synthetic HTTP contract verifies the report UI; PostgreSQL tests verify authority separately.
test('repair report binds approval to displayed source and keeps controls usable on a phone', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const hash = 'a'.repeat(64);
  let approved = false;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let data: unknown = null;
    if (path.endsWith('/auth/session')) data = { user: admin, authorizationVersion: 1 };
    else if (path.endsWith('/auto-repair/fix/decision')) {
      expect(route.request().postDataJSON()).toEqual({ action: 'approve', proposalHash: hash, confirmation: 'اعتماد aaaaaaaaaaaa' });
      approved = true;
    } else if (path.endsWith('/auto-repair/fix')) data = {
      id: 'fix', status: approved ? 'ready' : 'awaiting_approval', evidence: 'Null record', summary: 'تم إصلاح احتساب الاسترداد واختباره', proposalHash: hash, approvedHash: approved ? hash : '', releaseId: '',
      events: [{ id: 1, timestamp: new Date().toISOString(), status: 'testing', detail: 'نجح اختبار إعادة إنتاج المشكلة', actor: 'node-3' }],
    };
    else if (path.endsWith('/auto-repair')) data = {
      control: { paused: false, autoDeploy: true, heartbeat: new Date().toISOString(), runner: 'node-3' },
      incidents: [{ id: 'fix', source: 'backend', category: 'حساب الاسترداد', level: 'error', status: approved ? 'ready' : 'awaiting_approval', occurrences: 4, attempts: 1, firstSeen: new Date().toISOString(), lastSeen: new Date().toISOString(), summary: '', releaseId: '' }],
      counts: [{ status: 'awaiting_approval', count: 1 }], total: 1,
    };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data }) });
  });
  await installAuthAndGoto(page, 'repair-contract-token', admin, `${adminUrl}/admin/auto-repair`);
  await expect(page.getByRole('heading', { name: 'الإصلاح التلقائي', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'حساب الاسترداد', exact: true }).click();
  await expect(page.getByText('نجح اختبار إعادة إنتاج المشكلة')).toBeVisible();
  const approve = page.getByRole('button', { name: 'اعتماد الإصلاح والنشر', exact: true });
  await expect(approve).toBeDisabled();
  await page.getByRole('textbox', { name: 'اكتب: اعتماد aaaaaaaaaaaa' }).fill('اعتماد aaaaaaaaaaaa');
  await approve.click();
  await expect(page.getByRole('heading', { name: 'تفاصيل الإصلاح · جاهزة للنشر' })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('report.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
