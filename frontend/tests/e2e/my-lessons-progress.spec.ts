import { expect, test } from '@playwright/test';
import type { MyLessonDto } from '../../src/services/student-service';
import { installAuthAndGoto } from './e2e-contract-helpers';

const student = {
  id: 'lessons-progress-student',
  fullName: 'طالب اختبار الدروس',
  roles: ['Student'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['student'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};

// Completed-part counts and duration-weighted percentages are different measures.
const lessons: MyLessonDto[] = [
  { id: 'completed', title: 'المحاضرة الأولى | الحضارة والتاريخ', order: 1,
    packageId: 'history', packageName: 'باقة الشهر الأول', termTitle: 'الترم الأول',
    sectionTitle: 'شهر أكتوبر', teacherName: 'نادر جورج', isCompleted: true,
    videoCount: 3, watchedVideoCount: 3, watchProgressPercent: 100 },
  { id: 'partial', title: 'المحاضرة الثانية | مصادر دراسة الحضارات', order: 2,
    packageId: 'history', packageName: 'باقة الشهر الأول', termTitle: 'الترم الأول',
    sectionTitle: 'شهر أكتوبر', teacherName: 'نادر جورج', isCompleted: false,
    videoCount: 4, watchedVideoCount: 2, watchProgressPercent: 23 },
  { id: 'unknown', title: 'المحاضرة الثالثة | عوامل قيام الحضارات', order: 3,
    packageId: 'history', packageName: 'باقة الشهر الأول', termTitle: 'الترم الأول',
    sectionTitle: 'شهر أكتوبر', teacherName: 'نادر جورج', isCompleted: false,
    videoCount: 3, watchedVideoCount: 0, watchProgressPercent: null },
  { id: 'unwatched', title: 'التفاعلات الكيميائية', order: 1,
    packageId: 'chemistry', packageName: 'باقة الكيمياء', termTitle: 'الترم الأول',
    sectionTitle: 'شهر أكتوبر', teacherName: 'مدرس الكيمياء', isCompleted: false,
    videoCount: 2, watchedVideoCount: 0, watchProgressPercent: 0 },
];

test.describe('My Lessons watch progress (synthetic HTTP)', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
    await page.route('**/api/student/welcome/claim', route => route.fulfill({ json: { success: true, data: null } }));
    await page.route('**/api/auth/session', route => route.fulfill({
      json: { success: true, data: { user: student, authorizationVersion: 1 } },
    }));
    await page.route('**/api/public/settings', route => route.fulfill({ json: { maintenanceMode: false } }));
    await page.route('**/api/student/shell-bootstrap', route => route.fulfill({
      json: { success: true, data: { unreadNotificationsCount: 0, currentBalance: 0,
        gamification: { totalPoints: 0, levelName: 'طالب' }, hasSeenTrackingCodePopup: true } },
    }));
    await page.route('**/api/student/lessons', route => route.fulfill({ json: { success: true, data: lessons } }));
    await page.route('**/api/content/lessons/partial', route => route.fulfill({
      json: { success: true, data: { id: 'partial', packageId: 'history',
        title: lessons[1].title, summary: '', hasAccess: true, videos: [] } },
    }));
    await installAuthAndGoto(page, 'lessons-progress-test-token', student, `${baseURL}/student/lessons`);
  });

  test('lesson cards display the acknowledged percentage and keep unknown duration separate from zero', async ({ page }, testInfo) => {
    for (const lesson of lessons) {
      const card = page.getByRole('link').filter({ has: page.getByRole('heading', { name: lesson.title, exact: true }) });
      await expect(card).toBeVisible();
      await expect(card.getByText(`${lesson.watchedVideoCount} من ${lesson.videoCount} فيديو مكتمل · ${lesson.isCompleted ? 'مكتمل' : 'متاح للمشاهدة'}`, { exact: true })).toBeVisible();
      await expect(card.getByText('تقدّم الحصة', { exact: true })).toBeVisible();
      if (lesson.watchProgressPercent === null) {
        await expect(card.getByRole('progressbar')).toHaveCount(0);
        await expect(card.getByText('المدة غير متاحة بعد', { exact: true })).toBeVisible();
      } else {
        await expect(card.getByRole('progressbar', { name: 'تقدّم الحصة', exact: true }))
          .toHaveAttribute('aria-valuenow', String(lesson.watchProgressPercent));
        await expect(card.getByText(`${lesson.watchProgressPercent}%`, { exact: true })).toBeVisible();
      }
    }
    await expect(page.getByRole('progressbar')).toHaveCount(3);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath('my-lessons.png'), fullPage: true });
  });

  test('completion filters and title, teacher and package search still lead to the selected lesson', async ({ page }) => {
    const cards = page.getByRole('link').filter({ has: page.getByRole('heading', { level: 2 }) });
    await expect(cards).toHaveCount(4);
    await page.getByRole('button', { name: 'مكتملة', exact: true }).click();
    await expect(cards).toHaveCount(1);
    await expect(cards).toContainText(lessons[0].title);
    await page.getByRole('button', { name: 'لم تكتمل', exact: true }).click();
    await expect(cards).toHaveCount(3);
    await expect(cards.filter({ hasText: lessons[0].title })).toHaveCount(0);
    await page.getByRole('button', { name: 'الكل', exact: true }).click();
    const search = page.getByRole('textbox', { name: 'ابحث في دروسك' });
    for (const query of ['التفاعلات', 'مدرس الكيمياء', 'باقة الكيمياء']) {
      await search.fill(query);
      await expect(cards).toHaveCount(1);
      await expect(cards).toContainText(lessons[3].title);
    }
    await search.fill('بحث بلا نتائج');
    await expect(cards).toHaveCount(0);
    await expect(page.getByText('لا توجد دروس مطابقة للبحث.')).toBeVisible();
    await search.fill('مصادر دراسة');
    await expect(cards).toHaveCount(1);
    await expect(cards).toHaveAttribute('href', '/student/packages/history/lessons/partial');
    await cards.click();
    await expect(page).toHaveURL(/\/student\/packages\/history\/lessons\/partial$/);
    await expect(page.getByRole('heading', { name: lessons[1].title, exact: true })).toBeVisible();
  });
});
