import { expect, test } from '@playwright/test'; const api = 'http://api.lvh.me:5245/api/hr/governance';
test.describe('HR governance scope and rollout safety', () => {
  test('anonymous cannot dry-run, activate, rollback or export', async ({ request }) => { const responses = await Promise.all([request.post(`${api}/migration/dry-run`, { data: { module: 'people', sourceSystem: 'legacy', rows: [] } }), request.post(`${api}/migration/${crypto.randomUUID()}/activate`, { data: { module: 'people', reason: 'test' } }), request.post(`${api}/migration/rollback`, { data: { module: 'people', reason: 'test' } }), request.get(`${api}/reports/workforce/export?reason=test`)]); expect(responses.map((item) => item.status())).toEqual([401, 401, 401, 401]); });
  test('anonymous direct governance URLs are redirected or denied', async ({ page }) => { await page.goto('http://admin.lvh.me:3000/admin/hr/migration'); await expect(page.getByText('تشغيل Dry run', { exact: true })).toHaveCount(0); });
});
