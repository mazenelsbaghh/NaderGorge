import test from 'node:test';
import { chromium, expect } from '@playwright/test';

const origin = process.env.WATCH_REQUEST_ALERTS_TEST_ORIGIN ?? 'http://staff.lvh.me:8771';
const recordSeparator = '\u001e';

test('a new watch request alerts staff outside the queue and updates the pending count', { timeout: 90_000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    const user = {
      id: 'watch-alert-support', fullName: 'دعم تجريبي', roles: ['CustomSupportRole'],
      permissions: ['watch_requests.manage'], allowedDomains: ['assistant'], allowedNavbarItems: [],
      profileComplete: true, authorizationVersion: 1,
    };
    await page.addInitScript(account => {
      localStorage.setItem('accessToken', 'synthetic-watch-alert-token');
      localStorage.setItem('user', JSON.stringify(account));
    }, user);

    let pendingCount = 2;
    await page.route('**/api/**', async route => {
      const { pathname } = new URL(route.request().url());
      if (pathname.endsWith('/auth/session')) {
        return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
      }
      if (pathname.endsWith('/watch-requests/pending-count')) {
        return route.fulfill({ json: { success: true, data: pendingCount } });
      }
      return route.fulfill({ json: { success: true, data: [] } });
    });

    const { promise: connected, resolve: acceptConnection } = Promise.withResolvers();
    await page.routeWebSocket('**/hubs/platform?*', socket => {
      socket.onMessage(message => {
        if (typeof message === 'string' && message.includes('"protocol":"json"')) {
          socket.send(`{}${recordSeparator}`);
          acceptConnection(socket);
        }
      });
    });

    await page.goto(`${origin}/assistant/dashboard`);
    const pendingLink = count => page.getByRole('link', {
      name: `طلبات المشاهدة الإضافية، عدد الطلبات المعلّقة ${count}`,
    });
    await expect(pendingLink('٢')).toBeVisible();
    const socket = await connected;
    const send = (target, payload) => socket.send(JSON.stringify({
      type: 1, target, arguments: [JSON.stringify(payload)],
    }) + recordSeparator);

    pendingCount = 3;
    send('ExtraWatchRequestCreated', { requestId: 'request-3' });
    await expect(pendingLink('٣')).toBeVisible();
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(pendingLink('٣')).toBeVisible();
    await page.getByRole('link', { name: 'طلب مشاهدة إضافية جديد راجع الطلبات' }).click();
    await expect(page).toHaveURL(/\/assistant\/watch-requests$/);

    pendingCount = 2;
    send('ExtraWatchRequestUpdated', { requestId: 'request-3', status: 'Approved', allowedWatchCount: 1 });
    await expect(pendingLink('٢')).toBeVisible();
  } finally {
    await browser.close();
  }
});
