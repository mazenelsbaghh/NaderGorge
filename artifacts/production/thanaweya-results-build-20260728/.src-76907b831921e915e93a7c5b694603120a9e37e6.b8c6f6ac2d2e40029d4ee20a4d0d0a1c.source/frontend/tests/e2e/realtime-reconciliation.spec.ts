import { expect, test } from '@playwright/test';
import { apiUrl, bearer, login, seedE2E } from './e2e-contract-helpers';

test.describe('realtime reconciliation API contract', () => {
  test('session snapshot is safe to reconcile after reconnect', async ({ request }) => {
    await seedE2E(request);
    const auth = await login(request, 'admin');

    const headers = bearer(auth.accessToken);
    const first = await request.get(`${apiUrl}/auth/session`, { headers });
    const second = await request.get(`${apiUrl}/auth/session`, { headers });
    expect(first.ok()).toBeTruthy();
    expect(second.ok()).toBeTruthy();

    const firstData = (await first.json()).data;
    const secondData = (await second.json()).data;
    expect(secondData.authorizationVersion).toBeGreaterThanOrEqual(firstData.authorizationVersion);
    expect(secondData.user.id).toBe(firstData.user.id);
    expect(secondData.serverTime).toBeTruthy();
  });

  test('invalid bearer token does not produce a reconciled session', async ({ request }) => {
    await seedE2E(request);
    const response = await request.get(`${apiUrl}/auth/session`, {
      headers: { Authorization: 'Bearer invalid-realtime-token' },
    });
    expect([401, 403]).toContain(response.status());
  });

  test('permission and validation failures remain observable mutation outcomes', async ({ request }) => {
    await seedE2E(request);
    const denied = await request.post(`${apiUrl}/admin/hr/employees`, {
      headers: { Authorization: 'Bearer invalid-realtime-token' },
      data: {},
    });
    expect([401, 403, 422]).toContain(denied.status());

    const auth = await login(request, 'admin');
    const invalidMutation = await request.post(`${apiUrl}/admin/hr/employees`, {
      headers: { Authorization: `Bearer ${auth.accessToken}` },
      data: {},
    });
    expect([400, 422]).toContain(invalidMutation.status());
  });

  test('stale authorization bearer is rejected instead of being reconciled into a session', async ({ request }) => {
    await seedE2E(request);
    const auth = await login(request, 'admin');
    const response = await request.get(`${apiUrl}/auth/session`, { headers: bearer(`${auth.accessToken}.stale`) });
    expect([401, 403]).toContain(response.status());
  });
});
