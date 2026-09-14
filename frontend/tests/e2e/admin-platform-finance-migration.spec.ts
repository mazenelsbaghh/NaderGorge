import { expect, test } from '@playwright/test';
test('anonymous cannot preview or post historical finance migration', async ({ request }) => {
  const responses = await Promise.all([request.get('http://api.lvh.me:5245/api/admin/platform-finance/migration/preview'), request.post('http://api.lvh.me:5245/api/admin/platform-finance/migration/post')]);
  expect(responses.map(response => response.status())).toEqual([401, 401]);
});
