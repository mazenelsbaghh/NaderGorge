import { expect, request as requestFactory, test } from '@playwright/test';

const appUrl = process.env.LIVE_SUPPORT_E2E_URL || 'http://localhost:8899';

test.describe('live support participant', () => {
  test('unavailable support blocks chat and shows the next schedule on iPhone width', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.route('**/api/live-support/availability', (route) => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { isAvailable: false, availableStaffCount: 0, nextAvailableAt: '2026-06-22T09:00:00Z', code: 'LIVE_SUPPORT_UNAVAILABLE', message: 'الدعم غير متاح' } }),
    }));

    await page.goto(appUrl);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();

    await expect(page.getByRole('heading', { name: 'الدعم غير متاح الآن' })).toBeVisible();
    await expect(page.getByText('موعد توفر الدعم القادم')).toBeVisible();
    await expect(page.getByRole('button', { name: 'ابدأ المحادثة' })).toHaveCount(0);
    await expect(page.locator('[role="dialog"]')).toHaveCSS('width', '288px');
  });

  test('guest intake states that phone matching never links a student automatically', async ({ page }) => {
    await page.route('**/api/live-support/availability', (route) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'الدعم متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', (route) => route.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));

    await page.goto(appUrl);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();

    await expect(page.getByText('لن نربط رقمك بحساب طالب تلقائيًا.')).toBeVisible();
    await expect(page.getByLabel('الاسم')).toBeVisible();
    await expect(page.getByLabel('رقم الهاتف')).toBeVisible();
  });

  test('participant queue reconnect and large history snapshot stays deduplicated', async ({ page }) => {
    const conversation = { id: '14200000-0000-0000-0000-000000000010', status: 'Waiting', participantType: 'Guest', subject: 'اختبار الطابور', queuePosition: 2, createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false };
    const messages = Array.from({ length: 50 }, (_, index) => ({ id: `14200000-0000-0000-0000-${String(index).padStart(12, '0')}`, conversationId: conversation.id, senderType: index % 2 ? 'Staff' : 'Guest', clientMessageId: `client-${index}`, type: 'Text', content: `رسالة ${index + 1}`, sentAt: new Date(2026, 0, 1, 0, index).toISOString() }));
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 2, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [conversation] }) }));
    await page.route(`**/api/live-support/participant/conversations/${conversation.id}/messages**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: messages, nextCursor: 'next', lastEventSequence: 50, missedEvents: [] } }) }));
    await page.goto(appUrl); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();
    await expect(page.getByRole('status')).toContainText('رقم 2');
    await expect(page.getByRole('log').getByText('رسالة 50')).toHaveCount(1);
    await page.reload(); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();
    await expect(page.getByRole('log').getByText('رسالة 50')).toHaveCount(1);
  });

  test('rating closed conversation is read-only and requires a new conversation', async ({ page }) => {
    let ratingCount = 0;
    const closed = { id: '14200000-0000-0000-0000-000000000011', status: 'Closed', participantType: 'Student', subject: 'مغلقة', createdAt: new Date().toISOString(), closedAt: new Date().toISOString(), version: 2, canSend: false, canRate: true };
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [closed] }) }));
    await page.route(`**/api/live-support/participant/conversations/${closed.id}/messages**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [], lastEventSequence: 1, missedEvents: [] } }) }));
    await page.route(`**/api/live-support/participant/conversations/${closed.id}/rating`, route => { ratingCount++; return route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ success: true, data: {} }) }); });
    await page.goto(appUrl); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();
    await expect(page.getByPlaceholder('اكتب رسالتك')).toHaveCount(0);
    await page.getByRole('button', { name: '5 نجوم' }).click();
    await expect.poll(() => ratingCount).toBe(1);
    await expect(page.getByRole('button', { name: 'محادثة جديدة' })).toBeVisible();
  });

  test('guest link privacy never performs automatic student candidate search', async ({ page }) => {
    let studentSearchRequests = 0;
    page.on('request', request => { if (request.url().includes('/students/search')) studentSearchRequests++; });
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
    await page.goto(appUrl); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();
    await page.getByLabel('رقم الهاتف').fill('01012345678');
    await page.waitForTimeout(500);
    expect(studentSearchRequests).toBe(0);
  });

  test('accessibility keyboard focus and reduced motion work at 320px', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.setViewportSize({ width: 320, height: 700 });
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: false, availableStaffCount: 0, code: 'LIVE_SUPPORT_UNAVAILABLE', message: 'غير متاح' } }) }));
    await page.goto(appUrl); await page.keyboard.press('Tab');
    const launcher = page.getByRole('button', { name: 'فتح الدعم المباشر' });
    await launcher.focus(); await page.keyboard.press('Enter');
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'الدعم غير متاح الآن' })).toBeVisible();
  });
});

