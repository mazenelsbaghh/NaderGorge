import { expect, test } from '@playwright/test';

test.use({ baseURL: process.env.PLAYWRIGHT_ADMIN_BASE_URL || `http://admin.lvh.me:${process.env.PLAYWRIGHT_WEB_PORT || '3000'}` });

const account = {
  teacherId: 'first', teacherName: 'المدرس الأول', totalEarned: 600, available: 400,
  reserved: 100, paid: 200, debt: 50, netPayable: 250, netBalance: 350,
  debtReserved: 0, unreservedDebt: 50, todayEarnings: 0, sourceEarnings: 600,
  sourceDifference: 0, balanceDifference: -50, sources: [{ sourceType: 'DirectPurchase', count: 2, teacherShare: 600, platformShare: 400 }],
};

const payload = {
  generatedAt: '2026-09-18T21:00:00Z', earliestDate: '2026-01-01', historicalPlatformNetRevenue: 480,
  platform: { from: '2026-09-01', to: '2026-09-19', revenue: 500, refunds: 20, expenses: 100, netProfit: 380, cash: 4200, generalStudentLiability: 600, teacherStudentLiability: 200, teacherPayable: 550, supplierPayable: 100, accounts: [] },
  teachers: [
    { account, period: { teacherId: 'first', teacherName: 'المدرس الأول', grossSales: 1000, teacherShare: 600, platformShare: 350, refunds: 50, paid: 200, outstanding: 400, adjustments: 0 }, historicalPeriod: { teacherShare: 600 }, currentCalculatedBalance: 400, currentAccountBalance: 400, currentLedgerBalance: 400, reconciliationDifference: 0 },
    { account: { ...account, teacherId: 'second', teacherName: '=2+2', netPayable: 50 }, period: { teacherId: 'second', teacherName: '=2+2', grossSales: 300, teacherShare: 150, platformShare: 130, refunds: 20, paid: 0, outstanding: 150, adjustments: 0 }, historicalPeriod: { teacherShare: 150 }, currentCalculatedBalance: 150, currentAccountBalance: 100, currentLedgerBalance: 150, reconciliationDifference: 50 },
  ],
};

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('accessToken', 'test-admin-token');
    localStorage.setItem('user', JSON.stringify({ id: 'test-admin', fullName: 'Admin', roles: ['Admin'], permissions: [], profileComplete: true, allowedDomains: ['admin'], allowedNavbarItems: [] }));
  });
  await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
});

test('dedicated profit report filters teachers, exposes mismatches, and exports safe complete CSV', async ({ page }) => {
  let requestedDates = '';
  await page.route('**/api/admin/platform-finance/profits?*', route => {
    requestedDates = new URL(route.request().url()).search;
    return route.fulfill({ json: payload });
  });
  await page.goto('/admin/platform-profits');
  await expect(page.getByRole('heading', { name: 'الحسابات', exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'ملخص الأرباح' })).toContainText('380.00');
  await expect(page.getByText('فيه أرقام محتاجة مراجعة', { exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'حساب =2+2' })).not.toBeVisible();
  await page.getByRole('button', { name: 'فترة تانية' }).click();
  await page.getByLabel('من', { exact: true }).fill('2026-09-01');
  await page.getByLabel('إلى', { exact: true }).fill('2026-09-19');
  await page.getByRole('button', { name: 'عرض الحسابات', exact: true }).click();
  await expect(page.getByRole('list', { name: 'مستحقات المدرسين' })).toContainText('250.00');
  expect(requestedDates).toContain('from=2026-09-01');
  expect(requestedDates).toContain('to=2026-09-19');
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'تنزيل الحسابات CSV' }).click();
  const stream = await (await downloadPromise).createReadStream();
  const chunks = [];
  for await (const chunk of stream!) chunks.push(Buffer.from(chunk));
  const csv = Buffer.concat(chunks).toString('utf8');
  expect(csv).toContain('"\'=2+2"');
  expect(csv).toContain('"صافي الربح المسجل","380"');
  expect(csv).toContain('"المدرس الأول","1000","600","350","50","200"');
  await page.getByRole('combobox', { name: 'اختار المدرّس', exact: true }).selectOption('second');
  await expect(page.getByRole('list', { name: 'مستحقات المدرسين' })).not.toContainText('المدرس الأول');
  await page.locator('summary').filter({ hasText: '=2+2' }).focus();
  await page.keyboard.press('Enter');
  const detail = page.getByRole('region', { name: 'حساب =2+2' });
  await detail.getByText('مراجعة فرق الحساب', { exact: true }).click();
  await expect(detail.getByText('الفرق بين الرصيدين', { exact: true }).locator('..')).toContainText('50.00');
  await expect(detail.getByRole('link')).toHaveAttribute('href', '/admin/teachers/second/account');
  await page.getByRole('combobox', { name: 'اختار المدرّس', exact: true }).selectOption('');
  await page.setViewportSize({ width: 1440, height: 1050 });
  await page.getByRole('heading', { name: 'الحسابات', exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: '/tmp/simple-finance-desktop.png', fullPage: true });
});

