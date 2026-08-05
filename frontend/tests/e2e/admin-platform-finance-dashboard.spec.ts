import { expect, test } from '@playwright/test';

const api = 'http://api.lvh.me:5245/api/admin/platform-finance';
test('anonymous cannot read the platform finance dashboard or ledger', async ({ request }) => {
  const responses = await Promise.all([request.get(`${api}/dashboard`), request.get(`${api}/ledger`), request.get(`${api}/journals/${crypto.randomUUID()}`)]);
  expect(responses.map(response => response.status())).toEqual([401, 401, 401]);
});