test.describe('live support routing and admin', () => {
  test('routing capacity queue close admission and staff disconnect contract', async ({ page, request }) => {
    const seeded = await request.post('http://localhost:5245/api/e2e/seed', { data: { clearDatabase: false, seedAdmin: true, seedStudents: true, seedAssistant: true, seedLiveSupport: true } });
    expect(seeded.ok()).toBeTruthy();
    await page.goto('http://staff.localhost:8899/login');
    await page.locator('input[name="phoneNumber"]').fill('20000000003');
    await page.locator('input[name="password"]').fill('password');
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/assistant/, { timeout: 15_000 });
    await page.goto('http://staff.localhost:8899/assistant/live-support');
    await expect(page.getByText('حالة الاتصال').locator('..')).toContainText('متصل', { timeout: 15_000 });

    const guestA = await requestFactory.newContext({ baseURL: 'http://localhost:5245/api' });
    const guestB = await requestFactory.newContext({ baseURL: 'http://localhost:5245/api' });
    for (const [guest, phone] of [[guestA, '01014000001'], [guestB, '01014000002']] as const) {
      expect((await guest.post('/live-support/guest/session', { data: { displayName: `زائر ${phone}`, phoneNumber: phone } })).ok()).toBeTruthy();
    }
    const first = (await (await guestA.post('/live-support/participant/conversations', { data: { subject: 'routing A' } })).json()).data;
    const second = (await (await guestB.post('/live-support/participant/conversations', { data: { subject: 'routing B' } })).json()).data;
    expect(first.currentOwnerUserId).toBeTruthy();
    expect(second.status).toBe('Waiting');

    const login = await request.post('http://localhost:5245/api/auth/login', { headers: { 'X-App-Surface': 'assistant' }, data: { phoneNumber: '20000000003', password: 'password', deviceFingerprint: 'live-support-e2e-staff' } });
    const token = (await login.json()).data.accessToken;
    expect((await request.post(`http://localhost:5245/api/live-support/staff/conversations/${first.id}/close`, { headers: { Authorization: `Bearer ${token}` }, data: { reason: 'تم الحل في اختبار السعة' } })).ok()).toBeTruthy();
    await expect.poll(async () => (await (await guestB.get(`/live-support/participant/conversations/${second.id}`)).json()).data.status).toMatch(/Assigned|Active/);
    await guestA.dispose(); await guestB.dispose();
  });

  test('admin live support rating intervention requires an audited reason', async ({ page, request }) => {
    const seeded = await request.post('http://localhost:5245/api/e2e/seed', { data: { clearDatabase: false, seedAdmin: true, seedStudents: true, seedAssistant: true, seedLiveSupport: true } });
    expect(seeded.ok()).toBeTruthy();
    await page.goto('http://admin.localhost:8899/login');
    await page.locator('input[name="phoneNumber"]').fill('20000000000');
    await page.locator('input[name="password"]').fill('password');
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/admin/, { timeout: 15_000 });
    await page.goto('http://admin.localhost:8899/admin/live-support');
    await expect(page.getByRole('heading', { name: 'أداء الموظفين والتقييمات' })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('heading', { name: 'الموظفون والسعة والجداول' })).toBeVisible();
  });
});