test('failed profit report does not present zero earnings and can be retried on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  let failing = true;
  await page.route('**/api/admin/platform-finance/profits?*', route => failing
    ? route.fulfill({ status: 503, json: { message: 'Unavailable' } }) : route.fulfill({ json: payload }));
  await page.goto('/admin/platform-profits');
  await expect(page.getByRole('alert').filter({ hasText: 'تعذر تحميل الحسابات' })).toBeVisible();
  await expect(page.getByRole('list', { name: 'مستحقات المدرسين' })).toHaveCount(0);
  failing = false;
  await page.getByRole('button', { name: 'إعادة المحاولة' }).click();
  await expect(page.getByRole('list', { name: 'مستحقات المدرسين' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: '/tmp/simple-finance-mobile.png', fullPage: true });
});

test('loss and negative teacher balance stay explicit in the simple summary', async ({ page }) => {
  await page.route('**/api/admin/platform-finance/profits?*', route => route.fulfill({ json: {
    ...payload,
    platform: { ...payload.platform, expenses: 600, netProfit: -120 },
    teachers: [{ ...payload.teachers[0], account: { ...account, netPayable: 0, netBalance: -100 }, period: { ...payload.teachers[0].period, outstanding: -100 } }],
  } }));
  await page.goto('/admin/platform-profits');
  const summary = page.getByRole('region', { name: 'ملخص الأرباح' });
  await expect(summary).toContainText('صافي الخسارة');
  await expect(summary).toContainText('120.00');
  const teachers = page.getByRole('list', { name: 'مستحقات المدرسين' });
  await expect(teachers).toContainText('مطلوب منه');
  await expect(teachers).toContainText('100.00');
});

test('balances load without advanced requests and stay available when the ledger fails', async ({ page }) => {
  let ledgerRequests = 0;
  await page.route('**/api/admin/platform-finance/dashboard?*', route => route.fulfill({ json: payload.platform }));
  await page.route('**/api/admin/platform-finance/ledger?*', route => {
    ledgerRequests++;
    return route.fulfill({ status: 503, json: {} });
  });
  await page.goto('/admin/platform-finance');
  const balances = page.getByRole('region', { name: 'الأرصدة في نهاية الفترة' });
  await expect(balances).toContainText('4,200.00');
  await expect(balances).toContainText('800.00');
  expect(ledgerRequests).toBe(0);
  await page.getByText('تفاصيل محاسبية متقدمة', { exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'تعذر تحميل الحركات' })).toBeVisible();
  await expect(balances).toContainText('4,200.00');
  await page.getByText('الدخل والمصاريف في الفترة دي', { exact: true }).click();
  await expect(page.getByRole('region', { name: 'ملخص الأرباح' })).toContainText('380.00');
  await page.screenshot({ path: '/tmp/simple-finance-balances.png', fullPage: true });
});

