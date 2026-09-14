import { expect, test, type Page, type Route } from '@playwright/test';

import {
  adminUrl,
  installAuthAndGoto,
} from './e2e-contract-helpers';

const adminUser = {
  id: 'admin-student-search-contract',
  fullName: 'Admin Search Contract',
  phone: '20000000888',
  roles: ['Assistant'],
  permissions: ['users.manage'],
  profileComplete: true,
  allowedDomains: ['admin'],
  allowedNavbarItems: ['/admin/students'],
  authorizationVersion: 1,
};

function student(id: string, fullName: string) {
  return {
    id,
    phoneNumber: `010${id.padStart(8, '0').slice(-8)}`,
    status: 'Active',
    fullName,
    grade: 'FirstSecondary',
    track: 'Science',
    createdAt: '2026-07-29T12:00:00Z',
    roles: ['Student'],
    studentCode: `student-${id}`,
    parentTrackingCode: `parent-${id}`,
    gender: 'Male',
    educationStage: 'Secondary',
    isFatherAlive: true,
    isMotherAlive: true,
    governorate: 'Cairo',
    address: '',
    currentBalance: 0,
  };
}

async function fulfillUsers(
  route: Route,
  items: ReturnType<typeof student>[],
  page: number,
  pageSize: number,
  totalCount = items.length
) {
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      success: true,
      data: { items, totalCount, page, pageSize },
    }),
  });
}

async function installAdminStudentApi(page: Page) {
  const requests: Array<{
    page: number;
    pageSize: number;
    role: string | null;
    search: string;
  }> = [];
  let releaseOldSearch!: () => void;
  let releaseFinalSearch!: () => void;
  const oldSearchGate = new Promise<void>((resolve) => {
    releaseOldSearch = resolve;
  });
  const finalSearchGate = new Promise<void>((resolve) => {
    releaseFinalSearch = resolve;
  });

  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());

    if (url.pathname.endsWith('/api/auth/session')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            user: adminUser,
            authorizationVersion: adminUser.authorizationVersion,
          },
        }),
      });
      return;
    }

    if (url.pathname.endsWith('/api/admin/users')) {
      const requestedPage = Number(url.searchParams.get('page') ?? '1');
      const requestedPageSize = Number(
        url.searchParams.get('pageSize') ?? '0'
      );
      const search = url.searchParams.get('search') ?? '';
      requests.push({
        page: requestedPage,
        pageSize: requestedPageSize,
        role: url.searchParams.get('role'),
        search,
      });

      if (search === 'بحث قديم') {
        await oldSearchGate;
        await fulfillUsers(
          route,
          [student('901', 'نتيجة قديمة يجب تجاهلها')],
          1,
          requestedPageSize
        ).catch(() => undefined);
        return;
      }

      if (search === 'بحث نهائي') {
        await finalSearchGate;
        await fulfillUsers(
          route,
          [student('902', 'النتيجة النهائية')],
          1,
          requestedPageSize
        );
        return;
      }

      const pageItems = Array.from({ length: requestedPageSize }, (_, index) =>
        student(
          String((requestedPage - 1) * requestedPageSize + index + 1),
          requestedPage === 2
            ? `طالب الصفحة الثانية ${index + 1}`
            : `طالب الصفحة الأولى ${index + 1}`
        )
      );
      await fulfillUsers(
        route,
        pageItems,
        requestedPage,
        requestedPageSize,
        75
      );
      return;
    }

    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Not needed by this contract.' }),
    });
  });

  return {
    requests,
    releaseOldSearch,
    releaseFinalSearch,
  };
}

test.describe('admin student search performance contract', () => {
  test('rapid search cancels stale work and retains the previous server page', async ({
    page,
  }) => {
    const api = await installAdminStudentApi(page);
    await installAuthAndGoto(
      page,
      'admin-student-search-token',
      adminUser,
      `${adminUrl}/admin/students`
    );

    await expect(
      page.getByText('طالب الصفحة الأولى 1', { exact: true })
    ).toBeVisible();
    await page.getByRole('button', { name: 'الصفحة التالية' }).click();
    await expect(
      page.getByText('طالب الصفحة الثانية 1', { exact: true })
    ).toBeVisible();

    const search = page.getByPlaceholder(
      'البحث برقم متابعة ولي الأمر، الاسم، أو رقم الهاتف...'
    );
    await search.fill('بحث قديم');
    await expect
      .poll(() => api.requests.some((request) => request.search === 'بحث قديم'))
      .toBe(true);

    await search.fill('بحث نهائي');
    await expect
      .poll(() =>
        api.requests.some((request) => request.search === 'بحث نهائي')
      )
      .toBe(true);

    await expect(
      page.getByText('طالب الصفحة الثانية 1', { exact: true })
    ).toBeVisible();
    await expect(
      page.locator('.content-visibility-auto[aria-busy="true"]')
    ).toHaveCount(1);

    api.releaseFinalSearch();
    await expect(page.getByText('النتيجة النهائية')).toBeVisible();
    api.releaseOldSearch();
    await expect(page.getByText('نتيجة قديمة يجب تجاهلها')).toHaveCount(0);
    await expect(page.getByText('النتيجة النهائية')).toBeVisible();

    const userRequests = api.requests.filter(
      (request) => request.search !== ''
    );
    expect(userRequests).toHaveLength(2);
    expect(userRequests.every((request) => request.page === 1)).toBe(true);
    expect(userRequests.every((request) => request.pageSize === 25)).toBe(true);
    expect(userRequests.every((request) => request.role === 'Student')).toBe(
      true
    );
  });
});
