import { expect, test } from '@playwright/test';
test('anonymous cannot mutate or enumerate refunds', async ({ request }) => {
  const responses = await Promise.all([request.get('http://api.lvh.me:5245/api/admin/platform-finance/refunds'), request.post('http://api.lvh.me:5245/api/admin/platform-finance/refunds', { data: {} })]);
  expect(responses.map(response => response.status())).toEqual([401, 401]);
});