test('one teacher account explains sources and uses separate approval and actual-payment commands', async ({ page }) => {
  const teacher = { id: 'first', fullName: 'المدرس الأول', isActive: true, commissionRate: 75 };
  let payoutStatus = 'Pending';
  const actions: number[] = [];
  let current = { ...account, totalEarned: 550, sourceEarnings: 550, balanceDifference: 0,
    sources: [...account.sources, { sourceType: 'Refund', count: 1, teacherShare: -50, platformShare: 0 }] };
  await page.route('**/api/admin/teachers/first', route => route.fulfill({ json: { success: true, data: teacher } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/summary', route => route.fulfill({ json: { success: true, data: current } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/ledger?*', route => route.fulfill({ json: { success: true, data: {
    items: [{ id: 'line', sourceType: 'DirectPurchase', contentNameSnapshot: 'مراجعة الفيزياء', occurredAt: '2026-09-20T10:00:00Z', teacherShareAmount: 87.5, platformShareAmount: 12.5, reversedAmount: 0, reviewStatus: 'Approved', payoutStatus: 'Unpaid', allocationMode: 'FixedAmount', allocationValue: 12.5, agreementAllocationMode: 'PlatformFixedPerUnit', grossBasisAmount: 100, priceBasis: 'NetAfterDiscount', agreementId: 'old-agreement' }], total: 1, page: 1, pageSize: 25,
  } } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/collections?*', route => route.fulfill({ json: { success: true, data: { teacherId: 'first', items: [], totalAmount: 0, totalCount: 0, vodafoneCashAmount: 0, otherOrUnverifiedAmount: 0, vodafoneCashCount: 0, filteredCount: 0, page: 1, pageSize: 20 } } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/settlements?*', route => route.fulfill({ json: { success: true, data: { items: [], total: 0, page: 1, pageSize: 20 } } }));
  await page.route('**/api/admin/finance/payouts?*', route => {
    expect(new URL(route.request().url()).searchParams.get('teacherId')).toBe('first');
    return route.fulfill({ json: { success: true, data: [{ id: 'withdrawal', teacherId: 'first', teacherName: teacher.fullName, amount: 100, createdAt: '2026-09-20T10:00:00Z', status: payoutStatus }] } });
  });
  await page.route('**/api/admin/finance/payouts/withdrawal/resolve', route => {
    const status = route.request().postDataJSON().status;
    actions.push(status);
    if (status === 3) payoutStatus = 'Approved';
    if (status === 1) { payoutStatus = 'Paid'; current = { ...current, paid: 300, available: 300, reserved: 0, netBalance: 250 }; }
    return route.fulfill({ json: { success: true, data: true } });
  });
  await page.goto('/admin/platform-finance/teachers/first');
  await expect(page).toHaveURL(/\/admin\/teachers\/first\/account$/);
  const overview = page.getByRole('region', { name: 'ملخص حساب المدرس' });
  await expect(overview.getByText('متاح لسحب جديد', { exact: true }).first().locator('..')).toContainText('250.00');
  await expect(page.getByRole('region', { name: 'مصادر أرباح المدرس' })).toContainText('المرتجعات');
  await overview.getByText('المتاح للسحب اتحسب إزاي؟').click();
  await expect(overview).toContainText('المديونية اللي لسه ما اتحجزتش');
  await page.getByText('اتحسب إزاي؟', { exact: true }).click();
  await expect(page.getByText('نصيب المنصّة ثابت:', { exact: false })).toContainText('12.50');
  await page.getByText('طلبات السحب والمدفوعات', { exact: true }).click();
  await page.getByRole('button', { name: 'موافقة على الطلب', exact: true }).click();
  await page.getByRole('button', { name: 'تأكيد', exact: true }).click();
  await expect(page.getByRole('button', { name: 'تسجيل الصرف الفعلي', exact: true })).toBeVisible();
  await expect(overview.getByText('استلم فعليًا', { exact: true }).locator('..')).toContainText('200.00');
  await page.getByRole('button', { name: 'تسجيل الصرف الفعلي', exact: true }).click();
  await page.getByRole('button', { name: 'تأكيد', exact: true }).click();
  await expect(overview.getByText('استلم فعليًا', { exact: true }).locator('..')).toContainText('300.00');
  expect(actions).toEqual([3, 1]);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('#main-content')).toHaveCSS('margin-inline-start', '0px');
  await page.getByRole('heading', { name: 'المدرس الأول', exact: true }).scrollIntoViewIfNeeded();
  expect((await overview.boundingBox())!.width).toBeGreaterThan(320);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: '/tmp/unified-teacher-account-mobile.png', fullPage: true, animations: 'disabled' });
  await page.setViewportSize({ width: 1440, height: 1050 });
  await expect(page.locator('#main-content')).toHaveCSS('margin-inline-start', '288px');
  await page.getByRole('heading', { name: 'المدرس الأول', exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: '/tmp/unified-teacher-account-desktop.png', fullPage: true, animations: 'disabled' });
});

test('teacher account failure never displays a zero balance', async ({ page }) => {
  await page.route('**/api/admin/teacher-finance-center/teachers/first/summary', route => route.fulfill({ status: 503, json: {} }));
  await page.goto('/admin/teachers/first/account');
  const overview = page.getByRole('region', { name: 'ملخص حساب المدرس' });
  await expect(overview.getByRole('alert')).toContainText('تعذر تحميل حساب المدرّس');
  await expect(overview).not.toContainText('0.00');
});

test('teacher self-service shows the same account and withdrawal limit', async ({ browser }) => {
  const context = await browser.newContext();
  try {
    await context.addInitScript(() => {
      localStorage.setItem('accessToken', 'test-teacher-token');
      localStorage.setItem('user', JSON.stringify({ id: 'teacher-user', fullName: 'المدرس الأول', roles: ['Teacher'], permissions: [], profileComplete: true, allowedDomains: ['teacher'], allowedNavbarItems: [] }));
    });
    const page = await context.newPage();
    await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
    await page.route('**/api/teacher/finance/account', route => route.fulfill({ json: { success: true, data: {
      teacherId: 'first', teacherName: account.teacherName, todayEarnings: 0, totalEarnings: account.totalEarned,
      currentBalance: account.available, reservedBalance: account.reserved, availableBalance: account.netPayable,
      debtBalance: account.debt, commissionRate: 75, account,
    } } }));
    await page.route('**/api/teacher/finance/transactions?*', route => route.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 20 } } }));
    await page.goto(`http://teacher.lvh.me:${process.env.PLAYWRIGHT_WEB_PORT || '3000'}/teacher/finance`);
    const overview = page.getByRole('region', { name: 'ملخص حساب المدرس' });
    await expect(overview.getByText('متاح لسحب جديد', { exact: true }).first().locator('..')).toContainText('250.00');
    await expect(overview.getByText('استلم فعليًا', { exact: true }).locator('..')).toContainText('200.00');
    await page.getByRole('button', { name: /^طلب سحب(?: رصيد جديد)?$/ }).click();
    await expect(page.getByPlaceholder('أدخل قيمة السحب...')).toHaveAttribute('max', '250');
  } finally { await context.close(); }
});

