import { expect, test } from '@playwright/test';
import { adminUrl, installAuthAndGoto } from './e2e-contract-helpers';

const api = 'http://api.lvh.me:5245/api';
const e2eHeaders = { 'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345' };

test.describe('HR atomic employee provisioning', () => {
  test('authorized admin creates one employee account and profile from the same form', async ({ page, request }) => {
    const login = await request.post(`${api}/auth/login`, {
      headers: { 'X-App-Surface': 'admin' },
      data: { phoneNumber: '20000000000', password: 'password', deviceFingerprint: `hr-provision-${Date.now()}` },
    });
    expect(login.ok()).toBeTruthy();
    const payload = await login.json();
    const session = payload.data ?? payload;
    await installAuthAndGoto(page, session.accessToken, session.user, `${adminUrl}/admin/assistants`);
    await page.getByRole('button', { name: /إضافة مساعد جديد/ }).click();
    const uniquePhone = `010${String(Date.now()).slice(-8)}`;
    await page.getByLabel('الاسم الكامل').fill('موظف دعم تجريبي');
    await page.getByLabel('رقم الهاتف').fill(uniquePhone);
    await page.getByLabel('كلمة السر').fill('Secret123!');
    await page.getByLabel('الراتب الأساسي').fill('7500');
    await page.getByLabel('الساعات اليومية').fill('8');
    await page.getByRole('button', { name: /إضافة المستخدم/ }).click();
    await expect(page.getByText(/تم إنشاء حساب/)).toBeVisible();

    await page.goto(`${adminUrl}/admin/hr/organization`, { waitUntil: 'domcontentloaded' });
    await page.getByLabel('بحث في الموظفين').fill(uniquePhone);
    await expect(page.getByText('موظف دعم تجريبي')).toBeVisible();

    const users = await request.get(`${api}/e2e/users`, { headers: e2eHeaders });
    expect(users.ok()).toBeTruthy();
    const matches = (await users.json()).filter((item: { phoneNumber: string }) => item.phoneNumber === uniquePhone);
    expect(matches).toHaveLength(1);
  });

  test('request without employee-manage permission is denied at the API', async ({ request }) => {
    const login = await request.post(`${api}/auth/login`, {
      headers: { 'X-App-Surface': 'assistant' },
      data: { phoneNumber: '20000000003', password: 'password', deviceFingerprint: `hr-denied-${Date.now()}` },
    });
    expect(login.ok()).toBeTruthy();
    const payload = await login.json();
    const token = (payload.data ?? payload).accessToken;
    const response = await request.post(`${api}/admin/hr/employees/provision`, {
      headers: { Authorization: `Bearer ${token}`, 'Idempotency-Key': `denied-${Date.now()}` },
      data: { fullName: 'Denied Employee', phoneNumber: '01099999999', password: 'Secret123!', role: 'Assistant', basicSalary: 0, standardStartTime: '09:00', targetDailyHours: 8 },
    });
    expect(response.status()).toBe(403);
  });
});
