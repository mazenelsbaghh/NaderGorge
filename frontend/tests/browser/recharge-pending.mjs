import test from 'node:test';
import { chromium, expect } from '@playwright/test';

const origin = process.env.RECHARGE_BROWSER_ORIGIN ?? 'http://app.lvh.me:8739';
const teacherId = 'recharge-teacher';
const student = {
  id: 'recharge-student', fullName: 'طالبة اختبار الشحن', roles: ['Student'],
  permissions: [], allowedDomains: ['student'], allowedNavbarItems: [],
  profileComplete: true, authorizationVersion: 1,
};
let browser;
test.before(async () => { browser = await chromium.launch({ channel: 'chrome' }); });
test.after(async () => { await browser?.close(); });

async function openRecharge(t, width = 390) {
  const page = await browser.newPage({ viewport: { width, height: 900 } });
  t.after(() => page.close());
  await page.addInitScript(user => {
    localStorage.setItem('accessToken', 'synthetic-recharge-token');
    localStorage.setItem('user', JSON.stringify(user));
  }, student);
  const reservation = {
    id: 'old-request', reviewCode: 'OLD12345', amount: 100, teacherId,
    teacherName: 'أشرف', senderPhoneNumber: '', requiresSenderPhoneConfirmation: false,
    walletLabel: 'محفظة الاختبار', walletPhoneNumber: '01010000000', status: 'Pending',
    createdAt: new Date().toISOString(),
    reservationExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  };
  const server = { requests: [reservation], failRead: false, initiations: 0, submittedIds: [] };
  await page.route('**/api/**', async route => {
    const { pathname } = new URL(route.request().url());
    let data = [];
    if (pathname.endsWith('/auth/session')) data = { user: student, authorizationVersion: 1 };
    if (pathname.endsWith('/public/settings')) return route.fulfill({ json: { maintenanceMode: false } });
    if (pathname.endsWith('/public/teachers')) data = [{ id: teacherId, fullName: 'أشرف' }];
    if (pathname.endsWith('/student/shell-bootstrap')) data = {
      unreadNotificationsCount: 0, currentBalance: 0, hasSeenTrackingCodePopup: true,
      gamification: { totalPoints: 0, levelName: 'طالب' },
    };
    if (pathname.endsWith('/student/welcome/claim')) data = null;
    if (pathname.endsWith('/recharge/requests')) {
      if (server.failRead) return route.fulfill({ status: 503, json: { message: 'تعذر تحديث حالة الشحن' } });
      data = server.requests;
    }
    if (pathname.endsWith('/recharge/initiate')) {
      server.initiations += 1;
      const created = { ...reservation, id: 'new-request', reviewCode: 'NEW12345' };
      server.requests = [created, ...server.requests];
      data = { rechargeRequestId: created.id, reviewCode: created.reviewCode,
        walletPhoneNumber: created.walletPhoneNumber, walletLabel: created.walletLabel,
        expirationTime: created.reservationExpiresAt };
    }
    if (pathname.endsWith('/recharge/submit')) {
      const payload = route.request().postDataBuffer().toString();
      const requestId = /name="rechargeRequestId"\r\n\r\n([^\r]+)/.exec(payload)?.[1];
      expect(payload).toContain('name="screenshot"; filename=');
      server.submittedIds.push(requestId);
      server.requests = server.requests.map(request => request.id === requestId
        ? { ...request, screenshotUrl: '/proof.webp', senderPhoneNumber: '01012345678', reservationExpiresAt: null }
        : request);
      data = { isMatched: false, requiresSenderPhoneConfirmation: false, reviewCode: 'NEW12345' };
    }
    return route.fulfill({ json: { success: true, data } });
  });
  await page.goto(`${origin}/student/recharge`);
  await expect(page.getByRole('button', { name: 'عرض الطلب المعلق', exact: true })).toBeVisible();
  await page.getByLabel('رصيد المدرس', { exact: false }).selectOption(teacherId);
  return { page, server, reservation };
}

async function uploadProof(page) {
  await page.getByLabel('رقم الهاتف الذي قمت بالتحويل منه', { exact: false }).fill('01012345678');
  await page.locator('#recharge-screenshot').setInputFiles({
    name: 'transfer.png', mimeType: 'image/png',
    buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lXcAAAAASUVORK5CYII=', 'base64'),
  });
  await page.getByRole('button', { name: 'رفع الصورة وإرسال الطلب', exact: true }).click();
  await expect(page.getByText('جاري التأكد من وصول رسالة الشحن.', { exact: false })).toBeVisible();
}

// Regression: an operator resolves the request while the student's page stays open.
for (const scenario of [{ status: 3, width: 390 }, { status: 'Cancelled', width: 1280 }]) {
  test(`resolved ${scenario.status} request releases payment and proof upload at ${scenario.width}px (synthetic HTTP)`, { timeout: 90000 }, async t => {
    const { page, server, reservation } = await openRecharge(t, scenario.width);
    server.requests = [{ ...reservation, status: scenario.status, reservationExpiresAt: null }];
    await page.getByRole('button', { name: 'الذهاب لخطوة الدفع', exact: true }).click();
    await expect(page.locator('#recharge-screenshot')).toBeAttached();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await uploadProof(page);
    expect(server.initiations).toBe(1);
    expect(server.submittedIds).toEqual(['new-request']);
  });
}

test('pending reservation resumes and uploads proof without creating a duplicate (synthetic HTTP)', { timeout: 60000 }, async t => {
  const { page, server } = await openRecharge(t);
  await page.getByRole('button', { name: 'الذهاب لخطوة الدفع', exact: true }).click();
  await page.getByRole('button', { name: 'استكمال الطلب ورفع الإثبات', exact: true }).click();
  await uploadProof(page);
  expect(server.initiations).toBe(0);
  expect(server.submittedIds).toEqual(['old-request']);
});

test('failed status refresh preserves the pending request and allows retry (synthetic HTTP)', { timeout: 60000 }, async t => {
  const { page, server } = await openRecharge(t);
  server.failRead = true;
  await page.getByRole('button', { name: 'الذهاب لخطوة الدفع', exact: true }).click();
  await expect(page.getByText('تعذر تحديث حالة الشحن', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'عرض الطلب المعلق', exact: true })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(server.initiations).toBe(0);
  server.failRead = false;
  await page.getByRole('button', { name: 'الذهاب لخطوة الدفع', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
});

test('request resolved while its dialog is open cannot resume the old reservation (synthetic HTTP)', { timeout: 60000 }, async t => {
  const { page, server, reservation } = await openRecharge(t);
  await page.getByRole('button', { name: 'عرض الطلب المعلق', exact: true }).click();
  server.requests = [{ ...reservation, status: 'Rejected', reservationExpiresAt: null }];
  await page.getByRole('button', { name: 'استكمال الطلب ورفع الإثبات', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.locator('#recharge-screenshot')).toHaveCount(0);
  await page.getByRole('button', { name: 'الذهاب لخطوة الدفع', exact: true }).click();
  await expect(page.locator('#recharge-screenshot')).toBeAttached();
  expect(server.initiations).toBe(1);
});
