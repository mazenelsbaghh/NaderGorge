import { expect, test } from '@playwright/test';

test('admin can open refunds and see amount, operator and reason', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('accessToken', 'e2e-admin-token');
    localStorage.setItem('user', JSON.stringify({
      id: 'admin-refunds', fullName: 'Admin', roles: ['Admin'], permissions: [],
      profileComplete: true, allowedDomains: ['admin'], allowedNavbarItems: ['/admin/gifts'],
    }));
  });
  await page.route('**/api/admin/wallets**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) }));
  await page.route('**/api/admin/platform-finance/refunds/bootstrap**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ treasuryAccounts: [] }) }));
  await page.route('**/api/admin/platform-finance/refunds', route => route.fulfill({
    status: 200, contentType: 'application/json', body: JSON.stringify([{
      id: '11111111-1111-1111-1111-111111111111', originalSourceId: '22222222-2222-2222-2222-222222222222',
      originalSourceType: 'PurchaseOperation', studentId: '33333333-3333-3333-3333-333333333333',
      studentName: 'طالب تجريبي', studentPhoneNumber: '01000000001', processedByUserId: '44444444-4444-4444-4444-444444444444',
      processedByName: 'الموظف أحمد', platformAmount: 80, teacherAmount: 20, totalAmount: 100,
      method: 2, status: 2, reason: 'رد مصروف الحصة', createdAt: '2026-09-30T10:00:00Z', isHistorical: false,
    }]),
  }));

  await page.goto('http://admin.lvh.me:8738/admin');
  await page.getByRole('link', { name: /استردادات الطلاب/ }).click();
  await expect(page).toHaveURL(/\/admin\/platform-finance\/refunds$/);
  await expect(page.getByRole('columnheader', { name: 'المبلغ المسترد' })).toBeVisible();
  await expect(page.getByText('100.00 ج.م')).toBeVisible();
  await expect(page.getByText('الموظف أحمد')).toBeVisible();
  await expect(page.getByText('رد مصروف الحصة')).toBeVisible();
});

