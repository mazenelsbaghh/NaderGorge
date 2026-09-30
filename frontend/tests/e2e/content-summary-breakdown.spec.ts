import { expect, test } from '@playwright/test';
import { adminUrl, installAuthAndGoto } from './e2e-contract-helpers';

const teacherId = '10000000-0000-4000-8000-000000000001';
const packageId = '20000000-0000-4000-8000-000000000001';
const termId = '30000000-0000-4000-8000-000000000001';
const sectionId = '40000000-0000-4000-8000-000000000001';
const lessonId = '50000000-0000-4000-8000-000000000001';

test('content statistics expand from course to term, month, and lesson with direct counts', async ({ page }) => {
  const admin = {
    id: '00000000-0000-4000-8000-000000000001',
    fullName: 'مدير الاختبار',
    phone: '20000000000',
    roles: ['Admin'],
    permissions: ['content.view', 'users.manage'],
    profileComplete: true,
    allowedDomains: ['admin'],
    allowedNavbarItems: [],
    authorizationVersion: 1,
  };
  const teacher = {
    id: teacherId,
    fullName: 'نادر',
    subjectIds: [],
    subjectNames: ['رياضيات'],
    packagesCount: 1,
  };
  const counts = (purchased: number, gifts: number, refundedStudents: number) =>
    ({ purchased, gifts, refundedStudents });
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    let data: unknown = [];
    if (pathname.endsWith('/auth/session')) data = { user: admin, authorizationVersion: 1 };
    else if (pathname.endsWith('/content/packages')) data = [];
    else if (pathname.endsWith('/admin/subjects')) data = [];
    else if (pathname.endsWith('/admin/teachers')) data = [teacher];
    else if (pathname.endsWith('/admin/content/summary/teachers')) data = [teacher];
    else if (pathname.endsWith('/admin/content/summary')) data = {
      fromUtc: null,
      toUtc: null,
      packageCombinations: [],
      packages: [{
        packageId,
        packageName: 'كورس نادر',
        teacherName: 'نادر',
        package: counts(2, 0, 0),
        term: counts(1, 0, 0),
        section: counts(0, 1, 0),
        lesson: counts(0, 0, 1),
        purchasedStudents: 3,
        giftStudents: 1,
        totalStudents: 4,
        activeStudents: 4,
        refundOperations: 1,
        breakdown: [{
          id: termId, title: 'الترم الثاني', kind: 'term', counts: counts(1, 0, 0),
          children: [{
            id: sectionId, title: 'شهر أكتوبر', kind: 'section', counts: counts(0, 1, 0),
            children: [{ id: lessonId, title: 'الحصة الأولى', kind: 'lesson', counts: counts(0, 0, 1), children: [] }],
          }],
        }],
      }],
    };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data }) });
  });

  await installAuthAndGoto(page, 'content-summary-test-token', admin, `${adminUrl}/admin/content?teacher=${teacherId}`);
  await expect(page.getByRole('heading', { name: 'كورس نادر' })).toBeVisible();
  await expect(page.getByText('الترم الثاني')).toBeHidden();
  await page.getByText('افتح تفاصيل الترمات والأقسام والشهور والحصص').click();
  await expect(page.getByText('الترم الثاني')).toBeVisible();
  await expect(page.getByText('شهر أكتوبر')).toBeHidden();
  await page.getByText('الترم الثاني').click();
  await expect(page.getByText('شهر أكتوبر')).toBeVisible();
  await page.getByText('شهر أكتوبر').click();
  const lesson = page.getByText('الحصة الأولى').locator('..');
  await expect(lesson).toContainText('1 طالب استرد');
});
