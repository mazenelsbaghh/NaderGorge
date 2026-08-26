import { expect, request as requestFactory, test } from '@playwright/test';
import { adminUrl, apiUrl, appUrl, bearer, login, seedE2E, staffUrl } from './e2e-contract-helpers';

const e2eToken = process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345';

async function createGuestSession(suffix: string) {
  let context = await requestFactory.newContext({
    baseURL: `${apiUrl}/`,
    extraHTTPHeaders: { 'X-E2E-Token': e2eToken },
  });
  const session = await context.post('live-support/guest/session', {
    data: { displayName: `E2E ${suffix}`, phoneNumber: `010${Date.now().toString().slice(-8)}` },
  });
  expect(session.ok(), `guest session must be created (${session.status()})`).toBeTruthy();
  const cookie = session.headers()['set-cookie']?.match(/(?:^|,\s*)massar_support_guest=([^;]+)/)?.[1];
  expect(cookie).toBeTruthy();
  await context.dispose();
  context = await requestFactory.newContext({
    baseURL: `${apiUrl}/`,
    extraHTTPHeaders: { 'X-E2E-Token': e2eToken, Cookie: `massar_support_guest=${cookie!}` },
  });
  return context;
}

async function installAssistantAuth(page: import('@playwright/test').Page) {
  const user = {
    id: 'a0000000-0000-0000-0000-000000000099',
    fullName: 'Synthetic assistant',
    phone: '01000000000',
    roles: ['Assistant'],
    permissions: [],
    profileComplete: true,
    allowedDomains: ['assistant'],
    allowedNavbarItems: [],
    authorizationVersion: 1,
  };
  await page.addInitScript(({ token, authUser }) => {
    localStorage.setItem('accessToken', token);
    localStorage.setItem('user', JSON.stringify(authUser));
  }, { token: 'synthetic-assistant-token', authUser: user });
  await page.route('**/api/auth/session', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({ data: { user, authorizationVersion: 1, serverTime: new Date().toISOString() } }),
  }));
}

async function installAdminAuth(page: import('@playwright/test').Page) {
  const user = {
    id: 'a0000000-0000-0000-0000-000000000098',
    fullName: 'Synthetic admin',
    phone: '01000000001',
    roles: ['Admin'],
    permissions: ['live_support.manage'],
    profileComplete: true,
    allowedDomains: ['admin'],
    allowedNavbarItems: [],
    authorizationVersion: 1,
  };
  await page.addInitScript(({ token, authUser }) => {
    localStorage.setItem('accessToken', token);
    localStorage.setItem('user', JSON.stringify(authUser));
  }, { token: 'synthetic-admin-token', authUser: user });
  await page.route('**/api/auth/session', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({ data: { user, authorizationVersion: 1, serverTime: new Date().toISOString() } }),
  }));
}

async function openSyntheticAdminInvestigation(
  page: import('@playwright/test').Page,
  conversation: Record<string, unknown> & { id: string; participantName: string }
) {
  await installAdminAuth(page);
  await page.route('**/api/live-support/admin/ratings**', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({ success: true, data: [{ id: `${conversation.id}-rating`, conversationId: conversation.id, stars: 5, comment: 'متابعة', submittedAt: '2026-08-26T11:30:00.000Z', submittedByName: conversation.participantName, isStudent: false }] }),
  }));
  await page.route(`**/api/live-support/admin/conversations/${conversation.id}/timeline`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({ success: true, data: { conversation, items: [] } }),
  }));
  await page.route('**/api/live-support/whatsapp/templates**', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({ success: true, data: [] }),
  }));

  await page.goto(`${adminUrl}/admin/live-support/ratings`);
  await page.getByRole('button', { name: 'عرض التقييمات' }).click();
  const ratingRow = page.getByRole('row').filter({ hasText: conversation.participantName });
  await expect(ratingRow).toBeVisible();
  await ratingRow.getByRole('button', { name: 'فتح المحادثة' }).click();
}

