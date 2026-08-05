import { expect, test } from '@playwright/test';
test('anonymous cannot create or read budget actuals', async ({ request }) => {
  const responses = await Promise.all([request.post('http://api.lvh.me:5245/api/admin/platform-finance/budgets', { data: {} }), request.get('http://api.lvh.me:5245/api/admin/platform-finance/budgets/actuals')]);
  expect(responses.map(response => response.status())).toEqual([401, 401]);
});
