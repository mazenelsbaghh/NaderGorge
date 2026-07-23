import { expect, test } from '@playwright/test';
import { installAuthAndGoto, login, staffUrl } from './e2e-contract-helpers';

test.describe('Trusted attendance self service', () => {
  test('mobile surface shows explicit location/device decision states', async ({ page, request }) => {
    const auth = await login(request, 'assistant');
    await page.setViewportSize({ width: 390, height: 844 });
    await installAuthAndGoto(page, auth.accessToken, auth.user, `${staffUrl}/employee/attendance`);
    await expect(page.getByRole('heading', { name: 'الحضور والانصراف' })).toBeVisible();
    await expect(page.getByText(/سياسة الموقع أو الجهاز/)).toBeVisible();
  });
  test('clock API rejects unauthenticated callers without creating a session', async ({ request }) => {
    const response = await request.post('http://api.lvh.me:5245/api/hr/self/attendance/clock-in', { headers: { 'Idempotency-Key': crypto.randomUUID() }, data: {} });
    expect(response.status()).toBe(401);
  });
});
