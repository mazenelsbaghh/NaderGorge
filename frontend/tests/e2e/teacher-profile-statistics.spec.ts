import { expect, test, type Route } from '@playwright/test';

const teacherId = '2a0e7d2f-1dd7-4af0-9974-c999489899b2';
const stats = { studentsCount: 686, activeStudentsCount: 6700, packagesCount: 4, examsCount: 4,
  essaysPendingCount: 0, essaysGradedCount: 602, codeGroupsCount: 0, questionBankItemsCount: 0,
  totalEarnings: 100, currentBalance: 100, packageSales: [] };
const respond = (route: Route, data: unknown) => route.fulfill({ json: { success: true, data } });

for (const failFirst of [false, true]) {
  test(`teacher statistics ${failFirst ? 'recover from an error' : 'load independently of students'} without false zeros`, async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('accessToken', 'test-admin-token');
      localStorage.setItem('user', JSON.stringify({ id: 'test-admin', fullName: 'Admin', roles: ['Admin'],
        permissions: [], profileComplete: true, allowedDomains: ['admin'], allowedNavbarItems: [] }));
    });
    await page.route('**/api/**', route => respond(route, []));
    await page.route(`**/api/admin/teachers/${teacherId}`, route => respond(route, {
      id: teacherId, userId: 'teacher-user', fullName: 'Test teacher', subjectIds: [], subjects: [], commissionRate: 100,
    }));
    let releaseStudents!: () => void;
    const slowStudents = new Promise<void>(resolve => { releaseStudents = resolve; });
    await page.route(`**/api/admin/teachers/${teacherId}/students?*`, async route => {
      await slowStudents;
      await respond(route, { items: [], totalCount: 0 });
    });
    await page.route(`**/api/admin/teachers/${teacherId}/essays`, route => respond(route,
      Array.from({ length: 20 }, (_, id) => ({ id: String(id), status: 3 }))));
    let releaseStats!: () => void;
    const slowStats = new Promise<void>(resolve => { releaseStats = resolve; });
    let statsUnavailable = failFirst;
    await page.route(`**/api/admin/teachers/${teacherId}/stats`, async route => {
      await slowStats;
      if (statsUnavailable) await route.fulfill({ status: 503, json: { success: false } });
      else await respond(route, stats);
    });
    try {
      await page.goto(`/admin/teachers/${teacherId}`);
      await expect(page.getByText('جارٍ تحميل الإحصائيات...')).toBeVisible();
      const students = page.getByText('طلاب اقتنوا محتوى (تاريخي)', { exact: true }).locator('../..');
      const pending = page.getByText('مقالات قيد التصحيح', { exact: true }).locator('../..');
      await expect(students).toContainText('—');
      releaseStats();
      if (failFirst) {
        const error = page.getByRole('alert').filter({ hasText: 'تعذر تحميل الإحصائيات' });
        await expect(error).toBeVisible();
        await expect(students).toContainText('—');
        statsUnavailable = false;
        await error.getByRole('button', { name: 'إعادة المحاولة' }).click();
      }
      await expect(students).toContainText('686');
      await expect(pending).toContainText('0');
      await expect(pending).not.toContainText('20');
    } finally { releaseStats(); releaseStudents(); }
  });
}
