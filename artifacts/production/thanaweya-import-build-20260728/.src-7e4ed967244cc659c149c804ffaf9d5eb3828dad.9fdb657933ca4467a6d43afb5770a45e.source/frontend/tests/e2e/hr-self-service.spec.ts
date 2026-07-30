import { expect, test } from '@playwright/test';
const api = 'http://api.lvh.me:5245/api/hr';
test.describe('Employee documents and asset custody authorization', () => {
  test('anonymous direct employee hub is redirected or denied', async ({ page }) => {
    const response = await page.goto('http://admin.lvh.me:3000/employee');
    expect(response?.status()).toBe(404);
    await expect(page.getByText('بوابة الموظف')).toHaveCount(0);
  });
  test('anonymous cannot enumerate, download cross-employee documents or inspect assets', async ({ request }) => {
    const responses = await Promise.all([request.get(`${api}/self/documents`), request.get(`${api}/self/documents/${crypto.randomUUID()}/download`), request.get(`${api}/self/assets`)]);
    expect(responses.map((item) => item.status())).toEqual([401, 401, 401]);
  });
  test('offboarding asset blocker is an admin-only endpoint', async ({ request }) => {
    const response = await request.get(`${api}/admin/assets/offboarding-check/${crypto.randomUUID()}`); expect(response.status()).toBe(401);
  });
});
