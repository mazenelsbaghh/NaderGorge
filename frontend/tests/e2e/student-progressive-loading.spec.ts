import { expect, test } from '@playwright/test';
import type { MyLessonDto } from '../../src/services/student-service';
import { installAuthAndGoto } from './e2e-contract-helpers';

const student = {
  id: 'd723ce13-4343-4791-8345-238502173c40', fullName: 'طالب اختبار الأداء',
  roles: ['Student'], permissions: [], profileComplete: true,
  allowedDomains: ['student'], allowedNavbarItems: [], authorizationVersion: 1,
};
const lessons: MyLessonDto[] = [{
  id: 'lesson', title: 'حصة اختبار الأداء', order: 1, packageId: 'course',
  packageName: 'كورس اختبار الأداء', termTitle: 'الترم الأول', sectionTitle: 'أكتوبر',
  teacherName: 'مدرس الاختبار', isCompleted: false, videoCount: 3,
  watchedVideoCount: 1, watchProgressPercent: 20,
}];

test.beforeEach(async ({ page }) => {
  await page.addInitScript(id => localStorage.setItem(`onboarding_ack_${id}`, '1'), student.id);
  await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
  await page.route('**/api/auth/session', route => route.fulfill({
    json: { success: true, data: { user: student, authorizationVersion: 1 } },
  }));
  await page.route('**/api/public/settings', route => route.fulfill({ json: { maintenanceMode: false } }));
  await page.route('**/api/student/shell-bootstrap', route => route.fulfill({
    json: { success: true, data: { unreadNotificationsCount: 0, currentBalance: 0,
      gamification: { totalPoints: 0, levelName: 'طالب' }, hasSeenTrackingCodePopup: true } },
  }));
  await page.route('**/api/student/welcome/claim', route => route.fulfill({ json: { success: true, data: null } }));
  await page.route('**/api/student/lessons', route => route.fulfill({ json: { success: true, data: lessons } }));
});

// Incident 2026-10-27: secondary reads prevented ready course content from appearing.
test('ready courses remain usable while dashboard and quick access are still pending', async ({ page, baseURL }) => {
  let finishReads!: () => void;
  const pendingReads = new Promise<void>(resolve => { finishReads = resolve; });
  for (const endpoint of ['dashboard', 'dashboard/quick-access']) {
    await page.route(`**/api/student/${endpoint}`, async route => {
      await pendingReads;
      await route.fulfill({ status: 503, json: { success: false, message: 'Synthetic unavailable secondary read' } });
    });
  }
  try {
    await installAuthAndGoto(page, 'performance-test-token', student, `${baseURL}/student`);
    const course = page.getByRole('heading', { name: lessons[0].packageName, exact: true });
    await expect(course).toBeVisible();
    await expect(page.getByRole('link', { name: 'افتح الكورس', exact: true })).toHaveAttribute('href', '/student/packages/course');
    finishReads();
    await expect(page.getByRole('alert').filter({ hasText: 'تعذر تحميل لوحة الطالب' })).toBeVisible();
    await expect(course).toBeVisible();
  } finally {
    finishReads();
  }
});

test('packages appear without quick access and its tab loads only when selected', async ({ page, baseURL }) => {
  await page.route('**/api/content/packages', route => route.fulfill({ json: { success: true, data: [{
    id: 'course', name: lessons[0].packageName, description: 'كورس الاختبار', price: 100,
    programId: 'program', isEnrolled: true, hasDirectPackageAccess: true,
  }] } }));
  let finishRead!: () => void;
  const pendingRead = new Promise<void>(resolve => { finishRead = resolve; });
  let quickAccessReads = 0;
  await page.route('**/api/student/dashboard/quick-access', async route => {
    quickAccessReads++;
    await pendingRead;
    await route.fulfill({ json: { success: true, data: [{ title: 'فيديو اختبار الوصول',
      pathBreadcrumb: 'الكورس > الحصة', url: '/student/lessons/lesson?videoId=video', accessType: 'Video' }] } });
  });
  try {
    await installAuthAndGoto(page, 'performance-test-token', student, `${baseURL}/student/packages`);
    await expect(page.getByRole('heading', { name: 'الباقات والمسارات', exact: true })).toBeVisible();
    expect(quickAccessReads).toBe(0);
    await page.getByRole('button', { name: /فيديوهاتي/ }).click();
    await expect(page.getByRole('status', { name: 'جارٍ تحميل فيديوهاتي' })).toBeVisible();
    await expect(page.getByText('لا توجد فيديوهاتي بعد', { exact: true })).toHaveCount(0);
    finishRead();
    await expect(page.getByRole('heading', { name: 'فيديو اختبار الوصول', exact: true })).toBeVisible();
    expect(quickAccessReads).toBe(1);
  } finally {
    finishRead();
  }
});
