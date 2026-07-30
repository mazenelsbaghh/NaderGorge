import { expect, test } from '@playwright/test';
const api = 'http://api.lvh.me:5245/api/hr/payroll';
test.describe('HR financial request evidence and scope', () => {
  test('anonymous cannot submit or list requests', async ({ request }) => {
    const list = await request.get(`${api}/self/financial-requests`);
    const submit = await request.post(`${api}/self/financial-requests`, { data: { type: 'Loan', amount: 1000, installments: 2, reason: 'test', attachmentReference: 'proof.pdf' } });
    expect(list.status()).toBe(401); expect(submit.status()).toBe(401);
  });
  test('anonymous cannot approve or inject duplicate payroll sources', async ({ request }) => {
    const approval = await request.post(`${api}/financial-requests/${crypto.randomUUID()}/approve`, { data: { firstDueDate: '2026-08-01', expectedVersion: 1 } });
    expect(approval.status()).toBe(401);
  });
});