for (const paid of [true, false]) {
  test(`expense marked ${paid ? 'paid' : 'unpaid'} uses the matching payment source and retries the saved expense`, async ({ page }) => {
    const creations: unknown[] = [];
    const postings: Array<{ treasuryAccountId?: string; idempotencyKey: string }> = [];
    await page.route('**/api/admin/platform-finance/bootstrap', route => route.fulfill({ json: {
      categories: [{ id: 'internet', name: 'الإنترنت' }], treasuryAccounts: [{ id: 'cashbox', name: 'الخزنة' }],
    } }));
    await page.route('**/api/admin/platform-finance/expenses', route => {
      creations.push(route.request().postDataJSON());
      return route.fulfill({ json: { id: 'expense-1', amount: 250, documentNumber: 'EXP-1', status: 1 } });
    });
    await page.route('**/api/admin/platform-finance/expenses/expense-1/post', route => {
      postings.push(route.request().postDataJSON());
      return route.fulfill(postings.length === 1 ? { status: 503, json: {} } : { json: { id: 'expense-1', status: paid ? 4 : 2 } });
    });
    await page.goto('/admin/platform-finance/operations');
    const form = page.getByRole('form', { name: 'تسجيل مصروف' });
    await form.getByLabel('المبلغ بالجنيه').fill('250');
    await form.getByLabel('نوع المصروف').selectOption('internet');
    await form.getByLabel('اتصرف في إيه؟').fill('اشتراك الإنترنت');
    if (paid) {
      // A paid expense must not silently become unpaid when no cashbox is selected.
      await form.getByRole('button', { name: 'حفظ المصروف' }).click();
      await expect(form.getByLabel('اتدفع منين؟')).toBeFocused();
      expect(creations).toHaveLength(0);
      await form.getByLabel('اتدفع منين؟').selectOption('cashbox');
    } else {
      await form.getByLabel('لسه ما اتدفعش').check();
    }
    await form.getByRole('button', { name: 'حفظ المصروف' }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'المصروف اتحفظ' })).toBeVisible();
    await expect(form.getByLabel('المبلغ بالجنيه')).toBeDisabled();
    await form.getByRole('button', { name: 'إعادة محاولة التأكيد' }).click();
    await expect(page.getByRole('status').filter({ hasText: paid ? 'اتخصم' : 'مفيش فلوس اتخصمت' })).toBeVisible();
    expect(creations).toHaveLength(1);
    expect(postings).toHaveLength(2);
    expect(postings[0].idempotencyKey).toBe(postings[1].idempotencyKey);
    expect(postings[1].treasuryAccountId).toBe(paid ? 'cashbox' : undefined);
  });
}

