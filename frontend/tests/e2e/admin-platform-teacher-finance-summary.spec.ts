import { expect, test } from '@playwright/test';
test('anonymous cannot read teacher finance summary', async ({ request }) => {
  const response = await request.get('http://api.lvh.me:5245/api/admin/platform-finance/teachers/summary');
  expect(response.status()).toBe(401);
});
