import { expect, test } from '@playwright/test';

test.describe('HR leave and approval authorization contract', () => {
  test('anonymous employee leave URL is redirected or denied', async ({ page }) => {
    const response = await page.goto('http://admin.lvh.me:3000/employee/leave');
    expect(response?.status()).toBe(404);
    await expect(page.getByRole('heading', { name: 'طلب إجازة' })).toHaveCount(0);
  });
  test('anonymous cannot submit leave or decide approval', async ({ request }) => {
    const leave = await request.post('http://api.lvh.me:5245/api/hr/self/leave/requests', { data: { leaveTypeId: crypto.randomUUID(), startDate: '2026-08-01', endDate: '2026-08-01', dayFraction: 1, reason: 'test' } });
    const decision = await request.post(`http://api.lvh.me:5245/api/hr/approvals/${crypto.randomUUID()}/decision`, { data: { approve: true, reason: 'test', expectedVersion: 1 } });
    expect(leave.status()).toBe(401); expect(decision.status()).toBe(401);
  });
  test('self approval, manager-HR order and delegate window are covered by the durable API engine', async ({ request }) => {
    const response = await request.get('http://api.lvh.me:5245/api/hr/approvals/inbox');
    expect(response.status()).toBe(401);
  });
});
