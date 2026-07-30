import { expect, request as requestFactory, type APIRequestContext, type Page } from '@playwright/test';

export const apiUrl = process.env.E2E_API_URL || 'http://api.lvh.me:5245/api';
export const appUrl = process.env.LIVE_SUPPORT_E2E_URL || 'http://app.lvh.me:3000';
export const staffUrl = process.env.STAFF_E2E_URL || 'http://staff.lvh.me:3000';
export const adminUrl = process.env.ADMIN_E2E_URL || 'http://admin.lvh.me:3000';

export const accounts = {
  admin: { phoneNumber: '20000000000', password: 'password', surface: 'admin' },
  student: { phoneNumber: '20000000001', password: 'password', surface: 'student' },
  assistant: { phoneNumber: '20000000003', password: 'password', surface: 'assistant' },
};

export const e2eHeaders = {
  'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345',
};

export async function seedE2E(request: APIRequestContext, reason = 'E2E backend/seed is unavailable', clearDatabase = false) {
  let response;
  try {
    response = await request.post(`${apiUrl}/e2e/seed`, {
      headers: e2eHeaders,
      data: { clearDatabase, seedAdmin: true, seedStudents: true, seedAssistant: true, seedTeacher: true, seedLiveSupport: true },
    });
  } catch {
    throw new Error(reason);
  }
  expect(response.ok(), `${reason} (${response.status()})`).toBeTruthy();
}

export function requireRealAdminAbE2E() {
  expect(process.env.REAL_ADMIN_AB_E2E, 'REAL_ADMIN_AB_E2E=1 is required for the real two-session Admin A/B workflow').toBe('1');
}

export async function login(request: APIRequestContext, account: keyof typeof accounts) {
  const value = accounts[account];
  let response;
  try {
    response = await request.post(`${apiUrl}/auth/login`, {
      headers: { 'X-App-Surface': value.surface, 'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345' },
      data: {
        phoneNumber: value.phoneNumber,
        password: value.password,
        // A/B sessions must not share the backend's device/session identity.
        deviceFingerprint: `e2e-${account}-${Date.now()}-${Math.random().toString(36).slice(2)}`,
      },
    });
  } catch {
    throw new Error('E2E backend is unavailable');
  }
  expect(response.ok(), `E2E seed does not provide the documented ${account} account (${response.status()})`).toBeTruthy();
  const payload = await response.json().catch(() => null);
  const body = payload?.data ?? payload;
  expect(body?.accessToken, `${account} login must return an access token`).toBeTruthy();
  return body as { accessToken: string; user: { id: string; roles: string[]; permissions?: string[]; fullName?: string; phone?: string; profileComplete?: boolean } };
}

export function bearer(accessToken: string) {
  return { Authorization: `Bearer ${accessToken}` };
}

export async function installAuthAndGoto(page: Page, accessToken: string, user: unknown, url: string) {
  await page.addInitScript(({ accessToken: token, user: authUser }) => {
    window.localStorage.setItem('accessToken', token);
    window.localStorage.setItem('user', JSON.stringify(authUser));
  }, { accessToken, user });

  for (let attempt = 0; attempt < 2; attempt += 1) {
    try {
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      return;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (!message.includes('ERR_ABORTED') || attempt === 1) throw error;
    }
  }
}

export async function createGuest(request: APIRequestContext, suffix: string) {
  void request;
  let context = await requestFactory.newContext({
    baseURL: `${apiUrl}/`,
    extraHTTPHeaders: {
      'X-App-Surface': 'landing',
      'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345',
    },
  });
  const session = await context.post('live-support/guest/session', { data: { displayName: `E2E ${suffix}`, phoneNumber: `010${Date.now().toString().slice(-8)}` } });
  expect(session.ok(), `E2E guest session could not be created (${session.status()})`).toBeTruthy();
  const setCookie = session.headers()['set-cookie'];
  const guestCookie = setCookie?.match(/(?:^|,\s*)massar_support_guest=([^;]+)/)?.[1];
  if (guestCookie) {
    await context.dispose();
    context = await requestFactory.newContext({
      baseURL: `${apiUrl}/`,
      extraHTTPHeaders: {
        'X-App-Surface': 'landing',
        'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345',
        Cookie: `massar_support_guest=${guestCookie}`,
      },
    });
  }
  return context;
}
