import { expect, test, type Page, type Route } from '@playwright/test';

const emptyListResponse = { success: true, data: [] };

const shellBootstrapResponse = {
  success: true,
  data: {
    unreadNotificationsCount: 0,
    currentBalance: 0,
    gamification: {
      totalPoints: 0,
      currentStreakCount: 0,
      longestStreakCount: 0,
      levelName: 'طالب',
    },
    themePreferences: {
      lightPaletteId: 'default',
      darkPaletteId: 'default',
      currentMode: 'light',
      avatarSlug: null,
    },
    avatarSlug: null,
    parentTrackingCode: '',
    hasSeenTrackingCodePopup: true,
  },
};

const studentUser = {
  id: 'student-academic-scope-smoke',
  fullName: 'طالب اختبار النطاق',
  phone: '20000000001',
  roles: ['Student'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['student'],
  allowedNavbarItems: [],
};

async function json(route: Route, body: unknown) {
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

async function installStudentSession(page: Page) {
  await page.addInitScript((user) => {
    window.localStorage.setItem('user', JSON.stringify(user));
    window.localStorage.setItem('accessToken', 'student-academic-scope-smoke-token');
  }, studentUser);
}

async function mockStudentScopeApis(page: Page) {
  await page.route('**/api/public/settings', (route) =>
    json(route, { maintenanceMode: false, maintenanceMessage: '' })
  );
  await page.route('**/api/student/shell-bootstrap', (route) => json(route, shellBootstrapResponse));
  await page.route('**/api/student/dashboard/quick-access', (route) => json(route, emptyListResponse));
  await page.route('**/api/content/packages', (route) => json(route, emptyListResponse));
  await page.route('**/api/public/teachers', (route) => json(route, emptyListResponse));
  await page.route('**/api/community/posts', (route) => json(route, emptyListResponse));
  await page.route('**/api/community/posts/mine', (route) => json(route, emptyListResponse));
  await page.route('**/api/public-exams', (route) => json(route, emptyListResponse));
  await page.route('**/api/student/shared-packages', (route) => json(route, emptyListResponse));
  await page.route('**/api/student/notifications', (route) => json(route, emptyListResponse));
}

test.describe('Student academic scope empty states', () => {
  test.beforeEach(async ({ page }) => {
    await installStudentSession(page);
    await mockStudentScopeApis(page);
  });

  test('student catalog pages explain that empty results are academic-scope filtered', async ({ page }) => {
    await page.goto('/student/packages');
    await expect(page.getByText('لا توجد باقات مفعّلة ومتاحة لصفك حالياً')).toBeVisible();
    await expect(page.getByText('الباقات غير المطابقة لمرحلتك أو صفك لا تظهر في هذه القائمة.')).toBeVisible();

    await page.goto('/student/teachers');
    await expect(page.getByText('لا يوجد معلمون متاحون لمرحلتك أو موادك حالياً.')).toBeVisible();

    await page.goto('/student/community');
    await expect(page.getByText('لا توجد منشورات متاحة لك حالياً.')).toBeVisible();
    await expect(page.getByText('تظهر هنا المنشورات المعتمدة المطابقة لمرحلتك وصفك أو المنشورات العامة للمنصة.')).toBeVisible();

    await page.goto('/student/public-exams');
    await expect(page.getByText('لا توجد امتحانات عامة متاحة لبياناتك الدراسية حالياً.')).toBeVisible();
    await expect(page.getByText('الامتحانات غير المطابقة لمرحلتك أو صفك أو موادك لا تظهر هنا.')).toBeVisible();

    await page.goto('/student/shared-packages');
    await expect(page.getByText('لا توجد باكدجات عامة متاحة لمرحلتك أو صفك حالياً.')).toBeVisible();
    await expect(page.getByText('الباكدجات غير المطابقة لبياناتك الدراسية لا تظهر في هذه الصفحة.')).toBeVisible();

    await page.goto('/student/notifications');
    await expect(page.getByText('صندوق إشعاراتك فارغ')).toBeVisible();
    await expect(page.getByText('الإشعارات العامة أو المطابقة لبياناتك الدراسية ستظهر هنا عند وصولها.')).toBeVisible();
  });
});
