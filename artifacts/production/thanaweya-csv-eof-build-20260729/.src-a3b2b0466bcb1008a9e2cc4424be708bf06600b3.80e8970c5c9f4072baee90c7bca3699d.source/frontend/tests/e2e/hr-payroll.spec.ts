import { expect, test } from '@playwright/test';
const api = 'http://api.lvh.me:5245/api';
test.describe('Employee payroll authorization isolation', () => {
  test('anonymous cannot view payroll, prepare, finance-review or final-approve', async ({ request }) => {
    const calls = await Promise.all([
      request.get(`${api}/hr/payroll/self/payslips`), request.post(`${api}/hr/payroll/runs/prepare`, { data: {} }),
      request.post(`${api}/hr/payroll/runs/${crypto.randomUUID()}/finance-review`, { data: { expectedVersion: 1 } }),
      request.post(`${api}/hr/payroll/runs/${crypto.randomUUID()}/gm-approve`, { data: { expectedVersion: 1 } }),
    ]); expect(calls.map((item) => item.status())).toEqual([401, 401, 401, 401]);
  });
  test('teacher finance route remains separate from employee payroll', async ({ request }) => {
    const teacher = await request.get(`${api}/teacher/finance/account`); const employee = await request.get(`${api}/hr/payroll/runs`);
    expect(teacher.status()).toBe(401); expect(employee.status()).toBe(401);
  });
});
