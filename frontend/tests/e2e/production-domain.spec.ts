import { expect, test } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';

const enabled = process.env.PRODUCTION_DOMAIN_E2E === '1';
const adminPhone = process.env.PRODUCTION_E2E_ADMIN_PHONE;
const adminPassword = process.env.PRODUCTION_E2E_ADMIN_PASSWORD;
const surfaces = [
  'https://massar-academy.net',
  'https://app.massar-academy.net',
  'https://admin.massar-academy.net',
  'https://teacher.massar-academy.net',
  'https://staff.massar-academy.net',
] as const;

async function adminLogin(request: APIRequestContext): Promise<{
  accessToken: string;
  setCookie: string;
}> {
  const response = await request.post('https://api.massar-academy.net/api/auth/login', {
    headers: { 'X-App-Surface': 'admin' },
    data: {
      phoneNumber: adminPhone,
      password: adminPassword,
      deviceFingerprint: `production-rehearsal-${Date.now()}`,
      deviceName: 'Production rehearsal',
    },
  });
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  expect(body.data.accessToken).toBeTruthy();
  return {
    accessToken: body.data.accessToken,
    setCookie: response.headers()['set-cookie'] ?? '',
  };
}

test.use({ trace: 'off', video: 'off' });

test.describe('production domain contract', () => {
  test.skip(!enabled, 'Set PRODUCTION_DOMAIN_E2E=1 only after a protected tunnel rehearsal.');

  for (const origin of surfaces) {
    test(`${origin} serves through cluster ingress`, async ({ request }) => {
      const response = await request.get(origin);
      expect(response.ok()).toBeTruthy();
      expect(response.headers()['x-massar-node']).toMatch(/^node-[123]$/);
      expect(response.headers()['x-massar-release']).toBeTruthy();
    });
  }

  test('API liveness, CORS, and identity are stable', async ({ request }) => {
    const response = await request.get('https://api.massar-academy.net/api/health/live', {
      headers: { Origin: 'https://app.massar-academy.net' },
    });
    expect(response.ok()).toBeTruthy();
    expect(response.headers()['access-control-allow-origin']).toBe('https://app.massar-academy.net');
    const body = await response.json();
    expect(body.status).toBe('healthy');
    expect(body.nodeId).toMatch(/^node-[123]$/);
    expect(body.releaseId).toBeTruthy();
  });

  test('protected assets cannot be fetched anonymously', async ({ request }) => {
    const response = await request.get('https://assets.massar-academy.net/protected/probe');
    expect([401, 403, 404]).toContain(response.status());
  });

  test('Admin login emits one secure parent-domain refresh cookie', async ({ request }) => {
    test.skip(!adminPhone || !adminPassword, 'Protected Admin rehearsal credentials are required.');
    const login = await adminLogin(request);
    expect(login.setCookie).toContain('ng_refresh=');
    expect(login.setCookie).toMatch(/;\s*secure/i);
    expect(login.setCookie).toMatch(/;\s*httponly/i);
    expect(login.setCookie).toMatch(/;\s*samesite=lax/i);
    expect(login.setCookie).toMatch(/;\s*domain=\.?massar-academy\.net/i);
  });

  test('authenticated WebSocket completes the SignalR handshake', async ({ page, request }) => {
    test.skip(!adminPhone || !adminPassword, 'Protected Admin rehearsal credentials are required.');
    const login = await adminLogin(request);
    const negotiate = await request.post(
      'https://ws.massar-academy.net/hubs/platform/negotiate?negotiateVersion=1',
      { headers: { Authorization: `Bearer ${login.accessToken}` } },
    );
    expect(negotiate.ok()).toBeTruthy();
    const negotiation = await negotiate.json();
    expect(negotiation.connectionToken).toBeTruthy();

    const handshake = await page.evaluate(
      ({ connectionToken, accessToken }) => new Promise<string>((resolve, reject) => {
        const socket = new WebSocket(
          `wss://ws.massar-academy.net/hubs/platform?id=${encodeURIComponent(connectionToken)}&access_token=${encodeURIComponent(accessToken)}`,
        );
        const timeout = window.setTimeout(() => {
          socket.close();
          reject(new Error('SignalR handshake timed out'));
        }, 10_000);
        socket.onopen = () => socket.send('{"protocol":"json","version":1}\u001e');
        socket.onmessage = (event) => {
          window.clearTimeout(timeout);
          const payload = String(event.data);
          socket.close();
          resolve(payload);
        };
        socket.onerror = () => {
          window.clearTimeout(timeout);
          reject(new Error('WebSocket connection failed'));
        };
      }),
      {
        connectionToken: negotiation.connectionToken,
        accessToken: login.accessToken,
      },
    );
    expect(handshake).toContain('{}');
  });

  test('authenticated multipart upload reaches API validation without publishing a file', async ({ request }) => {
    test.skip(!adminPhone || !adminPassword, 'Protected Admin rehearsal credentials are required.');
    const login = await adminLogin(request);
    const response = await request.post(
      'https://api.massar-academy.net/api/admin/questions/image',
      {
        headers: { Authorization: `Bearer ${login.accessToken}` },
        multipart: {
          image: {
            name: 'rehearsal-invalid.png',
            mimeType: 'image/png',
            buffer: Buffer.from('not-an-image'),
          },
        },
      },
    );
    expect(response.status()).toBe(400);
    expect(await response.text()).toContain('supported image');
  });
});
