import { expect, test } from '@playwright/test'; const api = 'http://api.lvh.me:5245/api/hr';
test.describe('Performance and confidential employee cases', () => {
  test('anonymous cannot read cycles, confidential cases or publish reviews', async ({ request }) => { const responses = await Promise.all([request.get(`${api}/admin/performance/cycles`), request.get(`${api}/admin/cases`), request.post(`${api}/admin/performance/reviews`, { data: {} })]); expect(responses.map((item) => item.status())).toEqual([401, 401, 401]); });
  test('payroll penalty link is restricted and never exposed to anonymous callers', async ({ request }) => { const response = await request.post(`${api}/admin/cases/actions/${crypto.randomUUID()}/apply-payroll/${crypto.randomUUID()}`); expect(response.status()).toBe(401); });
});
