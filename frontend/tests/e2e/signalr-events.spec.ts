import { expect, test } from '@playwright/test';

import { shouldRefreshStaffRoute } from '../../src/lib/staff-realtime-scopes';

test.describe('staff realtime route mapping', () => {
  test('refreshes matching staff pages without interrupting edit routes', () => {
    expect(shouldRefreshStaffRoute('/admin/hr', ['hr'])).toBe(true);
    expect(shouldRefreshStaffRoute('/assistant/tasks/123', ['operations'])).toBe(true);
    expect(shouldRefreshStaffRoute('/teacher/finance', ['finance'])).toBe(true);
    expect(shouldRefreshStaffRoute('/admin/forms/123/edit', ['forms'])).toBe(false);
    expect(shouldRefreshStaffRoute('/admin/hr', ['community'])).toBe(false);
  });
});

test.describe('SignalR reconnect flow', () => {
  test('student reconnects and rejoins the active lesson group', async ({ browser, request }) => {
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
    const setupResponse = await request.post('http://localhost:5245/api/e2e/setup-mock-package');
    expect(setupResponse.ok()).toBeTruthy();
    const course = await setupResponse.json() as { packageId: string; lessonId: string };
    const grantResponse = await request.post('http://localhost:5245/api/e2e/grant-package', {
      data: { packageId: course.packageId },
    });
    expect(grantResponse.ok()).toBeTruthy();

    const context = await browser.newContext();
    const page = await context.newPage();
    
    // Override global WebSocket to allow forcing abnormal closure in the test
    await page.addInitScript(() => {
      const OriginalWebSocket = window.WebSocket;
      (window as any).__activeWebSockets = [];
      window.WebSocket = function (url: string | URL, protocols?: string | string[]) {
        const socket = new OriginalWebSocket(url, protocols);
        (window as any).__activeWebSockets.push(socket);
        return socket;
      } as any;
      Object.assign(window.WebSocket, OriginalWebSocket);
    });

    const browserLogs: string[] = [];

    page.on('console', msg => {
      console.log('BROWSER LOG reconnect test:', msg.text());
      browserLogs.push(msg.text());
    });
    page.on('response', response => {
      if (response.status() >= 400) {
        console.log(`E2E TEST reconnect: Failed Request: ${response.request().method()} ${response.url()} -> ${response.status()}`);
      }
    });

    await page.goto('http://app.localhost:3000/login');
    await page.fill('input[name="phoneNumber"]', '20000000001');
    await page.fill('input[name="password"]', 'password');
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/\/student$/, { timeout: 15_000 });

    await page.goto(
      `http://app.localhost:3000/student/packages/${course.packageId}/lessons/${course.lessonId}`
    );
    await expect.poll(() => browserLogs.some(log => log.includes('Joined active lesson group on startup') && log.includes(course.lessonId)), { timeout: 15_000 }).toBe(true);

    // Forcefully close the WebSocket to trigger automatic reconnect
    await page.evaluate(() => {
      for (const ws of (window as any).__activeWebSockets) {
        ws.close(3001, "Abnormal closure");
      }
    });

    await expect.poll(() => browserLogs.some(log => log.includes('Re-joined lesson group on reconnect') && log.includes(course.lessonId)), { timeout: 20_000 }).toBe(true);
    await context.close();
  });
});

test.describe('SignalR listener registry safety', () => {
  test('unmounting a hook does not remove handlers of other active hooks', async ({ browser, request }) => {
    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
    const setupResponse = await request.post('http://localhost:5245/api/e2e/setup-mock-package');
    expect(setupResponse.ok()).toBeTruthy();
    const course = await setupResponse.json() as { packageId: string; termId: string; lessonId: string };
    const grantResponse = await request.post('http://localhost:5245/api/e2e/grant-package', {
      data: { packageId: course.packageId },
    });
    expect(grantResponse.ok()).toBeTruthy();

    const context = await browser.newContext();
    const page = await context.newPage();
    page.on('console', msg => console.log('BROWSER LOG safety test:', msg.text()));
    page.on('response', response => {
      if (response.status() >= 400) {
        console.log(`E2E TEST safety: Failed Request: ${response.request().method()} ${response.url()} -> ${response.status()}`);
      }
    });

    await page.goto('http://app.localhost:3000/login');
    await page.fill('input[name="phoneNumber"]', '20000000001');
    await page.fill('input[name="password"]', 'password');
    await page.click('button[type="submit"]', { force: true });
    await expect(page).toHaveURL(/\/student$/, { timeout: 15_000 });

    let activeHooks = await page.evaluate(() => (window as any).__platformEventsTesting.getActiveHooksCount());
    expect(activeHooks).toBe(1);

    let balanceChangedCount = await page.evaluate(() => (window as any).__platformEventsTesting.getListeners().BalanceChanged.size);
    expect(balanceChangedCount).toBeGreaterThan(0);

    await page.goto(`http://app.localhost:3000/student/packages/${course.packageId}/lessons/${course.lessonId}`);
    await page.waitForTimeout(2000);

    activeHooks = await page.evaluate(() => (window as any).__platformEventsTesting.getActiveHooksCount());
    expect(activeHooks).toBe(2);

    await page.goto('http://app.localhost:3000/student');
    await page.waitForTimeout(2000);

    activeHooks = await page.evaluate(() => (window as any).__platformEventsTesting.getActiveHooksCount());
    expect(activeHooks).toBe(1);

    balanceChangedCount = await page.evaluate(() => (window as any).__platformEventsTesting.getListeners().BalanceChanged.size);
    expect(balanceChangedCount).toBeGreaterThan(0);

    await context.close();
  });
});