test.describe('live support participant', () => {
  test.beforeEach(async ({ page }) => {
    page.on('console', msg => console.log('PARTICIPANT PAGE LOG:', msg.text()));
    page.on('pageerror', err => console.error('PARTICIPANT PAGE ERROR:', err.message));
    await page.route('**/api/auth/refresh', route => route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ message: 'لا توجد جلسة' }) }));
  });

  test('unavailable support blocks chat and shows the next schedule on iPhone width', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.route('**/api/live-support/availability', (route) => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { isAvailable: false, availableStaffCount: 0, nextAvailableAt: '2026-06-22T09:00:00Z', code: 'LIVE_SUPPORT_UNAVAILABLE', message: 'الدعم غير متاح' } }),
    }));

    await page.goto(appUrl);
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });

    await expect(page.getByRole('heading', { name: 'الدعم غير متاح الآن' })).toBeVisible();
    await expect(page.getByText('موعد توفر الدعم القادم')).toBeVisible();
    await expect(page.getByRole('button', { name: 'ابدأ المحادثة' })).toHaveCount(0);
    await expect(page.locator('[role="dialog"]')).toHaveCSS('width', '288px');
  });

  test('unavailable support keeps an existing conversation and its history visible', async ({ page }) => {
    const conversation = { id: '14200000-0000-0000-0000-000000000011', status: 'Active', participantType: 'Guest', subject: 'محادثة قائمة', createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false };
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: false, availableStaffCount: 0, nextAvailableAt: null, code: 'UNAVAILABLE', message: 'غير متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [conversation] }) }));
    await page.route(`**/api/live-support/participant/conversations/${conversation.id}/messages**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [{ id: 'message-11', conversationId: conversation.id, senderType: 'Guest', clientMessageId: 'client-11', type: 'Text', content: 'أحتاج متابعة الطلب', sentAt: new Date().toISOString() }], nextCursor: null, lastEventSequence: 1, missedEvents: [] } }) }));

    await page.goto(appUrl);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });

    await expect(page.getByText('الدعم غير متاح لبدء محادثة جديدة، لكن يمكنك متابعة محادثتك وسجلها الحالي.')).toBeVisible();
    await expect(page.getByRole('log').getByText('أحتاج متابعة الطلب')).toBeVisible();
    await expect(page.getByRole('button', { name: 'ابدأ المحادثة' })).toHaveCount(0);
  });

  test('failed participant send restores the conversation draft for retry', async ({ page }) => {
    const conversation = { id: '14200000-0000-0000-0000-000000000012', status: 'Active', participantType: 'Guest', subject: 'مسودة', createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false };
    let sendAttempts = 0;
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [conversation] }) }));
    await page.route(/\/live-support\/participant\/conversations\/[^/]+\/messages(?:\?.*)?$/, route => {
      if (route.request().method() === 'GET') return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [], nextCursor: null, lastEventSequence: 0, missedEvents: [] } }) });
      sendAttempts += 1;
      return route.fulfill(sendAttempts === 1
        ? { status: 409, contentType: 'application/json', body: JSON.stringify({ message: 'تغيرت الحالة' }) }
        : { status: 201, contentType: 'application/json', body: JSON.stringify({ success: true, data: { id: 'message-retry', conversationId: conversation.id, senderType: 'Guest', clientMessageId: 'retry-client', type: 'Text', content: 'رسالة أريد إعادة إرسالها', sentAt: new Date().toISOString() } }) });
    });

    await page.goto(appUrl);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });
    const input = page.getByLabel('رسالة الدعم');
    await expect(input).toBeVisible();
    await input.fill('رسالة أريد إعادة إرسالها');
    await page.getByRole('button', { name: 'إرسال' }).click();

    await expect(input).toHaveValue('رسالة أريد إعادة إرسالها');
    await expect(page.getByText('لم تُرسل الرسالة. أعد المحاولة.')).toBeVisible();
    await page.getByRole('button', { name: 'إرسال' }).click();
    await expect.poll(() => sendAttempts).toBe(2);
    await expect(input).toHaveValue('');
    await expect(page.getByRole('log').getByText('رسالة أريد إعادة إرسالها')).toHaveCount(1);
  });

  test('guest intake states that phone matching never links a student automatically', async ({ page }) => {
    await page.route('**/api/live-support/availability', (route) => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'الدعم متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', (route) => route.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));

    await page.goto(appUrl);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });

    await expect(page.getByText('لن نربط رقمك بحساب طالب تلقائيًا.')).toBeVisible();
    await expect(page.getByLabel('الاسم')).toBeVisible();
    await expect(page.getByRole('dialog').getByLabel('رقم الهاتف')).toBeVisible();
  });

  test('participant queue reconnect and large history snapshot stays deduplicated', async ({ page }) => {
    const conversation = { id: '14200000-0000-0000-0000-000000000010', status: 'Waiting', participantType: 'Guest', subject: 'اختبار الطابور', queuePosition: 2, createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false };
    const messages = Array.from({ length: 50 }, (_, index) => ({ id: `14200000-0000-0000-0000-${String(index).padStart(12, '0')}`, conversationId: conversation.id, senderType: index % 2 ? 'Staff' : 'Guest', clientMessageId: `client-${index}`, type: 'Text', content: `رسالة ${index + 1}`, sentAt: new Date(2026, 0, 1, 0, index).toISOString() }));
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 2, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [conversation] }) }));
    await page.route(`**/api/live-support/participant/conversations/${conversation.id}/messages**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: messages, nextCursor: 'next', lastEventSequence: 50, missedEvents: [] } }) }));
    await page.goto(appUrl); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });
    await expect(page.getByText(/أنت في الطابور.*رقم 2/)).toBeVisible();
    await expect(page.getByRole('log').getByText('رسالة 50')).toHaveCount(1);
    await page.reload(); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });
    await expect(page.getByRole('log').getByText('رسالة 50')).toHaveCount(1);
  });

  test('rating closed conversation is read-only and requires a new conversation', async ({ page }) => {
    let ratingCount = 0;
    const closed = { id: '14200000-0000-0000-0000-000000000011', status: 'Closed', participantType: 'Student', subject: 'مغلقة', createdAt: new Date().toISOString(), closedAt: new Date().toISOString(), version: 2, canSend: false, canRate: true };
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [closed] }) }));
    await page.route(`**/api/live-support/participant/conversations/${closed.id}/messages**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [], lastEventSequence: 1, missedEvents: [] } }) }));
    await page.route(`**/api/live-support/participant/conversations/${closed.id}/rating`, route => { ratingCount++; return route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ success: true, data: {} }) }); });
    await page.goto(appUrl); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });
    await expect(page.getByPlaceholder('اكتب رسالتك')).toHaveCount(0);
    await page.getByRole('button', { name: '5 نجوم' }).click();
    await expect.poll(() => ratingCount).toBe(1);
    await expect(page.getByRole('status')).toContainText('شكرًا لتقييمك 5 من 5 نجوم.');
    await expect(page.getByRole('button', { name: 'محادثة جديدة' })).toBeVisible();
  });

  test('rating failure exposes a retry without losing the closed conversation', async ({ page }) => {
    const closed = { id: '14200000-0000-0000-0000-000000000013', status: 'Closed', participantType: 'Student', subject: 'تقييم قابل لإعادة المحاولة', createdAt: new Date().toISOString(), closedAt: new Date().toISOString(), version: 2, canSend: false, canRate: true };
    let ratingAttempts = 0;
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [closed] }) }));
    await page.route(`**/api/live-support/participant/conversations/${closed.id}/messages**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [], lastEventSequence: 1, missedEvents: [] } }) }));
    await page.route(`**/api/live-support/participant/conversations/${closed.id}/rating`, route => {
      ratingAttempts += 1;
      return ratingAttempts === 1
        ? route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({}) })
        : route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ success: true, data: {} }) });
    });

    await page.goto(appUrl);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });
    await page.getByRole('button', { name: '4 نجوم' }).click();
    await expect(page.getByRole('dialog').getByRole('alert').filter({ hasText: 'تعذر حفظ التقييم' })).toBeVisible();
    await page.getByRole('button', { name: 'إعادة المحاولة' }).click();
    await expect.poll(() => ratingAttempts).toBe(2);
    await expect(page.getByRole('button', { name: 'محادثة جديدة' })).toBeVisible();
  });

  test('guest link privacy never performs automatic student candidate search', async ({ page }) => {
    let studentSearchRequests = 0;
    page.on('request', request => { if (request.url().includes('/students/search')) studentSearchRequests++; });
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
    await page.goto(appUrl); await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });
    await page.getByRole('dialog').getByLabel('رقم الهاتف').fill('01012345678');
    await page.waitForTimeout(500);
    expect(studentSearchRequests).toBe(0);
  });

  test('staff bootstrap 401 renders an authentication error instead of a blank workspace', async ({ page }) => {
    await installAssistantAuth(page);
    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ message: 'انتهت جلسة الموظف' }) }));
    await page.goto(`${staffUrl}/assistant/live-support`);
    await expect(page.getByRole('alert').filter({ hasText: 'انتهت جلسة الموظف' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'مركز الدعم المباشر' }).first()).toBeVisible();
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
    await seedE2E(request, 'E2E backend/seed is unavailable', true);
    const staffAuth = await login(request, 'assistant');
    await page.addInitScript(({ token, user }) => { localStorage.setItem('accessToken', token); localStorage.setItem('user', JSON.stringify({ id: user.id, fullName: user.fullName, phone: user.phone, roles: user.roles, permissions: user.permissions || [], profileComplete: user.profileComplete })); }, { token: staffAuth.accessToken, user: staffAuth.user });
    await page.goto(`${staffUrl}/assistant/live-support`);
    await expect(page.getByText('حالة الاتصال').locator('..')).toContainText('متصل', { timeout: 15_000 });
    await expect.poll(async () => (await (await request.get(`${apiUrl}/live-support/availability`)).json()).data.isAvailable, { timeout: 15_000 }).toBe(true);

    let guestA = await requestFactory.newContext({ baseURL: `${apiUrl}/`, extraHTTPHeaders: { 'X-E2E-Token': e2eToken } });
    let guestB = await requestFactory.newContext({ baseURL: `${apiUrl}/`, extraHTTPHeaders: { 'X-E2E-Token': e2eToken } });
    for (const [key, phone] of [['a', '01014000001'], ['b', '01014000002']] as const) {
      const guest = key === 'a' ? guestA : guestB;
      const session = await guest.post('live-support/guest/session', { data: { displayName: `زائر ${phone}`, phoneNumber: phone } });
      expect(session.ok()).toBeTruthy();
      const value = session.headers()['set-cookie']?.match(/(?:^|,\s*)massar_support_guest=([^;]+)/)?.[1];
      expect(value).toBeTruthy();
      await guest.dispose();
      const authenticated = await requestFactory.newContext({ baseURL: `${apiUrl}/`, extraHTTPHeaders: { 'X-E2E-Token': e2eToken, Cookie: `massar_support_guest=${value!}` } });
      if (key === 'a') guestA = authenticated; else guestB = authenticated;
    }
    const first = (await (await guestA.post('live-support/participant/conversations', { data: { subject: 'routing A' } })).json()).data;
    const second = (await (await guestB.post('live-support/participant/conversations', { data: { subject: 'routing B' } })).json()).data;
    expect(first.currentOwnerUserId).toBeTruthy();
    expect(second.status).toBe('Waiting');

    const token = staffAuth.accessToken;
    expect((await request.post(`${apiUrl}/live-support/staff/conversations/${first.id}/close`, { headers: bearer(token), data: { reason: 'تم الحل في اختبار السعة' } })).ok()).toBeTruthy();
    await expect.poll(async () => (await (await guestB.get(`live-support/participant/conversations/${second.id}`)).json()).data.status).toMatch(/Assigned|Active/);
    await guestA.dispose(); await guestB.dispose();
  });

  test('admin live support rating intervention requires an audited reason', async ({ page, request }) => {
    await seedE2E(request, 'E2E backend/seed is unavailable', true);
    const adminAuth = await login(request, 'admin');
    await page.addInitScript(({ token, user }) => { localStorage.setItem('accessToken', token); localStorage.setItem('user', JSON.stringify({ id: user.id, fullName: user.fullName, phone: user.phone, roles: user.roles, permissions: user.permissions || [], profileComplete: user.profileComplete })); }, { token: adminAuth.accessToken, user: adminAuth.user });
    await page.goto(`${adminUrl}/admin/live-support`);
    await expect(page.getByRole('heading', { name: 'أداء الموظفين والتقييمات' })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('heading', { name: /الموظفون والسعة/ })).toBeVisible();
    const rejected = await request.post(`${apiUrl}/live-support/admin/conversations/${crypto.randomUUID()}/intervene`, { headers: bearer(adminAuth.accessToken), data: { operation: 'close', reason: '' } });
    expect(rejected.status()).toBe(409);
  });

  test('unavailable support still lists an existing conversation for its participant', async ({ request }) => {
    await seedE2E(request, 'E2E backend/seed is unavailable', true);
    const adminAuth = await login(request, 'admin');
    await request.put(`${apiUrl}/live-support/admin/feature`, { headers: bearer(adminAuth.accessToken), data: { enabled: true } });
    const guest = await createGuestSession('unavailable-with-history');
    try {
      const created = await guest.post('live-support/participant/conversations', { data: { subject: 'تظل ظاهرة عند توقف الدعم' } });
      expect(created.status()).toBe(201);
      const disabled = await request.put(`${apiUrl}/live-support/admin/feature`, { headers: bearer(adminAuth.accessToken), data: { enabled: false } });
      expect(disabled.ok()).toBeTruthy();
      const availability = await request.get(`${apiUrl}/live-support/availability`);
      expect((await availability.json()).data.isAvailable).toBe(false);
      const history = await guest.get('live-support/participant/conversations');
      expect(history.ok()).toBeTruthy();
      expect((await history.json()).data).toHaveLength(1);
    } finally {
      await request.put(`${apiUrl}/live-support/admin/feature`, { headers: bearer(adminAuth.accessToken), data: { enabled: true } });
      await guest.dispose();
    }
  });

  test('duplicate close and transfer requests are rejected as idempotent ownership conflicts', async ({ request }) => {
    await seedE2E(request, 'E2E backend/seed is unavailable', true);
    const adminAuth = await login(request, 'admin');
    await request.put(`${apiUrl}/live-support/admin/feature`, { headers: bearer(adminAuth.accessToken), data: { enabled: true } });
    const guest = await createGuestSession('duplicate-actions');
    try {
      const created = await guest.post('live-support/participant/conversations', { data: { subject: 'اختبار تكرار الإجراءات' } });
      expect(created.status()).toBe(201);
      const conversation = (await created.json()).data;
      const firstClose = await request.post(`${apiUrl}/live-support/admin/conversations/${conversation.id}/intervene`, { headers: bearer(adminAuth.accessToken), data: { operation: 'close', reason: 'إغلاق أول' } });
      expect(firstClose.ok()).toBeTruthy();
      const duplicateClose = await request.post(`${apiUrl}/live-support/admin/conversations/${conversation.id}/intervene`, { headers: bearer(adminAuth.accessToken), data: { operation: 'close', reason: 'إغلاق مكرر' } });
      expect(duplicateClose.status()).toBe(409);

      const second = await guest.post('live-support/participant/conversations', { data: { subject: 'اختبار تحويل مكرر' } });
      expect(second.status()).toBe(201);
      const secondConversation = (await second.json()).data;
      const config = await request.get(`${apiUrl}/live-support/admin/config`, { headers: bearer(adminAuth.accessToken) });
      expect(config.ok()).toBeTruthy();
      const target = (await config.json()).data.staff.find((staff: { userId: string }) => staff.userId !== conversation.currentOwnerUserId)?.userId;
      expect(target, 'E2E seed must expose a second live-support staff target').toBeTruthy();
      const firstTransfer = await request.post(`${apiUrl}/live-support/admin/conversations/${secondConversation.id}/intervene`, { headers: bearer(adminAuth.accessToken), data: { operation: 'transfer', targetStaffUserId: target, reason: 'تحويل أول' } });
      expect(firstTransfer.ok()).toBeTruthy();
      const duplicateTransfer = await request.post(`${apiUrl}/live-support/admin/conversations/${secondConversation.id}/intervene`, { headers: bearer(adminAuth.accessToken), data: { operation: 'transfer', targetStaffUserId: target, reason: 'تحويل مكرر' } });
      expect(duplicateTransfer.status()).toBe(409);
    } finally { await guest.dispose(); }
  });
});

