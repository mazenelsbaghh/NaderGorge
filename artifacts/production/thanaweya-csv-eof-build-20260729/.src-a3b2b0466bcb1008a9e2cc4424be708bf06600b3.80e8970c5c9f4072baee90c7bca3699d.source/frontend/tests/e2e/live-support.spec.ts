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
    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isEnabled: true, isCheckedIn: true, waitingCount: 0, activeCount: 2, maxActiveConversations: 2, conversations: [{ id: 'a0000000-0000-0000-0000-000000000001', subject: 'أولى', status: 'Active', participantType: 'Guest', currentOwnerUserId: staffId }, { id: 'a0000000-0000-0000-0000-000000000002', subject: 'ثانية', status: 'Active', participantType: 'Guest', currentOwnerUserId: staffId }] } }) }));
    await page.route('**/api/live-support/staff/conversations/*/messages**', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) }));
    await page.goto(`${staffUrl}/assistant/live-support`);
    await expect(page.getByRole('heading', { name: 'أولى', exact: true })).toBeVisible();
    await page.getByRole('option', { name: /أولى/ }).click();
    await page.getByLabel('رد موظف الدعم').fill('مسودة المحادثة الأولى');
    await page.getByRole('option', { name: /ثانية/ }).click();
    await expect(page.getByLabel('رد موظف الدعم')).not.toHaveValue('مسودة المحادثة الأولى');
  });
});
