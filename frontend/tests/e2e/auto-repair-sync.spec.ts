import { test, expect } from '@playwright/test';

test('Phase 1: synchronization report distinguishes pending, ready, and stale evidence', async ({ page }) => {
  await page.goto('http://admin.lvh.me:3000/login');
  await page.getByRole('button', { name: 'فهمت، متابعة لتسجيل الدخول' }).click();
  await page.fill('input[name="phoneNumber"]', '20000000000');
  await page.fill('input[name="password"]', 'password');
  await page.click('button[type="submit"]');
  await expect(page.locator('h1')).toContainText('الرئيسية', { timeout: 30_000 });
  let state = 'pending_release';
  let checkedAt = new Date().toISOString();
  await page.route('**/api/admin/auto-repair?*', route => route.fulfill({ json: { data: {
    control: { paused: false, autoDeploy: true, heartbeat: new Date().toISOString(), runner: 'node-3' },
    incidents: [], counts: [], total: 0, lastSynchronized: null,
    synchronization: { checkedAt, snapshot: { state, sharedCommit: 'a'.repeat(40), nodes: [
      { nodeId: 'node-1', releaseId: `git-${'b'.repeat(40)}` },
      { nodeId: 'node-2', releaseId: `git-${'b'.repeat(40)}` },
      { nodeId: 'node-3', releaseId: `git-${'b'.repeat(40)}` },
    ] } },
  } } }));
  await page.goto('http://admin.lvh.me:3000/admin/auto-repair');
  const panel = page.getByRole('region', { name: 'مزامنة المصدر' });
  await expect(panel).toContainText('بانتظار تأكيد الإصدار المنشور');
  await panel.getByText('تفاصيل الإصدارات وقت الفحص').click();
  await expect(panel).toContainText('node-3');
  state = 'release_failed';
  await page.getByRole('button', { name: 'تحديث التقرير' }).click();
  await expect(panel).toContainText('النشر فشل ويحتاج مراجعة');
  state = 'ready';
  await page.getByRole('button', { name: 'تحديث التقرير' }).click();
  await expect(panel).toContainText('المصدر متزامن مع السيرفرات');
  checkedAt = new Date(Date.now() - 4 * 60_000).toISOString();
  await page.getByRole('button', { name: 'تحديث التقرير' }).click();
  await expect(panel).toContainText('حالة المزامنة غير مؤكدة');
  await expect(panel).not.toContainText('المصدر متزامن مع السيرفرات');
});

test('Phase 1: work list archives dismissed cases with a reason and allows reopening', async ({ page }) => {
  await page.goto('http://admin.lvh.me:3000/login');
  await page.getByRole('button', { name: 'فهمت، متابعة لتسجيل الدخول' }).click();
  await page.fill('input[name="phoneNumber"]', '20000000000');
  await page.fill('input[name="password"]', 'password');
  await page.click('button[type="submit"]');
  await expect(page.locator('h1')).toContainText('الرئيسية', { timeout: 30_000 });
  let status = 'queued';
  const reason = 'فحص معروف لا يحتاج تغيير في الكود';
  await page.route('**/api/admin/auto-repair**', async route => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/case/decision')) {
      const decision = route.request().postDataJSON();
      if (decision.action === 'dismiss') expect(decision.reason).toBe(reason);
      status = decision.action === 'dismiss' ? 'dismissed' : 'queued';
      await route.fulfill({ json: { data: {} } });
    } else if (url.pathname.endsWith('/case')) {
      await route.fulfill({ json: { data: { id: 'case', status, evidence: 'Synthetic fixture', summary: status === 'dismissed' ? reason : '', proposalHash: '', approvedHash: '', releaseId: '', events: [] } } });
    } else {
      const filter = url.searchParams.get('status');
      const visible = filter === 'archive' ? status === 'dismissed' : status === 'queued';
      await route.fulfill({ json: { data: {
        control: { paused: false, autoDeploy: true, heartbeat: new Date().toISOString(), runner: 'node-3' },
        incidents: visible ? [{ id: 'case', source: 'gateway', category: 'حالة اختبار الأرشفة', level: 'warning', status, occurrences: 12, attempts: 0, firstSeen: new Date().toISOString(), lastSeen: new Date().toISOString() }] : [],
        counts: [{ status, count: 1 }], total: visible ? 1 : 0,
      } } });
    }
  });
  await page.goto('http://admin.lvh.me:3000/admin/auto-repair');
  await expect(page.getByRole('combobox', { name: 'الحالة' })).toHaveValue('active');
  await page.getByRole('button', { name: 'حالة اختبار الأرشفة' }).click();
  const dismiss = page.getByRole('button', { name: 'استبعاد ونقل للسجل' });
  await expect(dismiss).toBeDisabled();
  await page.getByRole('textbox', { name: 'سبب الاستبعاد من قائمة العمل' }).fill(reason);
  await dismiss.click();
  await expect(page.getByRole('region', { name: 'قائمة المشاكل' })).not.toContainText('حالة اختبار الأرشفة');
  await page.getByRole('combobox', { name: 'الحالة' }).selectOption('archive');
  await expect(page.getByRole('region', { name: 'قائمة المشاكل' })).toContainText('حالة اختبار الأرشفة');
  await expect(page.getByRole('region', { name: 'تفاصيل الإصلاح' })).toContainText(reason);
  await page.getByRole('button', { name: 'إعادة التشخيص والمحاولة' }).click();
  await page.getByRole('combobox', { name: 'الحالة' }).selectOption('active');
  await expect(page.getByRole('region', { name: 'قائمة المشاكل' })).toContainText('حالة اختبار الأرشفة');
});
