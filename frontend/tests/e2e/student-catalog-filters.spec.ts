import { expect, test } from '@playwright/test';
import type { PackageDto } from '../../src/services/content-service';
import { installAuthAndGoto } from './e2e-contract-helpers';

const student = {
  id: 'catalog-filter-student',
  fullName: 'طالب اختبار التصفية',
  roles: ['Student'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['student'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};
const packages: PackageDto[] = [
  {
    id: 'history-one',
    name: 'باقة التاريخ الأولى',
    teacherId: 'nader',
    teacherName: 'نادر جورج',
    subjectId: 'history',
    subjectName: 'تاريخ',
  },
  {
    id: 'psychology',
    name: 'باقة علم النفس',
    teacherId: 'yasmina',
    teacherName: 'ياسمينا البهنساوي',
    subjectId: 'psychology',
    subjectName: 'علم نفس',
  },
  {
    id: 'history-two',
    name: 'باقة التاريخ الثانية',
    teacherId: 'nader',
    teacherName: 'نادر جورج',
    subjectId: 'history',
    subjectName: 'تاريخ',
  },
].map((pkg) => ({
  ...pkg,
  description: '',
  price: 200,
  programId: 'secondary',
  isEnrolled: false,
  contentMode: 'TermWithSections',
}));

test('student catalog groups each teacher and filters content using dropdowns without mobile scrolling', async ({
  page,
  baseURL,
}, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.route('**/api/**', (route) => {
    const path = new URL(route.request().url()).pathname;
    let data: unknown = [];
    if (path.endsWith('/auth/session'))
      data = { user: student, authorizationVersion: 1 };
    if (path.endsWith('/content/packages')) data = packages;
    if (path.endsWith('/student/shell-bootstrap'))
      data = {
        unreadNotificationsCount: 0,
        currentBalance: 0,
        gamification: { totalPoints: 0, levelName: 'طالب' },
        hasSeenTrackingCodePopup: true,
      };
    if (path.endsWith('/public/settings'))
      return route.fulfill({
        json: { maintenanceMode: false, liveSupportEnabled: false },
      });
    return route.fulfill({ json: { success: true, data } });
  });
  await installAuthAndGoto(
    page,
    'catalog-test-token',
    student,
    `${baseURL}/student/packages`
  );
  const nader = page.getByRole('region', {
    name: 'محتوى نادر جورج',
    exact: true,
  });
  const yasmina = page.getByRole('region', {
    name: 'محتوى ياسمينا البهنساوي',
    exact: true,
  });
  await expect(
    nader.getByRole('heading', { name: 'باقة التاريخ الأولى', exact: true })
  ).toBeVisible();
  await expect(
    nader.getByRole('heading', { name: 'باقة التاريخ الثانية', exact: true })
  ).toBeVisible();
  await expect(
    yasmina.getByRole('heading', { name: 'باقة علم النفس', exact: true })
  ).toBeVisible();
  const filters = page.getByRole('region', {
    name: 'تصفية المحتوى',
    exact: true,
  });
  await filters
    .getByRole('combobox', { name: 'المدرس', exact: true })
    .selectOption('yasmina');
  await expect(nader).toHaveCount(0);
  await expect(yasmina).toBeVisible();
  await filters.getByRole('button', { name: 'مسح التصفية' }).click();
  await filters
    .getByRole('combobox', { name: 'المادة', exact: true })
    .selectOption('history');
  await expect(yasmina).toHaveCount(0);
  await expect(nader).toBeVisible();
  const packageFilters = page.getByRole('group', {
    name: 'تصفية الباقات',
    exact: true,
  });
  await packageFilters
    .getByRole('combobox', { name: 'المدرس', exact: true })
    .selectOption('yasmina');
  await expect(packageFilters.locator('..').getByRole('link')).toHaveCount(1);
  for (const controls of [filters, packageFilters]) {
    expect(
      await controls.evaluate(
        (element) => element.scrollWidth <= element.clientWidth
      )
    ).toBe(true);
  }
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth
    )
  ).toBe(true);
  await page.screenshot({
    path: testInfo.outputPath('student-catalog-mobile.png'),
    fullPage: true,
  });
});
