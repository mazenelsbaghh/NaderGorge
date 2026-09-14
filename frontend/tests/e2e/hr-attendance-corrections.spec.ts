import { expect, test } from '@playwright/test';
test.describe('Attendance correction authorization', () => {
  test('anonymous direct reviewer URL is redirected or denied', async ({ page }) => {
    await page.goto('http://admin.lvh.me:3000/admin/hr/attendance-corrections');
    await expect(page.getByText('تصحيحات الحضور', { exact: true })).toHaveCount(0);
  });
  test('anonymous correction API is denied', async ({ request }) => {
    const response = await request.post('http://api.lvh.me:5245/api/hr/self/attendance/corrections', { data: { attendanceSessionId: crypto.randomUUID(), reason: 'test' } });
    expect(response.status()).toBe(401);
  });
});