test.describe('live support client boundary contracts (synthetic HTTP only)', () => {
  test('malformed availability envelope fails closed instead of enabling chat', async ({ page }) => {
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { code: 'MALFORMED_NO_AVAILABILITY_FLAG' } }) }));
    await page.goto(appUrl);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click({ force: true });
    await expect(page.getByRole('heading', { name: 'الدعم غير متاح الآن' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'ابدأ المحادثة' })).toHaveCount(0);
  });

  test('stale availability response cannot overwrite the newer client-boundary result', async ({ page }) => {
    let calls = 0;
    await page.route('**/api/live-support/availability', async route => {
      calls += 1;
      if (calls === 1) await new Promise(resolve => setTimeout(resolve, 500));
      await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: calls === 1 ? { isAvailable: false, availableStaffCount: 0, code: 'STALE' } : { isAvailable: true, availableStaffCount: 1, code: 'AVAILABLE' } }) });
    });
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) }));
    await page.goto(appUrl);
    const launcher = page.getByRole('button', { name: 'فتح الدعم المباشر' });
    await launcher.click({ force: true });
    await page.getByRole('dialog').getByRole('button', { name: 'إغلاق', exact: true }).click();
    await launcher.click({ force: true });
    await expect(page.getByRole('button', { name: 'ابدأ المحادثة' })).toBeVisible();
  });

  test('switching conversations does not expose a draft from another conversation', async ({ page }) => {
    const staffId = 'a0000000-0000-0000-0000-000000000099';
    await installAssistantAuth(page);
    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isEnabled: true, isCheckedIn: true, waitingCount: 0, activeCount: 2, maxActiveConversations: 2, conversations: [{ id: 'a0000000-0000-0000-0000-000000000001', subject: 'أولى', status: 'Active', participantType: 'Guest', participantName: 'أولى', currentOwnerUserId: staffId }, { id: 'a0000000-0000-0000-0000-000000000002', subject: 'ثانية', status: 'Active', participantType: 'Guest', participantName: 'ثانية', currentOwnerUserId: staffId }] } }) }));
    await page.route('**/api/live-support/staff/conversations/*/messages**', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) }));
    await page.goto(`${staffUrl}/assistant/live-support`);
    await expect(page.getByRole('heading', { name: 'أولى', exact: true })).toBeVisible();
    await page.getByRole('option', { name: /أولى/ }).click();
    await page.getByLabel('رد موظف الدعم').fill('مسودة المحادثة الأولى');
    await page.getByRole('option', { name: /ثانية/ }).click();
    await expect(page.getByLabel('رد موظف الدعم')).not.toHaveValue('مسودة المحادثة الأولى');
  });

  // Regression 2026-08-26: a bootstrap started for A could reclaim selection
  // after the employee selected B, abort B's message request, and show A again.
  test('delayed staff bootstrap cannot overwrite a newer conversation selection', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await installAssistantAuth(page);
    const staffId = 'a0000000-0000-0000-0000-000000000099';
    const conversationA = {
      id: 'a0000000-0000-0000-0000-000000000011',
      subject: 'طلب أ',
      status: 'Active',
      participantType: 'Guest',
      participantName: 'عميل أ',
      currentOwnerUserId: staffId,
      channel: 'Web',
      createdAt: '2026-08-26T10:00:00.000Z',
      version: 1,
      canSend: true,
      canRate: false,
    };
    const conversationB = {
      ...conversationA,
      id: 'a0000000-0000-0000-0000-000000000012',
      subject: 'طلب ب',
      participantName: 'عميل ب',
      createdAt: '2026-08-26T10:01:00.000Z',
    };
    let delayNextBootstrap = false;
    let delayedBootstrapRequests = 0;
    let releaseBootstrap = () => {};
    const bootstrapGate = new Promise<void>((resolve) => {
      releaseBootstrap = resolve;
    });
    let bMessageRequests = 0;
    let releaseBMessages = () => {};
    const bMessagesGate = new Promise<void>((resolve) => {
      releaseBMessages = resolve;
    });

    await page.route('**/api/live-support/staff/bootstrap', async route => {
      let waitingCount = 0;
      if (delayNextBootstrap) {
        delayNextBootstrap = false;
        delayedBootstrapRequests += 1;
        await bootstrapGate;
        waitingCount = 7;
      }
      await route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: { isEnabled: true, isCheckedIn: true, waitingCount, activeCount: 2, maxActiveConversations: 2, conversations: [conversationA, conversationB], cannedReplies: [] } }),
      }).catch(() => undefined);
    });
    await page.route('**/api/live-support/staff/conversations/*/messages**', async route => {
      const url = route.request().url();
      if (route.request().method() !== 'GET') {
        const sentMessage = {
          id: 'a0000000-0000-0000-0000-000000000091',
          conversationId: conversationA.id,
          senderType: 'Staff',
          clientMessageId: 'selection-race-send',
          type: 'Text',
          content: 'تشغيل تحديث الخلفية',
          sentAt: '2026-08-26T10:03:00.000Z',
        };
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { message: sentMessage, replayed: false } }) });
      }
      const isConversationB = url.includes(conversationB.id);
      if (isConversationB) {
        bMessageRequests += 1;
        await bMessagesGate;
      }
      const conversationId = isConversationB ? conversationB.id : conversationA.id;
      const content = isConversationB ? 'رسالة ب الصحيحة' : 'رسالة أ القديمة';
      await route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: [{ id: `${conversationId}-message`, conversationId, senderType: 'Guest', clientMessageId: `${conversationId}-client`, type: 'Text', content, sentAt: '2026-08-26T10:02:00.000Z' }] }),
      }).catch(() => undefined);
    });

    await page.goto(`${staffUrl}/assistant/live-support`);
    const conversationAMessage = page.locator(
      `[data-live-support-message-id="${conversationA.id}-message"]`
    );
    await expect(conversationAMessage).toContainText('رسالة أ القديمة');
    delayNextBootstrap = true;
    await page.getByLabel('رد موظف الدعم').fill('تشغيل تحديث الخلفية');
    await page.getByRole('button', { name: 'إرسال الرد' }).click();
    await expect.poll(() => delayedBootstrapRequests).toBe(1);

    const optionB = page.getByRole('option', { name: /عميل ب/ });
    await optionB.click();
    await expect.poll(() => bMessageRequests).toBe(1);
    await expect(optionB).toHaveAttribute('aria-selected', 'true');

    releaseBootstrap();
    await expect(page.getByText('7 بانتظار التوزيع', { exact: true })).toBeVisible();
    await expect(optionB).toHaveAttribute('aria-selected', 'true');
    await expect(page.getByRole('heading', { name: 'عميل ب', exact: true })).toBeVisible();

    releaseBMessages();
    await expect(page.locator(
      `[data-live-support-message-id="${conversationB.id}-message"]`
    )).toContainText('رسالة ب الصحيحة');
    await expect(conversationAMessage).toHaveCount(0);
  });

  // Regression 2026-08-26: two same-selection head refreshes can resolve out
  // of order even when abort is requested; only the latest request may apply.
  test('late WhatsApp head response cannot rewind the newer head frontier', async ({ page }) => {
    await installAssistantAuth(page);
    const conversationId = 'b3000000-0000-0000-0000-000000000001';
    const conversation = {
      id: conversationId,
      subject: 'ترتيب تحديث واتساب',
      status: 'Active',
      participantType: 'Guest',
      participantName: 'عميل ترتيب التحديث',
      currentOwnerUserId: 'a0000000-0000-0000-0000-000000000099',
      channel: 'WhatsApp',
      externalPhoneNumber: '01000000005',
      customerServiceWindowExpiresAt: '2099-08-26T12:00:00.000Z',
      createdAt: '2026-08-26T11:00:00.000Z',
      version: 1,
      canSend: true,
      canRate: false,
    };
    const baseMessage = {
      id: 'b3000000-0000-0000-0000-000000000010',
      conversationId,
      senderType: 'Guest',
      clientMessageId: 'ordered-base',
      type: 'Text',
      content: 'الرأس الأساسي',
      sentAt: '2026-08-26T12:00:00.000Z',
    };
    const newerHeadMessage = {
      ...baseMessage,
      id: 'b3000000-0000-0000-0000-000000000011',
      clientMessageId: 'ordered-newer',
      content: 'الرأس الأحدث الصحيح',
      sentAt: '2026-08-26T12:01:00.000Z',
    };
    const staleHeadMessage = {
      ...baseMessage,
      id: 'b3000000-0000-0000-0000-000000000012',
      clientMessageId: 'ordered-stale',
      content: 'الرأس المتأخر الخاطئ',
      sentAt: '2026-08-26T12:02:00.000Z',
    };
    let preSendHeadRequests = 0;
    let preSendHeadSettled = 0;
    let postSendHeadRequests = 0;
    let sentMessages = 0;
    let messageSent = false;
    let releasePreSendHeads = () => {};
    const preSendHeadGate = new Promise<void>((resolve) => { releasePreSendHeads = resolve; });
    const requestedCursors: Array<string | null> = [];

    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { isEnabled: true, isCheckedIn: true, waitingCount: 0, activeCount: 1, maxActiveConversations: 2, conversations: [conversation], cannedReplies: [] } }),
    }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/whatsapp-thread/messages**`, async route => {
      const cursor = new URL(route.request().url()).searchParams.get('cursor');
      requestedCursors.push(cursor);
      if (cursor === 'historical-frontier') {
        await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [], nextCursor: null } }) });
        return;
      }
      if (!messageSent) {
        preSendHeadRequests += 1;
        await preSendHeadGate;
        await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [staleHeadMessage], nextCursor: 'stale-bridge' } }) }).catch(() => undefined);
        preSendHeadSettled += 1;
        return;
      }
      postSendHeadRequests += 1;
      await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [baseMessage, newerHeadMessage], nextCursor: 'historical-frontier' } }) });
    });
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/messages`, route => {
      sentMessages += 1;
      messageSent = true;
      const message = {
        ...baseMessage,
        id: `b3000000-0000-0000-0000-${String(100 + sentMessages).padStart(12, '0')}`,
        senderType: 'Staff',
        clientMessageId: `ordered-local-${sentMessages}`,
        content: `تشغيل التحديث ${sentMessages}`,
        sentAt: new Date(Date.UTC(2026, 7, 26, 13, 0, sentMessages)).toISOString(),
      };
      return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { message, replayed: false } }) });
    });

    await page.goto(`${staffUrl}/assistant/live-support`);
    await expect.poll(() => preSendHeadRequests).toBeGreaterThanOrEqual(1);
    await expect(page.getByText('جارٍ تحميل الرسائل…', { exact: true })).toBeVisible();
    await page.getByLabel('رد موظف الدعم').fill('تشغيل التحديث 1');
    await page.getByRole('button', { name: 'إرسال الرد' }).click();
    await expect.poll(() => postSendHeadRequests).toBeGreaterThanOrEqual(1);
    await expect(page.locator('[data-live-support-message-id="b3000000-0000-0000-0000-000000000011"]')).toContainText('الرأس الأحدث الصحيح');
    await expect(page.getByText('جارٍ تحميل الرسائل…', { exact: true })).toHaveCount(0);

    const staleRequestsBeforeRelease = preSendHeadRequests;
    releasePreSendHeads();
    await expect.poll(() => preSendHeadSettled).toBe(staleRequestsBeforeRelease);
    await expect(page.locator('[data-live-support-message-id="b3000000-0000-0000-0000-000000000012"]')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'استكمال الرسائل الناقصة' })).toHaveCount(0);
    await page.getByRole('button', { name: 'تحميل الرسائل الأقدم' }).click();
    await expect.poll(() => requestedCursors.filter((cursor) => cursor === 'historical-frontier').length).toBe(1);
    expect(requestedCursors).not.toContain('stale-bridge');
  });

  // Regression 2026-08-26: reopening WhatsApp hid prior episodes and refreshes dropped loaded pages.
  test('staff WhatsApp thread keeps older pages visible after a head refresh', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await installAssistantAuth(page);
    const staffId = 'a0000000-0000-0000-0000-000000000099';
    const currentConversationId = 'a0000000-0000-0000-0000-000000000021';
    const previousConversationId = 'a0000000-0000-0000-0000-000000000020';
    const olderAttachmentId = 'a0000000-0000-0000-0000-000000000099';
    const conversation = {
      id: currentConversationId,
      subject: 'متابعة واتساب',
      status: 'Active',
      participantType: 'Guest',
      participantName: 'عميل واتساب',
      currentOwnerUserId: staffId,
      channel: 'WhatsApp',
      externalPhoneNumber: '01000000000',
      customerServiceWindowExpiresAt: '2099-08-26T12:00:00.000Z',
      createdAt: '2026-08-26T11:00:00.000Z',
      version: 1,
      canSend: true,
      canRate: false,
    };
    const currentMessages = Array.from({ length: 24 }, (_, index) => ({
      id: `a1000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
      conversationId: currentConversationId,
      senderType: index % 2 ? 'Staff' : 'Guest',
      clientMessageId: `current-${index + 1}`,
      type: 'Text',
      content: `الحالية ${index + 1}`,
      sentAt: new Date(Date.UTC(2026, 7, 26, 12, index)).toISOString(),
    }));
    const olderMessages = Array.from({ length: 12 }, (_, index) => ({
      id: `a0000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
      conversationId: previousConversationId,
      senderType: index % 2 ? 'Staff' : 'Guest',
      clientMessageId: `older-${index + 1}`,
      type: index === 0 ? 'Image' : 'Text',
      content: `السابقة ${index + 1}`,
      sentAt: new Date(Date.UTC(2026, 7, 25, 12, index)).toISOString(),
      attachmentId: index === 0 ? olderAttachmentId : null,
    }));
    let sentMessage: (typeof currentMessages)[number] | undefined;
    let headRequests = 0;
    let legacyMessageGets = 0;
    let threadAttachmentGets = 0;
    let legacyAttachmentGets = 0;

    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { isEnabled: true, isCheckedIn: true, waitingCount: 0, activeCount: 1, maxActiveConversations: 2, conversations: [conversation], cannedReplies: [] } }),
    }));
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/whatsapp-thread/messages**`, route => {
      const cursor = new URL(route.request().url()).searchParams.get('cursor');
      if (cursor === 'older-page') {
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: olderMessages, nextCursor: null } }) });
      }
      headRequests += 1;
      return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: sentMessage ? [...currentMessages, sentMessage] : currentMessages, nextCursor: 'older-page' } }) });
    });
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/messages`, route => {
      if (route.request().method() === 'GET') {
        legacyMessageGets += 1;
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) });
      }
      sentMessage = {
        ...currentMessages.at(-1)!,
        id: 'a2000000-0000-0000-0000-000000000001',
        clientMessageId: 'sent-current',
        content: 'رد بعد تحميل القديم',
        sentAt: '2026-08-26T13:00:00.000Z',
      };
      return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { message: sentMessage, replayed: false } }) });
    });
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/whatsapp-thread/attachments/${olderAttachmentId}`, route => {
      threadAttachmentGets += 1;
      return route.fulfill({
        contentType: 'image/png',
        body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64'),
      });
    });
    await page.route(`**/api/live-support/staff/conversations/${previousConversationId}/attachments/${olderAttachmentId}`, route => {
      legacyAttachmentGets += 1;
      return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
    });

    await page.goto(`${staffUrl}/assistant/live-support`);
    await expect(page.locator('[data-live-support-message-id="a1000000-0000-0000-0000-000000000024"]')).toContainText('الحالية 24', { timeout: 15_000 });
    const viewport = page.getByLabel('سجل رسائل المحادثة');
    await viewport.evaluate((element) => { element.scrollTop = 0; });
    const firstCurrentMessage = page.locator('[data-live-support-message-id="a1000000-0000-0000-0000-000000000001"]');
    const beforePrepend = await firstCurrentMessage.boundingBox();
    expect(beforePrepend).toBeTruthy();

    await page.getByRole('button', { name: 'تحميل الرسائل الأقدم' }).click();
    const firstOlderMessage = page.locator('[data-live-support-message-id="a0000000-0000-0000-0000-000000000001"]');
    await expect(firstOlderMessage).toBeAttached();
    await expect.poll(() => threadAttachmentGets).toBeGreaterThan(0);
    await expect(firstOlderMessage.getByRole('link', { name: 'فتح الصورة بالحجم الكامل' })).toBeVisible();
    await expect(page.getByRole('separator', { name: 'بداية محادثة سابقة' })).toHaveCount(1);
    await expect(page.getByRole('separator', { name: 'بداية المحادثة الحالية' })).toHaveCount(1);
    const afterPrepend = await firstCurrentMessage.boundingBox();
    expect(afterPrepend).toBeTruthy();
    expect(Math.abs(afterPrepend!.y - beforePrepend!.y)).toBeLessThan(4);

    const headRequestsBeforeSend = headRequests;
    await page.getByLabel('رد موظف الدعم').fill('رد بعد تحميل القديم');
    await page.getByRole('button', { name: 'إرسال الرد' }).click();
    await expect.poll(() => headRequests).toBeGreaterThan(headRequestsBeforeSend);
    await expect(firstOlderMessage).toHaveCount(1);
    await expect(page.locator('[data-live-support-message-id="a2000000-0000-0000-0000-000000000001"]')).toContainText('رد بعد تحميل القديم');
    expect(legacyMessageGets).toBe(0);
    expect(legacyAttachmentGets).toBe(0);
  });

  // Regression 2026-08-26: when more than one page arrived between head
  // refreshes, the preserved historical cursor skipped the unseen middle.
  test('staff WhatsApp thread bridges a disjoint realtime head before resuming older history', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await installAssistantAuth(page);
    const staffId = 'b0000000-0000-0000-0000-000000000099';
    const currentConversationId = 'b0000000-0000-0000-0000-000000000021';
    const previousConversationId = 'b0000000-0000-0000-0000-000000000020';
    const conversation = {
      id: currentConversationId,
      subject: 'دفعة واتساب كبيرة',
      status: 'Active',
      participantType: 'Guest',
      participantName: 'عميل دفعة واتساب',
      currentOwnerUserId: staffId,
      channel: 'WhatsApp',
      externalPhoneNumber: '01000000004',
      customerServiceWindowExpiresAt: '2099-08-26T12:00:00.000Z',
      createdAt: '2026-08-26T11:00:00.000Z',
      version: 1,
      canSend: true,
      canRate: false,
    };
    const makeCurrentMessage = (number: number) => ({
      id: `b1000000-0000-0000-0000-${String(number).padStart(12, '0')}`,
      conversationId: currentConversationId,
      senderType: number % 2 ? 'Guest' : 'Staff',
      clientMessageId: `rollover-${number}`,
      type: 'Text',
      content: `رسالة الدفعة ${number}`,
      sentAt: new Date(Date.UTC(2026, 7, 26, 12, 0, number)).toISOString(),
    });
    const initialHead = Array.from({ length: 50 }, (_, index) =>
      makeCurrentMessage(index + 1)
    );
    const rolledHead = Array.from({ length: 50 }, (_, index) =>
      makeCurrentMessage(index + 52)
    );
    const bridgePage = Array.from({ length: 50 }, (_, index) =>
      makeCurrentMessage(index + 2)
    );
    const historicalMessage = {
      id: 'b0000000-0000-0000-0000-000000000001',
      conversationId: previousConversationId,
      senderType: 'Guest',
      clientMessageId: 'rollover-history',
      type: 'Text',
      content: 'رسالة من جلسة واتساب الأقدم',
      sentAt: '2026-08-25T12:00:00.000Z',
    };
    const locallySentMessage = {
      ...makeCurrentMessage(200),
      id: 'b2000000-0000-0000-0000-000000000001',
      senderType: 'Staff',
      clientMessageId: 'rollover-local-send',
      content: 'تشغيل تحديث الدفعة',
    };
    let rolled = false;
    let headRequests = 0;
    const requestedCursors: Array<string | null> = [];

    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { isEnabled: true, isCheckedIn: true, waitingCount: 0, activeCount: 1, maxActiveConversations: 2, conversations: [conversation], cannedReplies: [] } }),
    }));
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/whatsapp-thread/messages**`, route => {
      const cursor = new URL(route.request().url()).searchParams.get('cursor');
      requestedCursors.push(cursor);
      if (cursor === 'bridge-page') {
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: bridgePage, nextCursor: 'unused-after-overlap' } }) });
      }
      if (cursor === 'historical-page') {
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: [historicalMessage], nextCursor: null } }) });
      }
      headRequests += 1;
      return route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: rolled
          ? { items: rolledHead, nextCursor: 'bridge-page' }
          : { items: initialHead, nextCursor: 'historical-page' } }),
      });
    });
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/messages`, route => {
      if (route.request().method() === 'GET') {
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) });
      }
      rolled = true;
      return route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: { message: locallySentMessage, replayed: false } }),
      });
    });

    await page.goto(`${staffUrl}/assistant/live-support`);
    await expect(page.locator('[data-live-support-message-id="b1000000-0000-0000-0000-000000000050"]')).toContainText('رسالة الدفعة 50');
    const headRequestsBeforeSend = headRequests;
    await page.getByLabel('رد موظف الدعم').fill('تشغيل تحديث الدفعة');
    await page.getByRole('button', { name: 'إرسال الرد' }).click();
    await expect.poll(() => headRequests).toBeGreaterThan(headRequestsBeforeSend);
    await expect(page.locator('[data-live-support-message-id="b1000000-0000-0000-0000-000000000101"]')).toContainText('رسالة الدفعة 101');
    await expect(page.locator('[data-live-support-message-id="b1000000-0000-0000-0000-000000000051"]')).toHaveCount(0);

    await page.getByRole('button', { name: 'استكمال الرسائل الناقصة' }).click();
    await expect(page.locator('[data-live-support-message-id="b1000000-0000-0000-0000-000000000051"]')).toContainText('رسالة الدفعة 51');
    await expect(page.getByRole('button', { name: 'تحميل الرسائل الأقدم' })).toBeVisible();
    await page.getByRole('button', { name: 'تحميل الرسائل الأقدم' }).click();
    await expect(page.locator('[data-live-support-message-id="b0000000-0000-0000-0000-000000000001"]')).toContainText('رسالة من جلسة واتساب الأقدم');
    await expect(page.getByRole('button', { name: /الرسائل الأقدم|الرسائل الناقصة/ })).toHaveCount(0);
    expect(requestedCursors.filter((cursor) => cursor === 'bridge-page')).toHaveLength(1);
    expect(requestedCursors.filter((cursor) => cursor === 'historical-page')).toHaveLength(1);
  });

  // Regression 2026-08-26: the admin investigation still used the capped
  // single-conversation endpoint even when an open WhatsApp thread had history.
  test('admin WhatsApp investigation pages the full thread and preserves it on refresh', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const currentConversationId = 'a0000000-0000-0000-0000-000000000041';
    const previousConversationId = 'a0000000-0000-0000-0000-000000000040';
    const olderAttachmentId = 'a0000000-0000-0000-0000-000000000049';
    const conversation = {
      id: currentConversationId,
      participantName: 'مدير واتساب',
      participantType: 'Guest',
      status: 'Active',
      ownerName: 'موظف الدعم',
      createdAt: '2026-08-26T11:00:00.000Z',
      subject: 'تحقيق سجل واتساب',
      channel: 'WhatsApp',
      externalPhoneNumber: '01000000002',
      customerServiceWindowExpiresAt: '2099-08-26T12:00:00.000Z',
    };
    const currentMessages = Array.from({ length: 24 }, (_, index) => ({
      id: `a4000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
      conversationId: currentConversationId,
      senderType: index % 2 ? 'Staff' : 'Guest',
      clientMessageId: `admin-current-${index + 1}`,
      type: 'Text',
      content: `تحقيق الحالية ${index + 1}`,
      sentAt: new Date(Date.UTC(2026, 7, 26, 12, index)).toISOString(),
    }));
    const olderMessages = Array.from({ length: 12 }, (_, index) => ({
      id: `a3000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
      conversationId: previousConversationId,
      senderType: index % 2 ? 'Staff' : 'Guest',
      clientMessageId: `admin-older-${index + 1}`,
      type: index === 0 ? 'Image' : 'Text',
      content: `تحقيق السابقة ${index + 1}`,
      sentAt: new Date(Date.UTC(2026, 7, 25, 12, index)).toISOString(),
      attachmentId: index === 0 ? olderAttachmentId : null,
    }));
    const sentMessage = {
      ...currentMessages.at(-1)!,
      id: 'a5000000-0000-0000-0000-000000000001',
      clientMessageId: 'admin-sent-current',
      senderType: 'Admin',
      content: 'رد الإدارة بعد تحميل القديم',
      sentAt: '2026-08-26T13:00:00.000Z',
    };
    let headRequests = 0;
    let legacyMessageGets = 0;
    let threadAttachmentGets = 0;
    let legacyAttachmentGets = 0;

    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/whatsapp-thread/messages**`, route => {
      const cursor = new URL(route.request().url()).searchParams.get('cursor');
      if (cursor === 'admin-older-page') {
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: olderMessages, nextCursor: null } }) });
      }
      headRequests += 1;
      return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { items: currentMessages, nextCursor: 'admin-older-page' } }) });
    });
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/messages`, route => {
      if (route.request().method() === 'GET') {
        legacyMessageGets += 1;
        return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) });
      }
      return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { message: sentMessage, replayed: false } }) });
    });
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/whatsapp-thread/attachments/${olderAttachmentId}`, route => {
      threadAttachmentGets += 1;
      return route.fulfill({
        contentType: 'image/png',
        body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64'),
      });
    });
    await page.route(`**/api/live-support/staff/conversations/${previousConversationId}/attachments/${olderAttachmentId}`, route => {
      legacyAttachmentGets += 1;
      return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
    });

    await openSyntheticAdminInvestigation(page, conversation);
    await expect(page.locator('[data-live-support-message-id="a4000000-0000-0000-0000-000000000024"]')).toContainText('تحقيق الحالية 24', { timeout: 15_000 });

    const viewport = page.getByLabel('سجل رسائل المحادثة');
    await viewport.evaluate((element) => { element.scrollTop = 0; });
    const firstCurrentMessage = page.locator('[data-live-support-message-id="a4000000-0000-0000-0000-000000000001"]');
    const beforePrepend = await firstCurrentMessage.boundingBox();
    expect(beforePrepend).toBeTruthy();

    await page.getByRole('button', { name: 'تحميل الرسائل الأقدم' }).click();
    const firstOlderMessage = page.locator('[data-live-support-message-id="a3000000-0000-0000-0000-000000000001"]');
    await expect(firstOlderMessage).toBeAttached();
    await expect.poll(() => threadAttachmentGets).toBeGreaterThan(0);
    await expect(firstOlderMessage.getByRole('link', { name: 'فتح الصورة بالحجم الكامل' })).toBeVisible();
    await expect(page.getByRole('separator', { name: 'بداية جلسة واتساب أخرى' })).toHaveCount(1);
    await expect(page.getByRole('separator', { name: 'بداية المحادثة المحددة' })).toHaveCount(1);
    const afterPrepend = await firstCurrentMessage.boundingBox();
    expect(afterPrepend).toBeTruthy();
    expect(Math.abs(afterPrepend!.y - beforePrepend!.y)).toBeLessThan(4);

    const headRequestsBeforeSend = headRequests;
    await page.getByLabel('رد الإدارة على المحادثة').fill('رد الإدارة بعد تحميل القديم');
    await page.getByRole('button', { name: 'إرسال الرسالة' }).click();
    await expect.poll(() => headRequests).toBeGreaterThan(headRequestsBeforeSend);
    await expect(firstOlderMessage).toHaveCount(1);
    await expect(page.locator('[data-live-support-message-id="a5000000-0000-0000-0000-000000000001"]')).toContainText('رد الإدارة بعد تحميل القديم');
    expect(legacyMessageGets).toBe(0);
    expect(legacyAttachmentGets).toBe(0);
  });

  // Regression 2026-08-26: terminal WhatsApp anchors are valid thread roots;
  // the admin must see their earlier episodes without regaining send controls.
  test('closed admin WhatsApp investigation keeps cross-episode history read-only', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const currentConversationId = 'a0000000-0000-0000-0000-000000000051';
    const previousConversationId = 'a0000000-0000-0000-0000-000000000050';
    const conversation = {
      id: currentConversationId,
      participantName: 'سجل واتساب مغلق',
      participantType: 'Guest',
      status: 'Closed',
      ownerName: 'موظف سابق',
      createdAt: '2026-08-26T11:00:00.000Z',
      closedAt: '2026-08-26T13:00:00.000Z',
      subject: 'تحقيق مغلق',
      channel: 'WhatsApp',
      externalPhoneNumber: '01000000003',
      customerServiceWindowExpiresAt: '2026-08-26T12:00:00.000Z',
    };
    const currentMessage = {
      id: 'a7000000-0000-0000-0000-000000000001',
      conversationId: currentConversationId,
      senderType: 'Staff',
      clientMessageId: 'closed-current',
      type: 'Text',
      content: 'آخر رسالة قبل الإغلاق',
      sentAt: '2026-08-26T12:30:00.000Z',
    };
    const previousMessage = {
      id: 'a6000000-0000-0000-0000-000000000001',
      conversationId: previousConversationId,
      senderType: 'Guest',
      clientMessageId: 'closed-previous',
      type: 'Text',
      content: 'رسالة من المحادثة السابقة',
      sentAt: '2026-08-25T12:30:00.000Z',
    };
    let legacyMessageGets = 0;

    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/whatsapp-thread/messages**`, route => {
      const cursor = new URL(route.request().url()).searchParams.get('cursor');
      return route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: cursor === 'closed-older-page'
          ? { items: [previousMessage], nextCursor: null }
          : { items: [currentMessage], nextCursor: 'closed-older-page' } }),
      });
    });
    await page.route(`**/api/live-support/staff/conversations/${currentConversationId}/messages`, route => {
      if (route.request().method() === 'GET') legacyMessageGets += 1;
      return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) });
    });

    await openSyntheticAdminInvestigation(page, conversation);
    await expect(page.locator('[data-live-support-message-id="a7000000-0000-0000-0000-000000000001"]')).toContainText('آخر رسالة قبل الإغلاق');
    await page.getByRole('button', { name: 'تحميل الرسائل الأقدم' }).click();
    await expect(page.locator('[data-live-support-message-id="a6000000-0000-0000-0000-000000000001"]')).toContainText('رسالة من المحادثة السابقة');
    await expect(page.getByRole('separator', { name: 'بداية جلسة واتساب أخرى' })).toHaveCount(1);
    await expect(page.getByRole('separator', { name: 'بداية المحادثة المحددة' })).toHaveCount(1);
    await expect(page.getByLabel('رد الإدارة على المحادثة')).toBeDisabled();
    await expect(page.getByRole('button', { name: 'إرسال الرسالة' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'قالب واتساب' })).toHaveCount(0);
    expect(legacyMessageGets).toBe(0);
  });

  test('admin Web investigation keeps the exact conversation message contract', async ({ page }) => {
    const conversationId = 'a0000000-0000-0000-0000-000000000061';
    const conversation = {
      id: conversationId,
      participantName: 'مستخدم الموقع',
      participantType: 'Student',
      status: 'Active',
      ownerName: 'موظف الموقع',
      createdAt: '2026-08-26T11:00:00.000Z',
      subject: 'دعم الموقع',
      channel: 'Web',
    };
    let exactMessageGets = 0;
    let threadMessageGets = 0;

    await page.route(`**/api/live-support/staff/conversations/${conversationId}/messages**`, route => {
      exactMessageGets += 1;
      return route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: [{ id: 'a8000000-0000-0000-0000-000000000001', conversationId, senderType: 'Student', clientMessageId: 'web-exact', type: 'Text', content: 'رسالة الموقع الحالية', sentAt: '2026-08-26T12:00:00.000Z' }] }),
      });
    });
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/whatsapp-thread/messages**`, route => {
      threadMessageGets += 1;
      return route.fulfill({ status: 500, contentType: 'application/json', body: '{}' });
    });

    await openSyntheticAdminInvestigation(page, conversation);
    await expect(page.locator('[data-live-support-message-id="a8000000-0000-0000-0000-000000000001"]')).toContainText('رسالة الموقع الحالية');
    await expect(page.getByRole('button', { name: 'تحميل الرسائل الأقدم' })).toHaveCount(0);
    await expect(page.getByLabel('رد الإدارة على المحادثة')).toBeEnabled();
    expect(exactMessageGets).toBeGreaterThan(0);
    expect(threadMessageGets).toBe(0);
  });

  // Regression 2026-08-26: a delayed history response could replace a newer selection.
  test('late student-history response cannot overwrite the newly selected conversation', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await installAssistantAuth(page);
    const staffId = 'a0000000-0000-0000-0000-000000000099';
    const conversationId = 'a0000000-0000-0000-0000-000000000031';
    const historyAId = 'a0000000-0000-0000-0000-000000000032';
    const historyBId = 'a0000000-0000-0000-0000-000000000033';
    const conversation = {
      id: conversationId,
      subject: 'سجل الطالب',
      status: 'Active',
      participantType: 'Student',
      participantName: 'طالب الاختبار',
      linkedStudentUserId: 'a0000000-0000-0000-0000-000000000034',
      currentOwnerUserId: staffId,
      channel: 'Web',
      createdAt: '2026-08-26T11:00:00.000Z',
      version: 1,
      canSend: true,
      canRate: false,
    };
    const history = [
      { conversationId: historyAId, status: 'Closed', subject: 'السجل أ', startedAt: '2026-08-20T10:00:00.000Z', endedAt: '2026-08-20T11:00:00.000Z', lastActivityAt: '2026-08-20T11:00:00.000Z', messageCount: 1, lastMessagePreview: 'رسالة أ', activities: [] },
      { conversationId: historyBId, status: 'Closed', subject: 'السجل ب', startedAt: '2026-08-21T10:00:00.000Z', endedAt: '2026-08-21T11:00:00.000Z', lastActivityAt: '2026-08-21T11:00:00.000Z', messageCount: 1, lastMessagePreview: 'رسالة ب', activities: [] },
    ];
    let releaseHistoryA = () => {};
    const historyAGate = new Promise<void>((resolve) => { releaseHistoryA = resolve; });
    let settleHistoryAResponse = () => {};
    const historyAResponseSettled = new Promise<void>((resolve) => { settleHistoryAResponse = resolve; });
    let historyARequests = 0;

    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { isEnabled: true, isCheckedIn: true, waitingCount: 0, activeCount: 1, maxActiveConversations: 2, conversations: [conversation], cannedReplies: [] } }),
    }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/messages**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/student-history`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: history }) }));
    await page.route(`**/api/live-support/staff/conversations/${conversationId}/student-history/*/messages**`, async route => {
      const isHistoryA = route.request().url().includes(historyAId);
      if (isHistoryA) {
        historyARequests += 1;
        await historyAGate;
      }
      const historyConversationId = isHistoryA ? historyAId : historyBId;
      const content = isHistoryA ? 'تفاصيل السجل أ المتأخرة' : 'تفاصيل السجل ب الصحيحة';
      await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [{ id: `${historyConversationId}-message`, conversationId: historyConversationId, senderType: 'Staff', clientMessageId: `${historyConversationId}-client`, type: 'Text', content, sentAt: '2026-08-21T10:30:00.000Z' }] }) }).catch(() => undefined);
      if (isHistoryA) settleHistoryAResponse();
    });

    await page.goto(`${staffUrl}/assistant/live-support`);
    await page.getByRole('button', { name: /السجل أ/ }).click();
    await expect.poll(() => historyARequests).toBe(1);
    await page.getByRole('button', { name: /السجل ب/ }).click();
    await expect(page.getByText('تفاصيل السجل ب الصحيحة', { exact: true })).toBeVisible();
    releaseHistoryA();
    await historyAResponseSettled;
    await expect(page.getByText('تفاصيل السجل ب الصحيحة', { exact: true })).toBeVisible();
    await expect(page.getByText('تفاصيل السجل أ المتأخرة', { exact: true })).toHaveCount(0);
  });
});
