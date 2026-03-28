import { test, expect } from '@playwright/test';

test.describe('Parent Reporting Integration', () => {
  let mockStudentId = '00000000-0000-0000-0000-000000000001'; // Default valid E2e student Guid or similar

  test.beforeEach(async ({ request, page }) => {
    // We assume the DB has the generic students
    const seedRes = await request.post('http://localhost:5245/api/e2e/seed', {
      data: {
        clearDatabase: false,
        seedAdmin: false,
        seedStudents: true,
        seedAssistant: false,
      },
    });

    // We fetch a student ID via API directly just to make sure we query a valid one
    const loginRes = await request.post(
      'http://localhost:5245/api/v1/auth/login',
      {
        data: {
          phoneNumber: '20000000001',
          password: 'password',
          deviceId: 'e2e-dev11',
        },
      }
    );
    if (loginRes.ok()) {
      const tokenData = await loginRes.json();
      if (tokenData?.data?.user?.id) {
        mockStudentId = tokenData.data.user.id;
      } else if (tokenData?.user?.id) {
        mockStudentId = tokenData.user.id;
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
  });

  test('T011: Parent views report for a valid student ID', async ({ page }) => {
    // Navigate without authentication
    await page.goto(`/parent-report/${mockStudentId}`);

    // Wait for the report headers
    await expect(page.locator('text=Academic Progress Report')).toBeVisible({
      timeout: 15000,
    });

    // Verify specific metrics layout blocks are visible
    await expect(page.locator('text=Commitment Status')).toBeVisible();
    await expect(page.locator('text=Lessons Completed')).toBeVisible();
    await expect(page.locator('text=Recent Notices & Alerts')).toBeVisible();
  });

  test('T012: Parent views report for an invalid ID', async ({ page }) => {
    // Navigate with a fake GUID
    await page.goto('/parent-report/ffffffff-ffff-ffff-ffff-ffffffffffff');

    // Should see failure/not found notice
    await expect(page.locator('h2:has-text("Notice")')).toBeVisible({
      timeout: 10000,
    });
  });
});
