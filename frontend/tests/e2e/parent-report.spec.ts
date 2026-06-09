import { test, expect } from '@playwright/test';

test.describe('Parent Reporting Integration', () => {
  let mockStudentId = '00000000-0000-0000-0000-000000000001'; // Default valid E2e student Guid or similar
  let signedReportToken = '';

  test.beforeEach(async ({ request }) => {
    // We assume the DB has the generic students
    await request.post('http://localhost:5245/api/e2e/seed', {
      data: {
        clearDatabase: false,
        seedAdmin: true,
        seedStudents: true,
        seedAssistant: false,
      },
    });

    // We fetch a student ID via API directly just to make sure we query a valid one
    const loginRes = await request.post(
      'http://localhost:5245/api/auth/login',
      {
        headers: { 'X-App-Surface': 'student' },
        data: {
          phoneNumber: '20000000001',
          password: 'password',
          deviceFingerprint: 'e2e-dev11',
        },
      }
    );
    if (loginRes.ok()) {
      const tokenData = await loginRes.json();
      const user = tokenData?.data?.user || tokenData?.user;
      if (user?.id) {
        mockStudentId = user.id;
      }
    }

    // fetch the mock package so the student has some data
    const setupResponse = await request.post(
      'http://localhost:5245/api/e2e/setup-mock-package'
    );
    if (setupResponse.ok()) {
      const mockPackageData = await setupResponse.json();
      // Grant package to Student 1
      await request.post('http://localhost:5245/api/e2e/grant-package', {
        data: { packageId: mockPackageData.packageId },
      });
    }

    // Authenticate as Admin to generate parent link token
    const adminLoginRes = await request.post(
      'http://localhost:5245/api/auth/login',
      {
        headers: { 'X-App-Surface': 'admin' },
        data: {
          phoneNumber: '20000000000',
          password: 'password',
          deviceFingerprint: 'e2e-admin11',
        },
      }
    );

    if (adminLoginRes.ok()) {
      const adminTokenData = await adminLoginRes.json();
      const adminToken = adminTokenData?.data?.accessToken || adminTokenData?.accessToken;

      if (adminToken) {
        // Request parent link token
        const linkRes = await request.post(
          `http://localhost:5245/api/parent/reports/${mockStudentId}/links`,
          {
            headers: { Authorization: `Bearer ${adminToken}` },
          }
        );
        if (linkRes.ok()) {
          const linkData = await linkRes.json();
          signedReportToken = linkData?.data?.token || linkData?.token || '';
        }
      }
    }
  });

  test('T011: Parent views report for a valid student ID', async ({ page }) => {
    // Navigate without authentication but with signed token
    await page.goto(`http://app.localhost:3000/parent-report/${mockStudentId}?token=${signedReportToken}`);

    // Wait for the report headers
    await expect(page.locator('text=تقرير التقدم الأكاديمي').first()).toBeVisible({
      timeout: 15000,
    });

    // Verify specific metrics layout blocks are visible
    await expect(page.locator('text=مستوى الالتزام').first()).toBeVisible();
    await expect(page.locator('text=الدروس المكتملة').first()).toBeVisible();
    await expect(page.locator('text=التنبيهات والإشعارات الأخيرة').first()).toBeVisible();
  });

  test('T012: Parent views report for an invalid ID', async ({ page }) => {
    // Navigate with a fake GUID and token
    await page.goto('http://app.localhost:3000/parent-report/ffffffff-ffff-ffff-ffff-ffffffffffff?token=invalid.token');

    // Should see failure/not found notice (تنبيه)
    await expect(page.locator('h2:has-text("تنبيه")')).toBeVisible({
      timeout: 10000,
    });
  });
});
