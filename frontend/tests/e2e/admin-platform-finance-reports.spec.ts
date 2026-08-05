import { expect, test } from '@playwright/test';
test('anonymous cannot export or close financial periods', async ({ request }) => {
  const responses = await Promise.all([request.get('http://api.lvh.me:5245/api/admin/platform-finance/exports/xlsx'), request.get('http://api.lvh.me:5245/api/admin/platform-finance/periods')]);
  expect(responses.map(response => response.status())).toEqual([401, 401]);
});
