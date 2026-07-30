import { expect, test } from '@playwright/test';
import { adminUrl, apiUrl, appUrl, bearer, installAuthAndGoto, login, requireRealAdminAbE2E, seedE2E } from './e2e-contract-helpers';

test.describe('employee/session refresh contract', () => {
  test('current session exposes authorization data and HR lookup is readable by staff', async ({ request }) => {
    await seedE2E(request);
    const loginBody = await login(request, 'admin');

    const headers = bearer(loginBody.accessToken);
    const session = await request.get(`${apiUrl}/auth/session`, { headers });
    expect(session.ok()).toBeTruthy();
    const sessionBody = (await session.json()).data;
    expect(sessionBody.authorizationVersion).toEqual(sessionBody.user.authorizationVersion);
    expect(sessionBody.user.roles).toContain('Admin');
    expect(Array.isArray(sessionBody.user.permissions)).toBeTruthy();
    expect(Array.isArray(sessionBody.user.allowedNavbarItems)).toBeTruthy();

    const employees = await request.get(`${apiUrl}/admin/hr/employees`, { headers });
    expect(employees.ok()).toBeTruthy();
    expect(Array.isArray((await employees.json()).data)).toBeTruthy();
  });

  test('student credentials cannot read the staff HR lookup', async ({ request }) => {
    await seedE2E(request);
    const loginBody = await login(request, 'student');
    const response = await request.get(`${apiUrl}/admin/hr/employees`, {
      headers: bearer(loginBody.accessToken),
    });
    expect([401, 403]).toContain(response.status());
  });

  test('admin and assistant sessions keep authorization versions aligned after a fresh session read', async ({ request }) => {
    await seedE2E(request);
    const [adminAuth, assistantAuth] = await Promise.all([login(request, 'admin'), login(request, 'assistant')]);
    for (const auth of [adminAuth, assistantAuth]) {
      const response = await request.get(`${apiUrl}/auth/session`, { headers: bearer(auth.accessToken) });
      expect(response.ok()).toBeTruthy();
      const snapshot = (await response.json()).data;
      expect(snapshot.authorizationVersion).toBe(snapshot.user.authorizationVersion);
      expect(snapshot.user.roles.length).toBeGreaterThan(0);
    }
  });

  test('public guest bootstrap does not probe the platform refresh endpoint', async ({ page }) => {
    let refreshRequests = 0;
    page.on('request', (request) => {
      if (request.url().includes('/auth/refresh')) {
        refreshRequests += 1;
      }
    });

    await page.goto(appUrl, { waitUntil: 'networkidle' });

    expect(refreshRequests).toBe(0);
  });

  test('RF-R02 browser A/B session contract keeps both admin shells authenticated', async ({ browser, request }) => {
    requireRealAdminAbE2E();
    await seedE2E(request);
    const [adminA, adminB] = await Promise.all([login(request, 'admin'), login(request, 'admin')]);

    const contexts = await Promise.all([adminA, adminB].map(async (auth, index) => {
      const context = await browser.newContext();
      const page = await context.newPage();
      await installAuthAndGoto(page, auth.accessToken, auth.user, `${adminUrl}/admin/users`);
      await expect(page).not.toHaveURL(/\/login/);
      await expect(page).toHaveURL(/\/admin\//);
      await expect(page.locator('body')).toContainText(/تسجيل الخروج|Logout/i);
      await expect(page.locator('body')).not.toContainText(/غير مصرح|Unauthorized/i);
      return { context, page, index };
    }));

    await expect(contexts[0].page.locator('body')).toBeVisible();
    await expect(contexts[1].page.locator('body')).toBeVisible();
    await Promise.all(contexts.map(({ context }) => context.close()));
  });

  test('real Admin A/B employee update returns a conflict without mutating B draft state', async ({ request }) => {
    requireRealAdminAbE2E();
    await seedE2E(request);
    const [adminA, adminB] = await Promise.all([login(request, 'admin'), login(request, 'admin')]);
    const headersA = bearer(adminA.accessToken);
    const headersB = bearer(adminB.accessToken);

    const employeesResponse = await request.get(`${apiUrl}/admin/hr/employees`, { headers: headersA });
    expect(employeesResponse.ok()).toBeTruthy();
    const employees = (await employeesResponse.json()).data as Array<{
      userId: string;
      employeeProfile?: { updatedAt?: string | null } | null;
    }>;
    const employee = employees.find((item) => item.userId !== adminA.user.id);
    test.skip(!employee, 'E2E seed did not provide a second employee for the real A/B conflict contract.');

    const firstRead = await request.get(`${apiUrl}/admin/hr/employees`, { headers: headersB });
    expect(firstRead.ok()).toBeTruthy();
    const bEmployee = ((await firstRead.json()).data as typeof employees).find((item) => item.userId === employee!.userId);
    test.skip(!bEmployee, 'E2E seed did not provide the selected employee to Admin B.');

    let initialUpdatedAt = bEmployee!.employeeProfile?.updatedAt ?? null;
    if (!initialUpdatedAt) {
      const createProfile = await request.post(`${apiUrl}/admin/hr/employees`, {
        headers: headersA,
        data: { userId: employee!.userId, basicSalary: 4000, standardStartTime: '09:00:00', targetDailyHours: 8 },
      });
      expect(createProfile.ok()).toBeTruthy();
      expect((await createProfile.json()).success).toBeTruthy();

      const afterCreate = await request.get(`${apiUrl}/admin/hr/employees`, { headers: headersB });
      expect(afterCreate.ok()).toBeTruthy();
      const createdEmployee = ((await afterCreate.json()).data as typeof employees).find((item) => item.userId === employee!.userId);
      test.skip(!createdEmployee?.employeeProfile?.updatedAt, 'E2E backend did not return a version for the created employee profile.');
      initialUpdatedAt = createdEmployee!.employeeProfile!.updatedAt!;
    }

    const firstWrite = await request.post(`${apiUrl}/admin/hr/employees`, {
      headers: headersA,
      data: { userId: employee!.userId, basicSalary: 4100, standardStartTime: '09:00:00', targetDailyHours: 8, expectedUpdatedAt: initialUpdatedAt },
    });
    expect(firstWrite.ok()).toBeTruthy();
    const firstWriteBody = await firstWrite.json();
    expect(firstWriteBody.success).toBeTruthy();

    const staleWrite = await request.post(`${apiUrl}/admin/hr/employees`, {
      headers: headersB,
      data: { userId: employee!.userId, basicSalary: 4200, standardStartTime: '10:00:00', targetDailyHours: 7, expectedUpdatedAt: initialUpdatedAt },
    });
    expect(staleWrite.ok()).toBeTruthy();
    const staleBody = await staleWrite.json();
    expect(staleBody.success).toBeFalsy();
    expect(staleBody.errors).toContain('EMPLOYEE_PROFILE_CONFLICT');
    // The client owns the draft; the backend must only reject the stale write.
    expect(staleBody.data).toBeNull();
  });

  test('real Admin A permission revoke invalidates an already-issued assistant session', async ({ request }) => {
    requireRealAdminAbE2E();
    await seedE2E(request);
    const [adminAuth, assistantAuth] = await Promise.all([login(request, 'admin'), login(request, 'assistant')]);
    const assistantHeaders = bearer(assistantAuth.accessToken);
    const before = await request.get(`${apiUrl}/admin/hr/employees`, { headers: assistantHeaders });
    expect([401, 403]).toContain(before.status());

    const revoke = await request.put(`${apiUrl}/admin/users/${assistantAuth.user.id}/roles`, {
      headers: bearer(adminAuth.accessToken),
      data: { roles: ['Student'] },
    });
    expect(revoke.ok()).toBeTruthy();

    const after = await request.get(`${apiUrl}/auth/session`, { headers: assistantHeaders });
    expect([401, 403]).toContain(after.status());

    const restore = await request.put(`${apiUrl}/admin/users/${assistantAuth.user.id}/roles`, {
      headers: bearer(adminAuth.accessToken),
      data: { roles: ['Assistant'] },
    });
    expect(restore.ok()).toBeTruthy();
  });
});