for (const scenario of [
  { name: 'historical grant', paidAmount: 100, teacherShareAmount: 20, purchaseOperationId: null, amount: 75, platformAmount: 75, teacherAmount: 0 },
  { name: '2026-10-01: 290 EGP refund with upward floating point drift', paidAmount: 490, teacherShareAmount: 120, purchaseOperationId: '22222222-2222-2222-2222-222222222222', amount: 290, platformAmount: 218.98, teacherAmount: 71.02 },
  { name: '2026-10-01: 290 EGP refund with downward floating point drift', paidAmount: 490, teacherShareAmount: 300, purchaseOperationId: '22222222-2222-2222-2222-222222222222', amount: 290, platformAmount: 112.45, teacherAmount: 177.55 },
]) {
  test(`admin can submit an external package refund: ${scenario.name}`, async ({ page }) => {
    const studentId = '33333333-3333-3333-3333-333333333333';
    const grantId = '55555555-5555-5555-5555-555555555555';
    const treasuryId = '66666666-6666-6666-6666-666666666666';
    let submitted: Record<string, unknown> | null = null;
    await page.addInitScript(() => {
      localStorage.setItem('accessToken', 'e2e-admin-token');
      localStorage.setItem('user', JSON.stringify({
        id: 'admin-refunds', fullName: 'Admin', roles: ['Admin'], permissions: [],
        profileComplete: true, allowedDomains: ['admin'],
      }));
    });
    await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
    await page.route('**/api/auth/session', route => route.fulfill({ json: { success: true, data: {
      user: { id: 'admin-refunds', fullName: 'Admin', roles: ['Admin'], permissions: [], profileComplete: true, allowedDomains: ['admin'], authorizationVersion: 1 },
      authorizationVersion: 1,
    } } }));
    await page.route('**/api/admin/wallets**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) }));
    await page.route('**/api/admin/platform-finance/refunds/bootstrap**', route => route.fulfill({
      status: 200, contentType: 'application/json', body: JSON.stringify({ treasuryAccounts: [{ id: treasuryId, name: 'الخزنة الرئيسية' }] }),
    }));
    await page.route('**/api/admin/platform-finance/refunds/students?*', route => route.fulfill({
      status: 200, contentType: 'application/json', body: JSON.stringify([{ id: studentId, fullName: 'طالب تجريبي', phoneNumber: '01000000001' }]),
    }));
    await page.route(`**/api/admin/platform-finance/refunds/students/${studentId}`, route => route.fulfill({
      status: 200, contentType: 'application/json', body: JSON.stringify({
        id: studentId, fullName: 'طالب تجريبي', phone: '01000000001', packages: [{
          accessGrantId: grantId, name: 'باقة تجريبية', isActive: true, purchaseMethod: 'Balance',
          price: scenario.paidAmount, paidAmount: scenario.paidAmount, teacherShareAmount: scenario.teacherShareAmount, teacherId: null, purchaseOperationId: scenario.purchaseOperationId,
        }],
      }),
    }));
    await page.route(`**/api/admin/platform-finance/refunds/students/${studentId}/grants/${grantId}/preview**`, route => route.fulfill({
      status: 200, contentType: 'application/json', body: JSON.stringify({
        paidAmount: scenario.paidAmount, previouslyRefundedAmount: 0, remainingRefundableAmount: scenario.paidAmount,
        scopeLabel: 'الباقة', usageAvailable: true, videosAvailable: true, examsAvailable: true,
        totalVideos: 0, watchedVideos: 0, completedVideos: 0, unknownDurationVideos: 0,
        totalExams: 0, attemptedExams: 0, totalAttempts: 0, submittedAttempts: 0,
        historicalUsageNote: '', isHistoricalSource: !scenario.purchaseOperationId,
      }),
    }));
    await page.route('**/api/admin/platform-finance/refunds', async route => {
      if (route.request().method() === 'POST') {
        submitted = route.request().postDataJSON();
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: '77777777-7777-7777-7777-777777777777', totalAmount: scenario.amount, status: 2 }) });
      } else {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
      }
    });
    await page.route('**/api/admin/platform-finance/refunds/external-package', async route => {
      submitted = route.request().postDataJSON();
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: '77777777-7777-7777-7777-777777777777', totalAmount: scenario.amount, status: 2 }) });
    });

    await page.goto('http://admin.lvh.me:8738/admin/platform-finance/refunds');
    await page.getByPlaceholder('01xxxxxxxxx').fill('01000000001');
    await page.getByRole('button', { name: 'بحث' }).click();
    await page.getByRole('button', { name: /طالب تجريبي/ }).click();
    await page.getByRole('combobox', { name: 'الباقة التي سيتم إلغاؤها' }).selectOption(grantId);
    await expect(page.getByText('المتاح للاسترداد')).toBeVisible();
    await page.getByRole('combobox', { name: 'الخزنة أو المحفظة التي خرج منها المبلغ' }).selectOption(treasuryId);
    await page.getByPlaceholder('المبلغ بالجنيه').fill(String(scenario.amount));
    await page.getByPlaceholder('اكتب سبب إلغاء الباقة ورد المبلغ').fill('طلب الطالب');
    await page.getByRole('button', { name: 'إلغاء الباقة وتسجيل الاسترداد' }).click();
    await expect.poll(() => submitted).toMatchObject({
      accessGrantId: grantId, studentId, treasuryAccountId: treasuryId,
      platformAmount: scenario.platformAmount, teacherAmount: scenario.teacherAmount, reason: 'طلب الطالب',
      ...(scenario.purchaseOperationId ? { purchaseOperationId: scenario.purchaseOperationId } : {}),
    });
    await expect(page.getByText('تم إلغاء الباقة وتسجيل الاسترداد الخارجي في المركز المالي')).toBeVisible();
  });
}

test('anonymous cannot mutate or enumerate refunds', async ({ request }) => {
  const responses = await Promise.all([request.get('http://api.lvh.me:5245/api/admin/platform-finance/refunds'), request.post('http://api.lvh.me:5245/api/admin/platform-finance/refunds', { data: {} })]);
  expect(responses.map(response => response.status())).toEqual([401, 401]);
});