test('a prepaid code batch shows one agreement, records partial receipt and allows paying the remainder', async ({ page }) => {
  const teacher = { id: 'first', fullName: 'المدرس الأول', isActive: true, commissionRate: 75 };
  let delivered = false;
  let collected = 0;
  const confirmations: any[] = [];
  const receipts: any[] = [];
  await page.route('**/api/admin/teachers/first', route => route.fulfill({ json: { success: true, data: teacher } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/summary', route => route.fulfill({ json: { success: true, data: { ...account,
    totalEarned: delivered ? 8500 : 0, paid: 0, available: 0, netPayable: 0, retained: delivered ? 8500 : 0,
    codeAmountDue: delivered ? 1500 - collected : 0, codeAmountCollected: collected, sourceDifference: 0, balanceDifference: 0,
  } } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/ledger?*', route => route.fulfill({ json: { success: true, data: { items: [], total: 0, page: 1, pageSize: 25 } } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/collections?*', route => route.fulfill({ json: { success: true, data: { items: [], totalAmount: 0, totalCount: 0, vodafoneCashAmount: 0, otherOrUnverifiedAmount: 0, vodafoneCashCount: 0, filteredCount: 0, page: 1, pageSize: 20 } } }));
  await page.route('**/api/admin/teacher-finance-center/teachers/first/settlements?*', route => route.fulfill({ json: { success: true, data: { items: [], total: 0, page: 1, pageSize: 20 } } }));
  await page.route('**/api/admin/codes/groups*', route => route.fulfill({ json: { success: true, data: [{ id: 'batch', teacherId: 'first', name: 'دفعة ١٠٠ كود', codeType: 'Package', codeCount: 100, usedCount: 0 }] } }));
  await page.route('**/api/admin/platform-finance/bootstrap', route => route.fulfill({ json: { treasuryAccounts: [{ id: 'cash', name: 'الخزينة' }] } }));
  await page.route('**/api/admin/teacher-finance-center/code-groups/batch/account', route => route.fulfill({ json: { success: true, data: {
    id: 'batch', teacherId: 'first', name: 'دفعة ١٠٠ كود', totalCodes: 100, trigger: 'CodeDelivery', started: delivered,
    quote: delivered ? null : { units: 100, gross: 10000, net: 10000, teacherShare: 8500, platformShare: 1500, key: 'approved-quote', agreement: { agreementId: 'one-rule', allocationMode: 'PlatformFixedPerUnit', allocationValue: 15 } },
    delivery: delivered ? { recipient: teacher.fullName, confirmedAt: '2026-09-24T10:00:00Z', platformAmountDue: 1500, teacherRetainedAmount: 8500, paid: collected, remaining: 1500 - collected, payments: [] } : null,
  } } }));
  await page.route('**/api/admin/teacher-finance-center/code-groups/batch/confirm-delivery', route => {
    const body = route.request().postDataJSON(); confirmations.push(body); delivered = true; collected += body.payment.amount;
    return route.fulfill({ json: { success: true, data: { id: 'delivery' } } });
  });
  await page.route('**/api/admin/teacher-finance-center/code-groups/batch/payments', route => {
    const body = route.request().postDataJSON(); receipts.push(body); collected += body.amount;
    return route.fulfill({ json: { success: true } });
  });
  await page.goto('/admin/teachers/first/account?codeGroup=batch#code-batches');
  const panel = page.locator('#code-batches');
  await expect(panel).toContainText('معاينة حساب 100 كود');
  await expect(panel).toContainText('8,500.00');
  await panel.getByLabel('استلمت منه فلوس بالفعل').check();
  await panel.getByLabel('استلمت كام؟').fill('600');
  await panel.getByLabel('الفلوس وصلت فين؟').selectOption('cash');
  await panel.getByLabel('رقم التحويل أو الإيصال').fill('دفعة أولى');
  await panel.getByRole('button', { name: 'تأكيد تسليم وحساب الدفعة' }).click();
  await expect(panel.getByText('الباقي عليه', { exact: true }).locator('..')).toContainText('900.00');
  await expect(panel.getByRole('button', { name: 'تأكيد تسليم وحساب الدفعة' })).toHaveCount(0);
  expect(confirmations).toHaveLength(1);
  expect(confirmations[0]).toMatchObject({ quoteKey: 'approved-quote', payment: { amount: 600, treasuryAccountId: 'cash' } });
  await panel.getByLabel('استلمت كام؟').fill('900');
  await panel.getByLabel('رقم التحويل أو الإيصال').fill('الباقي');
  await panel.getByRole('button', { name: 'تسجيل المبلغ المستلم' }).click();
  await expect(panel).toContainText('الدفعة مسددة بالكامل');
  expect(receipts).toHaveLength(1);
  const summary = page.getByRole('region', { name: 'حساب دفعات الأكواد' });
  await expect(summary.getByText('نصيبه المحتفظ به', { exact: true }).locator('..')).toContainText('8,500.00');
  await expect(summary.getByText('دفع للمنصّة', { exact: true }).locator('..')).toContainText('1,500.00');
  await page.setViewportSize({ width: 390, height: 844 });
  await panel.scrollIntoViewIfNeeded();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: '/tmp/teacher-code-batch-mobile.png', fullPage: true });
});
